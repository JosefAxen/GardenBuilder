import { afterEach, describe, expect, it, vi } from 'vitest'
import { chooseProposal, fetchLatestProposals, ProposalError, startProposals } from './proposals'

function answer(status: number, body: unknown) {
  const fetchMock = vi.fn(async (_url: string, _init?: RequestInit) => new Response(body === undefined ? null : JSON.stringify(body), { status }))
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

const wishes = { style: 'natural', budget: '50k-150k', care: 'medium', zone: 3, note: '' }

describe('proposals api', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('starts a round with the design and the wishes', async () => {
    const fetchMock = answer(202, { id: 'job-1' })

    expect(await startProposals('g1', 'd1', wishes)).toBe('job-1')
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/gardens/g1/design-proposals')
    expect(JSON.parse(init!.body as string)).toEqual({ designId: 'd1', ...wishes })
  })

  it('invalid-wishes carries the server problems', async () => {
    answer(400, { code: 'invalid-wishes', problems: ['Välj en stil i listan.'] })

    const error = await startProposals('g1', 'd1', wishes).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ProposalError)
    expect((error as ProposalError).problems).toEqual(['Välj en stil i listan.'])
  })

  it('quota-reached says so in Swedish', async () => {
    answer(409, { code: 'quota-reached' })

    await expect(startProposals('g1', 'd1', wishes)).rejects.toThrow('Du har fått förslag fem gånger för den här trädgården.')
  })

  it('no round yet is null', async () => {
    answer(404, undefined)

    expect(await fetchLatestProposals('g1')).toBeNull()
  })

  it('chooses with the design and the index', async () => {
    const fetchMock = answer(204, undefined)

    await chooseProposal('g1', 'job-1', 'd1', 1)

    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/gardens/g1/design-proposals/job-1/chosen')
    expect(JSON.parse(init!.body as string)).toEqual({ designId: 'd1', index: 1 })
  })
})
