import { afterEach, describe, expect, it, vi } from 'vitest'
import { AiSettingsError, fetchAiSettings, fetchJob, saveAiSettings, startConnectionTest, type AiSettings } from './ai'

function answer(status: number, body: unknown) {
  const fetchMock = vi.fn(async (_url: string, _init?: RequestInit) => new Response(JSON.stringify(body), { status }))
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

const settings: AiSettings = {
  dailyBudget: 500,
  todayCost: 0,
  quotas: { 'photo-interpretation': 3, 'design-proposal': 5, 'dream-image': 5 },
  enabled: { 'photo-interpretation': true, 'design-proposal': true, 'dream-image': true },
}

describe('ai api', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('starts the connection test with POST and returns the job id', async () => {
    const fetchMock = answer(202, { id: 'job-1' })

    expect(await startConnectionTest()).toBe('job-1')
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/ai/connection-test')
    expect(init?.method).toBe('POST')
    expect(init?.credentials).toBe('same-origin')
  })

  it('reports a spent budget when the test is refused', async () => {
    answer(409, { code: 'budget-reached' })

    await expect(startConnectionTest()).rejects.toThrow('AI-stegen är pausade för i dag.')
  })

  it('reads a job by id', async () => {
    const fetchMock = answer(200, { id: 'job-1', kind: 'connection-test', status: 'running', attempts: 1, result: null, failure: null })

    expect((await fetchJob('job-1')).status).toBe('running')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/ai/jobs/job-1')
  })

  it('reads the settings', async () => {
    answer(200, settings)

    expect(await fetchAiSettings()).toEqual(settings)
  })

  it('saves the settings with PUT, without today\'s cost', async () => {
    const fetchMock = answer(200, {})

    await saveAiSettings(settings)

    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/ai/settings')
    expect(init?.method).toBe('PUT')
    expect(JSON.parse(init?.body as string)).toEqual({ dailyBudget: 500, quotas: settings.quotas, enabled: settings.enabled })
  })

  it('surfaces the Swedish problems of invalid settings', async () => {
    answer(400, { code: 'invalid-settings', problems: ['Dagsbudgeten ska vara mellan 0 och 100 000 kr.'] })

    const error = await saveAiSettings(settings).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(AiSettingsError)
    expect((error as AiSettingsError).problems).toEqual(['Dagsbudgeten ska vara mellan 0 och 100 000 kr.'])
  })
})
