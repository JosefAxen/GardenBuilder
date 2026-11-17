import { create } from 'zustand'
import { createDesign, DesignError, fetchDesign, saveWorkingCopy, type SavedDesign } from '../api/designs'
import type { SceneDocument } from '../api/scene'
import { useSceneStore } from './sceneStore'

/** How long the scene must be still before it is saved, so a drag or quick clicks become one save. */
const DEBOUNCE_MS = 1_000
/** Retries after 2, 4, 8, 16, 32 s, then every 60 s. */
const FIRST_RETRY_MS = 2_000
const MAX_RETRY_MS = 60_000
/** Browsers refuse keepalive requests with larger bodies (64 kB in all, for every keepalive request of the page). */
const KEEPALIVE_MAX_BYTES = 60_000

export type SaveStatus = 'idle' | 'pending' | 'saving' | 'saved' | 'retrying' | 'conflict' | 'invalid'

type SaveState = {
  status: SaveStatus
  /** Null until the first save has created the design. */
  designId: string | null
  /** The revision this tab last saved or loaded; sent as If-Match. */
  revision: number
  /** The server's revision after a 412, until the customer chooses. */
  conflictRevision: number | null
}

/** Saving of the customer's design. Separate from the scene store: a save is not a change to the design and is not undone. */
export const useSaveStore = create<SaveState>(() => ({ status: 'idle', designId: null, revision: 0, conflictRevision: null }))

const messages: Record<SaveStatus, string> = {
  idle: '',
  pending: 'Sparar…',
  saving: 'Sparar…',
  saved: 'Sparad',
  retrying: 'Inte sparad, försöker igen…',
  conflict: 'Designen har ändrats i en annan flik.',
  invalid: 'Designen kunde inte sparas.',
}

export const statusText = (status: SaveStatus) => messages[status]

/** False until the design has been fetched: before that, a save could create a second design beside the real one. */
let started = false
/** The document the server has: the one last saved or loaded. Compared by object, since every command makes a new one. */
let savedDocument: SceneDocument | null = null
/** The document the editor shows now. */
let shownDocument: SceneDocument | null = null
/** The newest change not yet sent, or null. */
let waiting: SceneDocument | null = null
let timer: ReturnType<typeof setTimeout> | undefined
let saving = false
/** A save was due while another was on its way; it goes as soon as that one answers. */
let saveWhenDone = false
let retries = 0
/** Bumped by startSaving, so an answer that belongs to an earlier start is thrown away. */
let generation = 0

const set = useSaveStore.setState
const get = useSaveStore.getState

/** Starts saving. `shown` is the document the editor shows now; it counts as saved. */
export function startSaving(saved: SavedDesign | null, shown: SceneDocument) {
  clearTimeout(timer)
  generation++
  started = true
  savedDocument = shown
  shownDocument = shown
  waiting = null
  saving = false
  saveWhenDone = false
  retries = 0
  set({ status: saved ? 'saved' : 'idle', designId: saved?.id ?? null, revision: saved?.revision ?? 0, conflictRevision: null })
}

/** Saves the document one second after the last call. Call it with every new document the editor shows. */
export function scheduleSave(document: SceneDocument) {
  if (!started) return
  shownDocument = document
  const { status } = get()

  if (document === savedDocument && !saving) {
    // Back to what the server has, for example after undo: nothing to send. During a save the server is about to
    // have something else, so then it is a change like any other.
    waiting = null
    if (status === 'pending' || status === 'invalid' || status === 'retrying') {
      clearTimeout(timer)
      retries = 0
      set({ status: get().designId ? 'saved' : 'idle' })
    }
    return
  }

  waiting = document
  // In a conflict the customer must choose first. While retrying, the next attempt takes the newest document, or,
  // when an attempt is on its way, a save follows it if it succeeds.
  if (status === 'conflict') return
  if (status === 'retrying') {
    if (saving) saveWhenDone = true
    return
  }
  if (!saving) set({ status: 'pending' })
  clearTimeout(timer)
  timer = setTimeout(() => void flush(), DEBOUNCE_MS)
}

/** Sends the waiting document. Only one save is on its way at a time. */
async function flush(keepalive = false) {
  clearTimeout(timer)
  if (saving) {
    saveWhenDone = true
    return
  }
  const document = waiting
  if (!document) return
  waiting = null
  saving = true
  const run = generation
  // A retry keeps saying that the design is not saved until a save gets through.
  if (get().status !== 'retrying') set({ status: 'saving' })

  try {
    const { designId, revision } = get()
    if (designId === null) {
      const design = await createDesign(document, { keepalive })
      if (run !== generation) return
      set({ designId: design.id, revision: design.revision })
    } else {
      const next = await saveWorkingCopy(designId, revision, document, { keepalive })
      if (run !== generation) return
      set({ revision: next })
    }
    savedDocument = document
    retries = 0
    set({ status: waiting ? 'pending' : 'saved' })
  } catch (error) {
    if (run !== generation) return
    const reason = error instanceof DesignError ? error : new DesignError('unavailable', String(error))
    if (reason.reason === 'conflict' && (await savedAfterAll(document, reason, run))) return
    if (run !== generation) return
    failed(document, reason)
  } finally {
    if (run === generation) {
      saving = false
      const { status } = get()
      const due = saveWhenDone && waiting && status !== 'conflict' && status !== 'retrying'
      saveWhenDone = false
      if (due) void flush()
    }
  }
}

/**
 * A conflict can be this tab's own save: the server stored it but the answer was lost, and the retry then met the
 * revision it had made itself. When the server's design is the one that was sent, that is adopted without a
 * conflict box. Otherwise the conflict stays, with the server's revision as fetched now.
 */
async function savedAfterAll(document: SceneDocument, error: DesignError, run: number): Promise<boolean> {
  const designId = error.designId ?? get().designId
  if (designId === null) return false
  if (error.designId) set({ designId })
  try {
    const server = await fetchDesign(designId)
    if (run !== generation) return true
    if (canonical(server.scene) !== canonical(document)) {
      failed(document, new DesignError('conflict', error.message, server.revision, designId))
      return true
    }
    savedDocument = document
    retries = 0
    set({ designId, revision: server.revision, conflictRevision: null, status: waiting ? 'pending' : 'saved' })
    return true
  } catch {
    return false
  }
}

/** JSON with the keys of every object sorted, so two scenes compare equal whatever order the server gives keys in. */
function canonical(value: unknown): string {
  return JSON.stringify(value, (_key, item: unknown) =>
    item && typeof item === 'object' && !Array.isArray(item)
      ? Object.fromEntries(Object.entries(item).sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0)))
      : item,
  )
}

function failed(document: SceneDocument, error: DesignError) {
  // The newest change wins: a change made during the failed save is newer than the document that failed.
  waiting ??= document
  switch (error.reason) {
    case 'conflict':
      set({ status: 'conflict', conflictRevision: error.revision ?? null })
      return
    case 'not-found':
      // The design is not this customer's (for example after a new cookie): the next save creates a new one.
      set({ designId: null, revision: 0, status: 'pending' })
      timer = setTimeout(() => void flush(), DEBOUNCE_MS)
      return
    case 'invalid':
      // The same scene gets the same answer, so no retry. The next change tries again.
      if (waiting === document) waiting = null
      set({ status: 'invalid' })
      return
    case 'unavailable': {
      const delay = Math.min(FIRST_RETRY_MS * 2 ** retries, MAX_RETRY_MS)
      retries++
      set({ status: 'retrying' })
      timer = setTimeout(() => void flush(), delay)
    }
  }
}

/** After a conflict: shows the server's design and drops this tab's unsaved changes. */
export async function takeLatest() {
  const { designId } = get()
  if (designId === null) return
  const run = generation
  try {
    const latest = await fetchDesign(designId)
    if (run !== generation) return
    clearTimeout(timer)
    waiting = null
    saveWhenDone = false
    retries = 0
    savedDocument = latest.scene
    shownDocument = latest.scene
    set({ status: 'saved', revision: latest.revision, conflictRevision: null })
    useSceneStore.getState().load(latest.scene)
  } catch {
    // Still in conflict; the customer can press the button again.
  }
}

/** After a conflict: saves this tab's design over the server's. */
export function keepMine() {
  const { conflictRevision } = get()
  if (conflictRevision === null) return
  set({ revision: conflictRevision, conflictRevision: null, status: 'pending' })
  waiting ??= shownDocument
  void flush()
}

/** True while the editor shows something the server does not have. */
export function hasUnsavedChanges() {
  return started && shownDocument !== savedDocument
}

/**
 * The page is going away: sends what is not saved yet, now. Browsers limit keepalive bodies, so a larger scene is sent
 * as an ordinary request, which may not finish; the leave warning has already told the customer.
 */
export function saveOnLeave(document: SceneDocument) {
  if (!started || saving || get().status === 'conflict' || document === savedDocument) return
  shownDocument = document
  waiting = document
  const bytes = new TextEncoder().encode(JSON.stringify(document)).length
  void flush(bytes < KEEPALIVE_MAX_BYTES)
}
