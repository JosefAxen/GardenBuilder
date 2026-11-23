/** One dream image as the gallery shows it. Links are fresh for 10 minutes; fetch the list again for new ones. */
export type DreamImage = {
  id: string
  status: 'queued' | 'running' | 'succeeded' | 'failed'
  viewName: string
  /** The design's revision when the render was made; the gallery notes when the design has changed since. */
  revision: number
  /** Made by the test model, not a real image service. */
  test: boolean
  createdAt: string
  imageUrl: string | null
  /** The same image, but the storage answers "save as a file" (<a download> does not work across addresses). */
  downloadUrl: string | null
  /** The 3D render the customer saw, shown when the image failed. */
  renderUrl: string
  failure: string | null
}

const UNAVAILABLE = 'Drömbilder går inte att skapa just nu. Designa vidare i 3D så länge.'

const MESSAGES: Record<string, string> = {
  'invalid-image': '3D-vyn kunde inte skickas. Försök igen.',
  'no-such-view': 'Vyn finns inte längre. Spara den igen och försök på nytt.',
  'quota-reached': 'Du har skapat fem drömbilder för den här trädgården.',
  'budget-reached': UNAVAILABLE,
  'step-off': UNAVAILABLE,
  'not-failed': 'Bara bilder som misslyckades kan tas bort.',
}

/** Why a dream image call failed: the server's code and a Swedish message. */
export class DreamError extends Error {
  readonly code: string

  constructor(code: string) {
    super(MESSAGES[code] ?? 'Något gick fel. Försök igen om en stund.')
    this.name = 'DreamError'
    this.code = code
  }
}

async function send(url: string, init: RequestInit = {}): Promise<Response> {
  let response: Response
  try {
    response = await fetch(url, { ...init, credentials: 'same-origin', signal: AbortSignal.timeout(60_000) })
  } catch {
    throw new DreamError('unavailable')
  }
  if (response.ok) return response
  const body = (await response.json().catch(() => null)) as { code?: string } | null
  throw new DreamError(body?.code ?? `http-${response.status}`)
}

const dreams = (gardenId: string) => `/api/gardens/${gardenId}/dream-images`

/** Uploads the render of a saved view and queues a dream image. Returns the job's id. */
export async function startDream(gardenId: string, designId: string, viewId: string, sendPhotos: boolean, png: Blob): Promise<string> {
  const form = new FormData()
  form.append('designId', designId)
  form.append('viewId', viewId)
  form.append('sendPhotos', sendPhotos ? 'true' : 'false')
  form.append('render', png, 'vy.png')
  const response = await send(dreams(gardenId), { method: 'POST', body: form })
  return ((await response.json()) as { id: string }).id
}

/** The garden's dream images, newest first. */
export async function listDreams(gardenId: string): Promise<DreamImage[]> {
  return (await (await send(dreams(gardenId))).json()) as DreamImage[]
}

/** One image with freshly signed links; links only work for ten minutes. */
export async function getDream(gardenId: string, id: string): Promise<DreamImage> {
  return (await (await send(`${dreams(gardenId)}/${id}`)).json()) as DreamImage
}

/** Removes a failed image from the gallery. */
export async function deleteDream(gardenId: string, id: string): Promise<void> {
  await send(`${dreams(gardenId)}/${id}`, { method: 'DELETE' })
}
