import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { SceneDocument } from '../../api/scene'
import { useSaveStore } from '../saveStore'
import { applyCommand, useSceneStore } from '../sceneStore'
import { chooseProposal, FALLBACK, loadProposals, startProposals, tryProposal, useProposalStore } from './proposalStore'

const plot = {
  kind: 'plot' as const,
  id: 'plot',
  status: 'existing-keep' as const,
  measurementSource: 'customer' as const,
  sloped: false,
  points: [{ x: 0, y: 0 }, { x: 20, y: 0 }, { x: 20, y: 20 }, { x: 0, y: 20 }],
}
const today: SceneDocument = { formatVersion: 2, elements: [plot] }
const proposalScene: SceneDocument = {
  formatVersion: 2,
  elements: [plot, { kind: 'point', id: 'tree', status: 'new', measurementSource: 'estimated', articleId: 'maple', position: { x: 5, y: 5 }, rotation: 0 }],
}
const round = (status: string) => ({
  id: 'job-1',
  status,
  failure: null,
  result: status === 'succeeded' ? { proposals: [{ name: 'Grön', motivation: 'Lugn.', scene: proposalScene, low: 90000, high: 110000, overBudget: false, dropped: [] }] } : null,
})

describe('the proposal panel', () => {
  beforeEach(() => {
    useSceneStore.getState().load(today)
    useProposalStore.setState({ gardenId: 'g1', proposals: [], state: 'idle', message: null, problems: [], tried: null, jobId: null })
    useSaveStore.setState({ designId: 'd1', gardenId: 'g1', status: 'saved' })
  })
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.useRealTimers()
  })

  it('replace-scene and undo give back the exact scene before', () => {
    expect(applyCommand(today, { type: 'replace-scene', document: proposalScene })).toBe(proposalScene)
    useSceneStore.getState().execute({ type: 'replace-scene', document: proposalScene })
    useSceneStore.getState().undo()
    expect(useSceneStore.getState().document).toBe(today)
  })

  it('tryProposal replaces the scene as one undo step', () => {
    useProposalStore.setState({ proposals: round('succeeded').result!.proposals })

    tryProposal(0)

    expect(useSceneStore.getState().document).toBe(proposalScene)
    expect(useProposalStore.getState().tried).toBe(0)
    useSceneStore.getState().undo()
    expect(useSceneStore.getState().document).toBe(today)
  })

  it('choose waits for the save and then posts chosen', async () => {
    vi.useFakeTimers()
    const fetchMock = vi.fn(async (_url: string, _init?: RequestInit) => new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetchMock)
    useProposalStore.setState({ proposals: round('succeeded').result!.proposals, jobId: 'job-1' })
    useSaveStore.setState({ status: 'pending' })

    const choosing = chooseProposal(0)
    await vi.advanceTimersByTimeAsync(600)
    expect(fetchMock).not.toHaveBeenCalled()
    useSaveStore.setState({ status: 'saved' })
    await vi.advanceTimersByTimeAsync(600)
    await choosing

    expect(fetchMock.mock.calls[0][0]).toBe('/api/gardens/g1/design-proposals/job-1/chosen')
    expect(useProposalStore.getState().message).toBe('Förslaget är valt. Designa vidare som du vill.')
  })

  it('choosing after Prova and Ångra shows the proposal again before saving it', async () => {
    vi.useFakeTimers()
    vi.stubGlobal('fetch', vi.fn(async () => new Response(null, { status: 204 })))
    useProposalStore.setState({ proposals: round('succeeded').result!.proposals, jobId: 'job-1' })
    tryProposal(0)
    useSceneStore.getState().undo()

    const choosing = chooseProposal(0)
    await vi.advanceTimersByTimeAsync(600)
    await choosing

    expect(useSceneStore.getState().document).toBe(proposalScene)
  })

  it('a round made from an older garden is marked out of date and cannot be tried', async () => {
    useSaveStore.setState({ revision: 3 })
    const old = { ...round('succeeded'), result: { ...round('succeeded').result!, revision: 1 } }
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify(old), { status: 200 })))

    await loadProposals('g1')
    tryProposal(0)

    expect(useProposalStore.getState().stale).toBe(true)
    expect(useSceneStore.getState().document).toBe(today)
    expect(useProposalStore.getState().message).toBe('Trädgården har ändrats sedan förslagen togs fram. Be om nya förslag.')
  })

  it('a finished round is shown again on load', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify(round('succeeded')), { status: 200 })))

    await loadProposals('g1')

    expect(useProposalStore.getState().proposals.map((p) => p.name)).toEqual(['Grön'])
    expect(useProposalStore.getState().state).toBe('done')
  })

  it('gives up after eight minutes with the fallback', async () => {
    vi.useFakeTimers()
    vi.stubGlobal(
      'fetch',
      vi.fn(async (_url: string, init?: RequestInit) =>
        init?.method === 'POST' ? new Response(JSON.stringify({ id: 'job-1' }), { status: 202 }) : new Response(JSON.stringify(round('queued')), { status: 200 }),
      ),
    )

    const running = startProposals()
    await vi.advanceTimersByTimeAsync(8 * 60_000 + 5_000)
    await running

    expect(useProposalStore.getState().state).toBe('failed')
    expect(useProposalStore.getState().message).toBe(FALLBACK)
  })

  it('invalid-wishes shows the server problems', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ code: 'invalid-wishes', problems: ['Välj en stil i listan.'] }), { status: 400 })))

    await startProposals()

    expect(useProposalStore.getState().problems).toEqual(['Välj en stil i listan.'])
    expect(useProposalStore.getState().state).toBe('idle')
  })
})
