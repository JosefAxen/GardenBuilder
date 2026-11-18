import { create } from 'zustand'
import type { HouseLook, Interpretation } from '../../api/photos'
import type { SceneElement } from '../../api/scene'
import { applyCommand, type SceneCommand, useSceneStore } from '../sceneStore'
import { shapeProblem } from '../shapeCommands'

export type Proposal = { label: string; confidence: number; element: SceneElement }

type ReviewState = {
  /** What the photos showed that the customer has not taken or dropped yet. Not part of the design until taken. */
  proposals: Proposal[]
  house: HouseLook | null
  show: (result: Interpretation) => void
  take: (id: string) => void
  takeAll: () => void
  drop: (id: string) => void
  takeHouse: () => void
  clear: () => void
}

const NO_LONGER_FITS = 'Passar inte längre på tomten.'

/** In words for the customer: how sure the AI was. */
export function confidenceText(confidence: number): string {
  if (confidence >= 0.7) return 'säker'
  if (confidence >= 0.4) return 'ganska säker'
  return 'osäker'
}

/** Runs the command if the scene stays valid; the plot may have changed since the photos were interpreted. */
function addIfItFits(command: SceneCommand): boolean {
  const { document, execute, setNotice } = useSceneStore.getState()
  if (shapeProblem(applyCommand(document, command))) {
    setNotice(NO_LONGER_FITS)
    return false
  }
  execute(command)
  return true
}

/**
 * The customer's review of a photo interpretation. Taking a proposal is an ordinary 'add' command, so it saves and
 * undoes like everything else; dropping one only removes it from the list.
 */
export const useReviewStore = create<ReviewState>((set, get) => ({
  proposals: [],
  house: null,
  show: (result) => set({ proposals: result.elements, house: result.house }),
  take: (id) => {
    const proposal = get().proposals.find((p) => p.element.id === id)
    if (!proposal) return
    addIfItFits({ type: 'add', element: proposal.element })
    set({ proposals: get().proposals.filter((p) => p !== proposal) })
  },
  takeAll: () => {
    const { proposals } = get()
    const { document } = useSceneStore.getState()
    // Each proposal is tried on top of the ones before it; those that no longer fit stay out.
    const fitting: SceneCommand[] = []
    let trial = document
    for (const p of proposals) {
      const next = applyCommand(trial, { type: 'add', element: p.element })
      if (!shapeProblem(next)) {
        fitting.push({ type: 'add', element: p.element })
        trial = next
      }
    }
    if (fitting.length > 0) useSceneStore.getState().execute({ type: 'batch', commands: fitting })
    if (fitting.length < proposals.length) useSceneStore.getState().setNotice(NO_LONGER_FITS)
    set({ proposals: [] })
  },
  drop: (id) => set({ proposals: get().proposals.filter((p) => p.element.id !== id) }),
  takeHouse: () => {
    const { house } = get()
    const building = useSceneStore.getState().document.elements.find((e) => e.kind === 'building')
    if (!house || !building) return
    useSceneStore.getState().execute({ type: 'house-look', id: building.id, ...house })
    set({ house: null })
  },
  clear: () => set({ proposals: [], house: null }),
}))
