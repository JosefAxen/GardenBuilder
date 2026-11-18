import { create } from 'zustand'
import {
  deletePhoto,
  fetchInterpretation,
  listPhotos,
  PhotoError,
  setCamera,
  startInterpretation,
  uploadPhoto,
  type Photo,
} from '../../api/photos'
import type { Point2 } from '../../api/scene'
import { useSaveStore } from '../saveStore'
import { useSceneStore } from '../sceneStore'
import { useReviewStore } from './reviewStore'

export type InterpretState = 'idle' | 'running' | 'done' | 'failed'

type PhotoState = {
  gardenId: string | null
  photos: Photo[]
  busy: boolean
  /** A Swedish message about the last thing that went wrong, or null. */
  message: string | null
  interpret: InterpretState
}

export const FALLBACK = 'Vi kunde inte tolka fotona. Lägg in det som finns i trädgården själv, det går snabbt.'
const POLL_MS = 2_000

/** The customer's photos and the photo interpretation's progress. The photos live on the server; this is a view of them. */
export const usePhotoStore = create<PhotoState>(() => ({ gardenId: null, photos: [], busy: false, message: null, interpret: 'idle' }))

const set = usePhotoStore.setState
const get = usePhotoStore.getState
const messageOf = (error: unknown) => (error instanceof PhotoError ? error.message : 'Något gick fel. Försök igen om en stund.')

/** Degrees from north, clockwise, of the direction from `from` to `to`. */
export function headingFrom(from: Point2, to: Point2): number {
  return ((Math.atan2(to.x - from.x, to.y - from.y) * 180) / Math.PI + 360) % 360
}

export async function loadPhotos(gardenId: string): Promise<void> {
  set({ gardenId, message: null })
  try {
    set({ photos: await listPhotos(gardenId) })
  } catch (error) {
    set({ message: messageOf(error) })
  }
}

/** Uploads one file at a time, so the server's limit of six is met in the order the customer chose. */
export async function uploadFiles(files: File[]): Promise<void> {
  const { gardenId } = get()
  if (!gardenId) return
  set({ busy: true, message: null })
  try {
    for (const file of files) {
      const photo = await uploadPhoto(gardenId, file)
      set({ photos: [...get().photos, photo] })
    }
  } catch (error) {
    set({ message: messageOf(error) })
  } finally {
    set({ busy: false })
  }
}

export async function removePhoto(photoId: string): Promise<void> {
  const { gardenId } = get()
  if (!gardenId) return
  try {
    await deletePhoto(gardenId, photoId)
    set({ photos: get().photos.filter((p) => p.id !== photoId) })
  } catch (error) {
    set({ message: messageOf(error) })
  }
}

/** Starts pinning where a photo was taken: seen from above, a click where the customer stood, then one in the direction. */
export function startPin(photoId: string): void {
  const { setTool, setView, select } = useSceneStore.getState()
  select(null)
  setTool({ mode: 'pin', photoId })
  setView('top')
}

export async function pinClick(point: Point2): Promise<void> {
  const { tool, setTool, setView } = useSceneStore.getState()
  const { gardenId } = get()
  if (tool.mode !== 'pin' || !gardenId) return
  if (!tool.position) {
    setTool({ ...tool, position: point })
    return
  }

  const camera = { x: tool.position.x, y: tool.position.y, heading: Math.round(headingFrom(tool.position, point)) }
  setTool({ mode: 'select' })
  setView('perspective')
  try {
    await setCamera(gardenId, tool.photoId, camera)
    set({ photos: get().photos.map((p) => (p.id === tool.photoId ? { ...p, camera } : p)), message: null })
  } catch (error) {
    set({ message: messageOf(error) })
  }
}

const wait = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms))

/** "Tolka foton": queues the job, asks for its status every two seconds and shows the proposal when it is done. */
export async function interpretPhotos(): Promise<void> {
  const { gardenId } = get()
  const { designId } = useSaveStore.getState()
  if (!gardenId || !designId) return
  set({ interpret: 'running', message: null })
  useReviewStore.getState().clear()
  try {
    const jobId = await startInterpretation(gardenId, designId)
    for (;;) {
      await wait(POLL_MS)
      const job = await fetchInterpretation(gardenId, jobId)
      if (job.status === 'succeeded' && job.result) {
        const empty = job.result.elements.length === 0 && !job.result.house
        useReviewStore.getState().show(job.result)
        set({ interpret: empty ? 'failed' : 'done', message: empty ? FALLBACK : null })
        return
      }
      if (job.status === 'failed') {
        set({ interpret: 'failed', message: FALLBACK })
        return
      }
    }
  } catch (error) {
    set({ interpret: 'failed', message: error instanceof PhotoError && error.code !== 'unavailable' ? error.message : FALLBACK })
  }
}
