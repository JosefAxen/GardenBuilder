import { create } from 'zustand'
import {
  chooseProposal as postChosen,
  fetchLatestProposals,
  fetchProposals,
  ProposalError,
  startProposals as postStart,
  type Proposal,
  type ProposalRound,
  type Wishes,
} from '../../api/proposals'
import { useSaveStore } from '../saveStore'
import { useSceneStore } from '../sceneStore'

export type RoundState = 'idle' | 'running' | 'done' | 'failed'

type ProposalState = {
  gardenId: string | null
  wishes: Wishes
  state: RoundState
  proposals: Proposal[]
  jobId: string | null
  /** The proposal shown in the scene right now, or null. */
  tried: number | null
  /** A Swedish message about the round or the choice. */
  message: string | null
  /** What the server said was wrong with the wishes. */
  problems: string[]
  /** The garden has been saved again since the round started: trying a proposal would undo that work. */
  stale: boolean
}

export const FALLBACK = 'Vi kunde inte ta fram förslag just nu. Designa vidare själv i katalogen.'
export const STALE = 'Trädgården har ändrats sedan förslagen togs fram. Be om nya förslag.'
const POLL_MS = 2_000
/** Longer than a round of six model calls can take. */
const GIVE_UP_MS = 8 * 60_000
const SAVE_WAIT_MS = 15_000

export const useProposalStore = create<ProposalState>(() => ({
  gardenId: null,
  wishes: { style: 'natural', budget: '50k-150k', care: 'medium', zone: 3, note: '' },
  state: 'idle',
  proposals: [],
  jobId: null,
  tried: null,
  message: null,
  problems: [],
  stale: false,
}))

const set = useProposalStore.setState
const get = useProposalStore.getState
const wait = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms))

function settle(round: ProposalRound): void {
  const proposals = round.status === 'succeeded' ? (round.result?.proposals ?? []) : []
  if (proposals.length === 0) {
    set({ state: 'failed', proposals: [], message: FALLBACK, stale: false })
    return
  }
  // A proposal is the whole garden as it was when the round started; after later saves it would wipe that work.
  const revision = round.result?.revision
  const stale = typeof revision === 'number' && revision !== useSaveStore.getState().revision
  set({ state: 'done', proposals, stale, message: stale ? STALE : null })
}

/** Asks for the round's status every two seconds until it is done, the garden changes, or it takes too long. */
async function follow(gardenId: string, jobId: string): Promise<void> {
  set({ state: 'running', jobId })
  const deadline = Date.now() + GIVE_UP_MS
  try {
    while (Date.now() < deadline) {
      await wait(POLL_MS)
      if (get().gardenId !== gardenId) return
      const round = await fetchProposals(gardenId, jobId)
      if (get().gardenId !== gardenId) return
      if (round.status === 'succeeded' || round.status === 'failed') {
        settle(round)
        return
      }
    }
    set({ state: 'failed', message: FALLBACK })
  } catch {
    if (get().gardenId === gardenId) set({ state: 'failed', message: FALLBACK })
  }
}

/** Shows the garden's latest round again (or picks up one still running) when the panel opens. */
export async function loadProposals(gardenId: string): Promise<void> {
  set({ gardenId, state: 'idle', proposals: [], jobId: null, tried: null, message: null, problems: [], stale: false })
  try {
    const latest = await fetchLatestProposals(gardenId)
    if (!latest || get().gardenId !== gardenId) return
    set({ jobId: latest.id })
    if (latest.status === 'queued' || latest.status === 'running') void follow(gardenId, latest.id)
    else if (latest.status === 'succeeded') settle(latest)
  } catch {
    // No earlier round to show; the form is there.
  }
}

export function setWishes(wishes: Partial<Wishes>): void {
  set({ wishes: { ...get().wishes, ...wishes }, problems: [] })
}

/** "Ge mig förslag": queues a round for the saved design and follows it. */
export async function startProposals(): Promise<void> {
  const { gardenId, wishes } = get()
  const { designId } = useSaveStore.getState()
  if (!gardenId || !designId) return
  set({ state: 'running', proposals: [], tried: null, message: null, problems: [], stale: false })
  try {
    const jobId = await postStart(gardenId, designId, wishes)
    await follow(gardenId, jobId)
  } catch (error) {
    if (error instanceof ProposalError && error.code === 'invalid-wishes') set({ state: 'idle', problems: error.problems })
    else set({ state: 'failed', message: error instanceof ProposalError && !error.code.startsWith('http-') && error.code !== 'unavailable' ? error.message : FALLBACK })
  }
}

/** Shows a proposal in the scene as one command, so "Ångra" brings back the garden as it was. */
export function tryProposal(index: number): void {
  const proposal = get().proposals[index]
  if (!proposal) return
  if (get().stale) {
    set({ message: STALE })
    return
  }
  useSceneStore.getState().execute({ type: 'replace-scene', document: proposal.scene })
  set({ tried: index, message: null })
}

/** Keeps a proposal: shows it if needed, waits until it is saved, then freezes it as a design version. */
export async function chooseProposal(index: number): Promise<void> {
  const { gardenId, jobId } = get()
  if (!gardenId || !jobId) return
  // What the scene shows decides, not what was tried last: "Ångra" may have taken the proposal away again.
  const proposal = get().proposals[index]
  if (!proposal || get().stale) return
  if (useSceneStore.getState().document !== proposal.scene) tryProposal(index)

  const deadline = Date.now() + SAVE_WAIT_MS
  while (useSaveStore.getState().status !== 'saved' && Date.now() < deadline) await wait(500)
  const { status, designId } = useSaveStore.getState()
  if (status !== 'saved' || !designId) {
    set({ message: 'Förslaget kunde inte sparas än. Försök igen om en stund.' })
    return
  }

  try {
    await postChosen(gardenId, jobId, designId, index)
    set({ message: 'Förslaget är valt. Designa vidare som du vill.' })
  } catch (error) {
    set({ message: error instanceof ProposalError ? error.message : FALLBACK })
  }
}
