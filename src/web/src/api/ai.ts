/** The AI steps that have a quota per garden and an on/off switch (AiJobKinds in the AI module). */
export type AiStep = 'photo-interpretation' | 'design-proposal' | 'dream-image'

export const AI_STEPS: AiStep[] = ['photo-interpretation', 'design-proposal', 'dream-image']

/** GET /api/ai/settings (AiAdminEndpoints.SettingsResponse). Amounts are SEK. */
export type AiSettings = {
  dailyBudget: number
  todayCost: number
  quotas: Record<AiStep, number>
  enabled: Record<AiStep, boolean>
}

export type AiJobStatus = 'queued' | 'running' | 'succeeded' | 'failed'

/** The connection test's result: the answer, the model, the time and the cost in SEK. */
export type ConnectionTestResult = { answer: string; model: string; durationMs: number; cost: number }

/** GET /api/ai/jobs/{id} (AiAdminEndpoints.JobResponse). */
export type AiJob = {
  id: string
  kind: string
  status: AiJobStatus
  attempts: number
  result: ConnectionTestResult | null
  failure: string | null
}

/** The server refused the settings; `problems` are Swedish messages to show. */
export class AiSettingsError extends Error {
  readonly problems: string[]

  constructor(problems: string[]) {
    super(problems.join(' '))
    this.name = 'AiSettingsError'
    this.problems = problems
  }
}

const REFUSALS: Record<string, string> = {
  'budget-reached': 'AI-stegen är pausade för i dag.',
  'step-off': 'Det här AI-steget är avstängt.',
  'quota-reached': 'Kvoten är nådd.',
}

async function send(path: string, init: RequestInit = {}): Promise<Response> {
  return fetch(path, { ...init, credentials: 'same-origin' })
}

async function ok(response: Response, what: string): Promise<Response> {
  if (!response.ok) throw new Error(`${what} misslyckades (HTTP ${response.status}).`)
  return response
}

/** Starts "Testa AI-anslutningen" and returns the job's id. */
export async function startConnectionTest(): Promise<string> {
  const response = await send('/api/ai/connection-test', { method: 'POST' })
  if (response.status === 409) {
    const body = (await response.json().catch(() => null)) as { code?: string } | null
    throw new Error(REFUSALS[body?.code ?? ''] ?? 'Testet kunde inte startas.')
  }
  return ((await (await ok(response, 'Testet')).json()) as { id: string }).id
}

export async function fetchJob(id: string): Promise<AiJob> {
  return (await (await ok(await send(`/api/ai/jobs/${id}`), 'Hämtningen av jobbet')).json()) as AiJob
}

export async function fetchAiSettings(): Promise<AiSettings> {
  return (await (await ok(await send('/api/ai/settings'), 'Hämtningen av inställningarna')).json()) as AiSettings
}

/** Saves budget, quotas and switches. Today's cost is the server's to count, so it is not sent. */
export async function saveAiSettings(settings: AiSettings): Promise<void> {
  const response = await send('/api/ai/settings', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ dailyBudget: settings.dailyBudget, quotas: settings.quotas, enabled: settings.enabled }),
  })
  if (response.status === 400) {
    const body = (await response.json().catch(() => null)) as { problems?: string[] } | null
    throw new AiSettingsError(body?.problems ?? ['Inställningarna kunde inte sparas.'])
  }
  await ok(response, 'Sparandet')
}
