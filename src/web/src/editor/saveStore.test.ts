import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { SceneDocument } from '../api/scene'
import {
  hasUnsavedChanges,
  keepMine,
  saveOnLeave,
  scheduleSave,
  startSaving,
  statusText,
  takeLatest,
  useSaveStore,
} from './saveStore'
import { useSceneStore } from './sceneStore'

/** A new document object each call, as every command in the editor makes one. */
const doc = (n = 0): SceneDocument => ({ formatVersion: 1, elements: [], n } as unknown as SceneDocument)

type Call = { url: string; init?: RequestInit; answer: (r: Response) => void; fail: (e: unknown) => void }

/** A fetch whose answers the test hands out by hand. */
function manualFetch() {
  const calls: Call[] = []
  const fetchMock = vi.fn(
    (url: string, init?: RequestInit) =>
      new Promise<Response>((resolve, reject) => calls.push({ url, init, answer: resolve, fail: reject })),
  )
  vi.stubGlobal('fetch', fetchMock)
  return { fetchMock, calls }
}

const json = (status: number, body: unknown, revision?: number) =>
  new Response(JSON.stringify(body), {
    status,
    headers: revision === undefined ? {} : { ETag: `"${revision}"` },
  })
const created = (id: string, revision = 1) => json(201, { id, revision, scene: doc() }, revision)
const saved = (revision: number) => json(200, { id: 'd1', revision, scene: doc() }, revision)
const ifMatch = (call: Call) => new Headers(call.init?.headers).get('If-Match')
const settle = () => vi.advanceTimersByTimeAsync(0)

describe('saveStore', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
  })

  it('nothing is sent before the first change', async () => {
    const { fetchMock } = manualFetch()
    const shown = doc()

    startSaving(null, shown)
    scheduleSave(shown)
    await vi.advanceTimersByTimeAsync(5_000)

    expect(fetchMock).not.toHaveBeenCalled()
    expect(useSaveStore.getState().status).toBe('idle')
    expect(hasUnsavedChanges()).toBe(false)
  })

  it('saves one second after the last change', async () => {
    const { calls } = manualFetch()
    startSaving({ id: 'd1', revision: 1, scene: doc() }, doc())

    scheduleSave(doc(1))
    await vi.advanceTimersByTimeAsync(300)
    scheduleSave(doc(2))
    await vi.advanceTimersByTimeAsync(300)
    scheduleSave(doc(3))
    expect(useSaveStore.getState().status).toBe('pending')
    await vi.advanceTimersByTimeAsync(999)
    expect(calls).toHaveLength(0)

    await vi.advanceTimersByTimeAsync(1)
    expect(calls).toHaveLength(1)
    expect(calls[0].init?.method).toBe('PUT')
    expect(JSON.parse(calls[0].init?.body as string).n).toBe(3)
  })

  it('the first changes create one design', async () => {
    const { calls } = manualFetch()
    startSaving(null, doc())

    scheduleSave(doc(1))
    await vi.advanceTimersByTimeAsync(1_000)
    expect(calls).toHaveLength(1)
    expect(calls[0].init?.method).toBe('POST')
    scheduleSave(doc(2))
    await vi.advanceTimersByTimeAsync(5_000)
    expect(calls).toHaveLength(1)

    calls[0].answer(created('d1'))
    await settle()

    expect(calls).toHaveLength(2)
    expect(calls[1].url).toBe('/api/designs/d1/working-copy')
    expect(ifMatch(calls[1])).toBe('"1"')
  })

  it('a change during a save is saved after it with the new revision', async () => {
    const { calls } = manualFetch()
    startSaving({ id: 'd1', revision: 1, scene: doc() }, doc())
    scheduleSave(doc(1))
    await vi.advanceTimersByTimeAsync(1_000)
    expect(ifMatch(calls[0])).toBe('"1"')

    scheduleSave(doc(2))
    await vi.advanceTimersByTimeAsync(1_000)
    calls[0].answer(saved(2))
    await settle()

    expect(calls).toHaveLength(2)
    expect(ifMatch(calls[1])).toBe('"2"')
    expect(JSON.parse(calls[1].init?.body as string).n).toBe(2)
    calls[1].answer(saved(3))
    await settle()
    expect(useSaveStore.getState()).toMatchObject({ status: 'saved', revision: 3 })
    expect(hasUnsavedChanges()).toBe(false)
  })

  it('retries with backoff and saves when the server is back', async () => {
    const { calls } = manualFetch()
    startSaving({ id: 'd1', revision: 1, scene: doc() }, doc())
    scheduleSave(doc(1))
    await vi.advanceTimersByTimeAsync(1_000)

    calls[0].answer(new Response(null, { status: 503 }))
    await settle()
    expect(useSaveStore.getState().status).toBe('retrying')
    await vi.advanceTimersByTimeAsync(1_999)
    expect(calls).toHaveLength(1)
    await vi.advanceTimersByTimeAsync(1)
    expect(calls).toHaveLength(2)

    calls[1].fail(new TypeError('Failed to fetch'))
    await settle()
    await vi.advanceTimersByTimeAsync(3_999)
    expect(calls).toHaveLength(2)
    await vi.advanceTimersByTimeAsync(1)
    expect(calls).toHaveLength(3)

    calls[2].answer(saved(2))
    await settle()
    expect(useSaveStore.getState().status).toBe('saved')
    expect(hasUnsavedChanges()).toBe(false)
  })

  it('a retry on its way still shows retrying', async () => {
    const { calls } = manualFetch()
    startSaving({ id: 'd1', revision: 1, scene: doc() }, doc())
    scheduleSave(doc(1))
    await vi.advanceTimersByTimeAsync(1_000)
    calls[0].answer(new Response(null, { status: 503 }))
    await settle()

    await vi.advanceTimersByTimeAsync(2_000)
    expect(calls).toHaveLength(2)
    expect(useSaveStore.getState().status).toBe('retrying')

    calls[1].answer(saved(2))
    await settle()
    expect(useSaveStore.getState().status).toBe('saved')
  })

  it('a change while a retry is on its way is saved after it', async () => {
    const { calls } = manualFetch()
    startSaving({ id: 'd1', revision: 1, scene: doc() }, doc())
    scheduleSave(doc(1))
    await vi.advanceTimersByTimeAsync(1_000)
    calls[0].answer(new Response(null, { status: 503 }))
    await settle()
    await vi.advanceTimersByTimeAsync(2_000)

    scheduleSave(doc(2))
    calls[1].answer(saved(2))
    await settle()
    await vi.advanceTimersByTimeAsync(1_000)

    expect(calls).toHaveLength(3)
    expect(ifMatch(calls[2])).toBe('"2"')
    expect(JSON.parse(calls[2].init?.body as string).n).toBe(2)
  })

  it('undo back to the saved design during a save sends it again', async () => {
    const { calls } = manualFetch()
    const shown = doc()
    startSaving({ id: 'd1', revision: 1, scene: shown }, shown)
    scheduleSave(doc(1))
    await vi.advanceTimersByTimeAsync(1_000)

    scheduleSave(shown)
    calls[0].answer(saved(2))
    await settle()
    await vi.advanceTimersByTimeAsync(1_000)

    expect(calls).toHaveLength(2)
    expect(JSON.parse(calls[1].init?.body as string).n).toBe(0)
    calls[1].answer(saved(3))
    await settle()
    expect(useSaveStore.getState().status).toBe('saved')
    expect(hasUnsavedChanges()).toBe(false)
  })

  it('undo back to the saved design while retrying ends the retries', async () => {
    const { calls } = manualFetch()
    const shown = doc()
    startSaving({ id: 'd1', revision: 1, scene: shown }, shown)
    scheduleSave(doc(1))
    await vi.advanceTimersByTimeAsync(1_000)
    calls[0].answer(new Response(null, { status: 503 }))
    await settle()

    scheduleSave(shown)
    await vi.advanceTimersByTimeAsync(120_000)

    expect(calls).toHaveLength(1)
    expect(useSaveStore.getState().status).toBe('saved')
    expect(hasUnsavedChanges()).toBe(false)
  })

  it('conflict stops saving until the customer chooses', async () => {
    const { calls } = manualFetch()
    startSaving({ id: 'd1', revision: 1, scene: doc() }, doc())
    scheduleSave(doc(1))
    await vi.advanceTimersByTimeAsync(1_000)

    calls[0].answer(json(412, { code: 'conflict', revision: 4 }))
    await settle()
    expect(useSaveStore.getState()).toMatchObject({ status: 'conflict', conflictRevision: 4 })
    scheduleSave(doc(2))
    await vi.advanceTimersByTimeAsync(120_000)
    expect(calls).toHaveLength(1)
    expect(hasUnsavedChanges()).toBe(true)

    keepMine()
    await settle()
    expect(calls).toHaveLength(2)
    expect(ifMatch(calls[1])).toBe('"4"')
    expect(JSON.parse(calls[1].init?.body as string).n).toBe(2)
    calls[1].answer(saved(5))
    await settle()
    expect(useSaveStore.getState()).toMatchObject({ status: 'saved', revision: 5, conflictRevision: null })
  })

  it('takeLatest loads the server design', async () => {
    const { calls } = manualFetch()
    startSaving({ id: 'd1', revision: 1, scene: doc() }, doc())
    scheduleSave(doc(1))
    await vi.advanceTimersByTimeAsync(1_000)
    calls[0].answer(json(412, { code: 'conflict', revision: 4 }))
    await settle()

    const latest = takeLatest()
    await settle()
    expect(calls[1].url).toBe('/api/designs/d1')
    calls[1].answer(json(200, { id: 'd1', revision: 4, scene: doc(40) }, 4))
    await latest

    expect((useSceneStore.getState().document as unknown as { n: number }).n).toBe(40)
    expect(useSaveStore.getState()).toMatchObject({ status: 'saved', revision: 4, conflictRevision: null })
    scheduleSave(useSceneStore.getState().document)
    await vi.advanceTimersByTimeAsync(5_000)
    expect(calls).toHaveLength(2)
    expect(hasUnsavedChanges()).toBe(false)
  })

  it('invalid is not retried', async () => {
    const { calls } = manualFetch()
    startSaving({ id: 'd1', revision: 1, scene: doc() }, doc())
    scheduleSave(doc(1))
    await vi.advanceTimersByTimeAsync(1_000)

    calls[0].answer(json(400, { code: 'invalid-scene', problems: [] }))
    await settle()
    expect(useSaveStore.getState().status).toBe('invalid')
    await vi.advanceTimersByTimeAsync(60_000)
    expect(calls).toHaveLength(1)

    scheduleSave(doc(2))
    await vi.advanceTimersByTimeAsync(1_000)
    expect(calls).toHaveLength(2)
  })

  it('not-found starts a new design on the next save', async () => {
    const { calls } = manualFetch()
    startSaving({ id: 'gone', revision: 3, scene: doc() }, doc())
    scheduleSave(doc(1))
    await vi.advanceTimersByTimeAsync(1_000)

    calls[0].answer(new Response(null, { status: 404 }))
    await settle()
    await vi.advanceTimersByTimeAsync(1_000)

    expect(calls).toHaveLength(2)
    expect(calls[1].init?.method).toBe('POST')
    expect(JSON.parse(calls[1].init?.body as string).n).toBe(1)
  })

  it('saveOnLeave uses keepalive only under 60 000 bytes', async () => {
    const { calls } = manualFetch()
    startSaving({ id: 'd1', revision: 1, scene: doc() }, doc())

    saveOnLeave(doc(1))
    await settle()
    expect(calls[0].init?.keepalive).toBe(true)
    calls[0].answer(saved(2))
    await settle()

    const big = { ...doc(2), padding: 'x'.repeat(60_000) } as unknown as SceneDocument
    saveOnLeave(big)
    await settle()
    expect(calls[1].init?.keepalive).toBe(false)
    expect(ifMatch(calls[1])).toBe('"2"')
  })

  it('statusText uses the Swedish copy', () => {
    expect(statusText('idle')).toBe('')
    expect(statusText('pending')).toBe('Sparar…')
    expect(statusText('saving')).toBe('Sparar…')
    expect(statusText('saved')).toBe('Sparad')
    expect(statusText('retrying')).toBe('Inte sparad, försöker igen…')
    expect(statusText('conflict')).toBe('Designen har ändrats i en annan flik.')
    expect(statusText('invalid')).toBe('Designen kunde inte sparas.')
  })
})
