import { create } from 'zustand'
import { deleteDream, DreamError, listDreams, startDream, type DreamImage } from '../../api/dreams'
import type { SceneView } from '../../api/scene'
import { useSaveStore } from '../saveStore'

export type DreamState = 'idle' | 'working' | 'failed'

type DreamStoreState = {
  gardenId: string | null
  /** The gallery, newest first. */
  images: DreamImage[]
  state: DreamState
  /** A Swedish message about the latest attempt, or null. */
  message: string | null
  /** "Skicka med mina foton": the customer's photos go along as references. Remembered per garden. */
  sendPhotos: boolean
}

export const QUOTA = 5
export const FAILED = 'Det gick inte att skapa en drömbild just nu. Här är 3D-vyn.'
const POLL_MS = 3_000
/** Longer than an image service takes, even with a queue in front. */
const GIVE_UP_MS = 5 * 60_000

export const useDreamStore = create<DreamStoreState>(() => ({
  gardenId: null,
  images: [],
  state: 'idle',
  message: null,
  sendPhotos: true,
}))

const set = useDreamStore.setState
const get = useDreamStore.getState
const wait = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms))

/** Images left for this garden. Failed ones give their place back. */
export function remaining(images: DreamImage[]): number {
  return Math.max(0, QUOTA - images.filter((i) => i.status !== 'failed').length)
}

const storageKey = (gardenId: string) => `dream-send-photos:${gardenId}`

function readSendPhotos(gardenId: string): boolean {
  try {
    return localStorage.getItem(storageKey(gardenId)) !== 'false'
  } catch {
    return true
  }
}

export function setSendPhotos(on: boolean): void {
  set({ sendPhotos: on })
  const { gardenId } = get()
  if (!gardenId) return
  try {
    localStorage.setItem(storageKey(gardenId), on ? 'true' : 'false')
  } catch {
    // The choice still holds until the page is closed.
  }
}

const isDone = (image: DreamImage | undefined) => image?.status === 'succeeded' || image?.status === 'failed'

/** Asks for the gallery every three seconds until the image is done, the garden changes, or it takes too long. */
async function follow(gardenId: string, jobId: string): Promise<void> {
  set({ state: 'working', message: null })
  const deadline = Date.now() + GIVE_UP_MS
  try {
    while (Date.now() < deadline) {
      await wait(POLL_MS)
      if (get().gardenId !== gardenId) return
      const images = await listDreams(gardenId)
      if (get().gardenId !== gardenId) return
      set({ images })
      const image = images.find((i) => i.id === jobId)
      if (isDone(image)) {
        set(image!.status === 'succeeded' ? { state: 'idle', message: null } : { state: 'failed', message: FAILED })
        return
      }
    }
    set({ state: 'failed', message: FAILED })
  } catch {
    if (get().gardenId === gardenId) set({ state: 'failed', message: FAILED })
  }
}

/** Shows the garden's gallery when the panel opens, and follows an image still on its way. */
export async function loadDreams(gardenId: string): Promise<void> {
  set({ gardenId, images: [], state: 'idle', message: null, sendPhotos: readSendPhotos(gardenId) })
  try {
    const images = await listDreams(gardenId)
    if (get().gardenId !== gardenId) return
    set({ images })
    const pending = images.find((i) => !isDone(i))
    if (pending) void follow(gardenId, pending.id)
  } catch {
    // The gallery stays empty; the button still works.
  }
}

/** "Skapa drömbild": uploads the view's render and follows the image until it is done. */
export async function createDream(view: SceneView, png: Blob): Promise<void> {
  const { gardenId, sendPhotos } = get()
  const { designId } = useSaveStore.getState()
  if (!gardenId || !designId) return
  set({ state: 'working', message: null })
  try {
    const jobId = await startDream(gardenId, designId, view.id, sendPhotos, png)
    await follow(gardenId, jobId)
  } catch (error) {
    const known = error instanceof DreamError && !error.code.startsWith('http-') && error.code !== 'unavailable'
    set({ state: 'failed', message: known ? error.message : FAILED })
  }
}

/** Removes a failed image from the gallery. */
export async function removeDream(id: string): Promise<void> {
  const { gardenId } = get()
  if (!gardenId) return
  try {
    await deleteDream(gardenId, id)
    set({ images: get().images.filter((i) => i.id !== id), message: null })
  } catch (error) {
    set({ message: error instanceof DreamError ? error.message : FAILED })
  }
}
