import type { RoofShape, SceneElement } from './scene'

/** Where a photo was taken: scene meters, heading in degrees (0 north, clockwise). */
export type Camera = { x: number; y: number; heading: number }

/** A garden photo as the server lists it. `url` stops working after ten minutes; list again for a fresh one. */
export type Photo = { id: string; width: number; height: number; url: string; camera: Camera | null }

export type HouseLook = { wallColor: string | null; roofColor: string | null; roof: RoofShape }

/** What the photo interpretation proposes (PhotoInterpretationResult in the Ai module). */
export type Interpretation = {
  elements: { label: string; confidence: number; element: SceneElement }[]
  house: HouseLook | null
  dropped: string[]
  skippedPhotos: number
}

export type InterpretationJob = { id: string; status: 'queued' | 'running' | 'succeeded' | 'failed'; result: Interpretation | null; failure: string | null }

const MESSAGES: Record<string, string> = {
  'too-many-photos': 'Du kan ladda upp högst 6 foton.',
  'too-large': 'Fotot är större än 15 MB.',
  'not-an-image': 'Filen är inte ett foto vi kan läsa. Spara det som JPEG och försök igen.',
  'camera-outside-plot': 'Stå på tomten eller högst 5 meter utanför.',
  'camera-missing': 'Visa var du stod för varje foto först.',
  'no-photos': 'Ladda upp minst ett foto först.',
  'quota-reached': 'Du har tolkat foton för den här trädgården tre gånger.',
  'budget-reached': 'Fototolkningen är inte tillgänglig just nu. Lägg in det som finns själv.',
  'step-off': 'Fototolkningen är inte tillgänglig just nu. Lägg in det som finns själv.',
}

/** Why a photo call failed: the server's code and a Swedish message for the customer. */
export class PhotoError extends Error {
  readonly code: string

  constructor(code: string) {
    super(MESSAGES[code] ?? 'Något gick fel. Försök igen om en stund.')
    this.name = 'PhotoError'
    this.code = code
  }
}

async function send(url: string, init: RequestInit = {}): Promise<Response> {
  let response: Response
  try {
    response = await fetch(url, { ...init, credentials: 'same-origin', signal: AbortSignal.timeout(60_000) })
  } catch {
    throw new PhotoError('unavailable')
  }
  if (response.ok) return response
  const body = (await response.json().catch(() => null)) as { code?: string } | null
  throw new PhotoError(body?.code ?? `http-${response.status}`)
}

const json = (body: unknown): RequestInit => ({ headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })
const photos = (gardenId: string) => `/api/gardens/${gardenId}/photos`

export async function listPhotos(gardenId: string): Promise<Photo[]> {
  return (await (await send(photos(gardenId))).json()) as Photo[]
}

export async function uploadPhoto(gardenId: string, file: File): Promise<Photo> {
  const form = new FormData()
  form.append('file', file)
  return (await (await send(photos(gardenId), { method: 'POST', body: form })).json()) as Photo
}

export async function deletePhoto(gardenId: string, photoId: string): Promise<void> {
  await send(`${photos(gardenId)}/${photoId}`, { method: 'DELETE' })
}

export async function setCamera(gardenId: string, photoId: string, camera: Camera): Promise<void> {
  await send(`${photos(gardenId)}/${photoId}/camera`, { method: 'PUT', ...json(camera) })
}

/** Queues "Tolka foton" and returns the job's id. */
export async function startInterpretation(gardenId: string, designId: string): Promise<string> {
  const response = await send(`/api/gardens/${gardenId}/photo-interpretations`, { method: 'POST', ...json({ designId }) })
  return ((await response.json()) as { id: string }).id
}

/** The garden's most recent interpretation, or null when it has none. */
export async function fetchLatestInterpretation(gardenId: string): Promise<InterpretationJob | null> {
  try {
    return (await (await send(`/api/gardens/${gardenId}/photo-interpretations/latest`)).json()) as InterpretationJob
  } catch (error) {
    if (error instanceof PhotoError && error.code === 'http-404') return null
    throw error
  }
}

export async function fetchInterpretation(gardenId: string, jobId: string): Promise<InterpretationJob> {
  return (await (await send(`/api/gardens/${gardenId}/photo-interpretations/${jobId}`)).json()) as InterpretationJob
}
