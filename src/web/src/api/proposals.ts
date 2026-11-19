import type { SceneDocument } from './scene'

/** What the customer asks for. Keys match ProposalWishes on the server. */
export type Wishes = { style: string; budget: string; care: string; zone: number; note: string }

/** One finished proposal: a whole scene and its approximate price including VAT. */
export type Proposal = {
  name: string
  motivation: string
  scene: SceneDocument
  low: number
  high: number
  overBudget: boolean
  dropped: string[]
}

export type ProposalRound = {
  id: string
  status: 'queued' | 'running' | 'succeeded' | 'failed'
  /** revision: the saved garden the proposals start from. */
  result: { proposals: Proposal[]; revision?: number } | null
  failure: string | null
}

const MESSAGES: Record<string, string> = {
  'invalid-wishes': 'Kontrollera dina önskemål.',
  'quota-reached': 'Du har fått förslag fem gånger för den här trädgården.',
  'budget-reached': 'Förslagen är inte tillgängliga just nu. Designa vidare själv i katalogen.',
  'step-off': 'Förslagen är inte tillgängliga just nu. Designa vidare själv i katalogen.',
  'no-such-proposal': 'Förslaget finns inte längre.',
}

/** Why a proposal call failed: the server's code, a Swedish message, and for invalid wishes the server's problems. */
export class ProposalError extends Error {
  readonly code: string
  readonly problems: string[]

  constructor(code: string, problems: string[] = []) {
    super(MESSAGES[code] ?? 'Något gick fel. Försök igen om en stund.')
    this.name = 'ProposalError'
    this.code = code
    this.problems = problems
  }
}

async function send(url: string, init: RequestInit = {}): Promise<Response> {
  let response: Response
  try {
    response = await fetch(url, { ...init, credentials: 'same-origin', signal: AbortSignal.timeout(30_000) })
  } catch {
    throw new ProposalError('unavailable')
  }
  if (response.ok) return response
  const body = (await response.json().catch(() => null)) as { code?: string; problems?: string[] } | null
  throw new ProposalError(body?.code ?? `http-${response.status}`, body?.problems ?? [])
}

const json = (body: unknown): RequestInit => ({ headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })
const rounds = (gardenId: string) => `/api/gardens/${gardenId}/design-proposals`

/** Queues "Ge mig förslag" and returns the round's id. */
export async function startProposals(gardenId: string, designId: string, wishes: Wishes): Promise<string> {
  const response = await send(rounds(gardenId), { method: 'POST', ...json({ designId, ...wishes }) })
  return ((await response.json()) as { id: string }).id
}

export async function fetchProposals(gardenId: string, jobId: string): Promise<ProposalRound> {
  return (await (await send(`${rounds(gardenId)}/${jobId}`)).json()) as ProposalRound
}

/** The garden's most recent round, or null when it has none. */
export async function fetchLatestProposals(gardenId: string): Promise<ProposalRound | null> {
  try {
    return (await (await send(`${rounds(gardenId)}/latest`)).json()) as ProposalRound
  } catch (error) {
    if (error instanceof ProposalError && error.code === 'http-404') return null
    throw error
  }
}

/** Freezes the saved design as a version chosen from this round. */
export async function chooseProposal(gardenId: string, jobId: string, designId: string, index: number): Promise<void> {
  await send(`${rounds(gardenId)}/${jobId}/chosen`, { method: 'POST', ...json({ designId, index }) })
}
