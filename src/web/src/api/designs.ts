import type { SceneDocument } from './scene'

/** A design as the server has it (DesignEndpoints.DesignResponse in the Design module). */
export type SavedDesign = { id: string; revision: number; scene: SceneDocument }

/**
 * Why a design could not be fetched or saved:
 * - conflict: someone saved a newer revision, for example in another tab (412), or the customer already has a design
 *   when this tab tried to create one (409). `revision` is the server's; `designId` names the existing design on a 409.
 * - not-found: no such design for this customer (404).
 * - invalid: the server refused the scene (400 or 413). Sending it again gives the same answer.
 * - unavailable: no answer, a timeout or a server error. Worth trying again.
 */
export class DesignError extends Error {
  readonly reason: 'conflict' | 'not-found' | 'invalid' | 'unavailable'
  readonly revision?: number
  readonly designId?: string

  constructor(reason: DesignError['reason'], message: string, revision?: number, designId?: string) {
    super(message)
    this.name = 'DesignError'
    this.reason = reason
    this.revision = revision
    this.designId = designId
  }
}

type SendOptions = { keepalive?: boolean; timeoutMs?: number }

async function send(url: string, init: RequestInit, { keepalive = false, timeoutMs = 10_000 }: SendOptions = {}) {
  let response: Response
  try {
    response = await fetch(url, { ...init, credentials: 'same-origin', keepalive, signal: AbortSignal.timeout(timeoutMs) })
  } catch {
    throw new DesignError('unavailable', 'No answer from the server.')
  }

  if (response.ok) return response
  switch (response.status) {
    case 412: {
      const body = (await response.json().catch(() => null)) as { revision?: number } | null
      throw new DesignError('conflict', 'The design has a newer revision on the server.', body?.revision)
    }
    case 409: {
      const body = (await response.json().catch(() => null)) as { id?: string; revision?: number } | null
      throw new DesignError('conflict', 'The customer already has a design.', body?.revision, body?.id)
    }
    case 404:
      throw new DesignError('not-found', 'No such design for this customer.')
    case 400:
    case 413:
      throw new DesignError('invalid', `The server refused the design: HTTP ${response.status}`)
    default:
      throw new DesignError('unavailable', `The server could not answer: HTTP ${response.status}`)
  }
}

/** The revision in an ETag such as "7" or W/"7", or null when there is none. */
function revisionFrom(response: Response): number | null {
  const match = /^(?:W\/)?"?(\d+)"?$/.exec(response.headers.get('ETag')?.trim() ?? '')
  return match ? Number(match[1]) : null
}

const jsonHeaders = { 'Content-Type': 'application/json' }

/** The customer's latest design, or null when the customer has none (or no cookie yet). */
export async function fetchLatestDesign(): Promise<SavedDesign | null> {
  try {
    return (await (await send('/api/designs/latest', { method: 'GET' })).json()) as SavedDesign
  } catch (error) {
    if (error instanceof DesignError && error.reason === 'not-found') return null
    throw error
  }
}

export async function fetchDesign(id: string): Promise<SavedDesign> {
  return (await (await send(`/api/designs/${id}`, { method: 'GET' })).json()) as SavedDesign
}

/** The first save: creates the customer (when needed), the garden and the design with revision 1. */
export async function createDesign(scene: SceneDocument, options?: SendOptions): Promise<{ id: string; revision: number }> {
  const response = await send('/api/designs', { method: 'POST', headers: jsonHeaders, body: JSON.stringify(scene) }, options)
  const body = (await response.json()) as { id: string; revision: number }
  return { id: body.id, revision: revisionFrom(response) ?? body.revision }
}

/** Saves the working copy if the server still has `revision`. Returns the new revision. */
export async function saveWorkingCopy(id: string, revision: number, scene: SceneDocument, options?: SendOptions): Promise<number> {
  const response = await send(
    `/api/designs/${id}/working-copy`,
    { method: 'PUT', headers: { ...jsonHeaders, 'If-Match': `"${revision}"` }, body: JSON.stringify(scene) },
    options,
  )
  return revisionFrom(response) ?? ((await response.json()) as { revision: number }).revision
}
