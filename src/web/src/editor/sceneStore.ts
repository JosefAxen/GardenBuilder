import { create } from 'zustand'
import type { Point2, SceneDocument, SceneElement, SurfaceMaterial } from '../api/scene'
import type { CatalogArticle } from '../api/catalog'
import { demoScene, stressElements } from './demoScene'
import { placementProblem } from './placement'

/**
 * Editor state. The scene document lives here and React Three Fiber renders from it;
 * no state lives only in three.js objects. Every change to the document is a command, which gives undo and redo.
 */
export type SceneCommand =
  | { type: 'add'; element: SceneElement }
  | { type: 'move'; id: string; position: Point2; rotation?: number }
  /** Swaps the article but keeps place and rotation, so the customer can quickly try alternatives. */
  | { type: 'replace'; id: string; articleId: string; material?: SurfaceMaterial }
  /** Deletes a new element. An existing one stays in the scene, marked 'existing-remove', so it can be priced and kept again. */
  | { type: 'remove'; id: string }
  /** Takes back the removal of an existing element. */
  | { type: 'keep'; id: string }
  /** Several commands that undo and redo as one step. */
  | { type: 'batch'; commands: SceneCommand[] }

/**
 * Changes the one element with this id. Returns the same document when there is no such element or the update
 * leaves it as it was, so a command that changes nothing can be recognised and does not become an undo step.
 */
function updateElement(document: SceneDocument, id: string, update: (e: SceneElement) => SceneElement): SceneDocument {
  const index = document.elements.findIndex((e) => e.id === id)
  if (index < 0) return document
  const updated = update(document.elements[index])
  if (updated === document.elements[index]) return document
  return { ...document, elements: document.elements.with(index, updated) }
}

/** Applies a command. Returns the same document object when nothing changes. */
export function applyCommand(document: SceneDocument, command: SceneCommand): SceneDocument {
  switch (command.type) {
    case 'add':
      return { ...document, elements: [...document.elements, command.element] }
    case 'move':
      return updateElement(document, command.id, (e) => {
        if (e.kind !== 'point' || e.status === 'existing-remove') return e
        const rotation = command.rotation ?? e.rotation
        const unchanged = e.position.x === command.position.x && e.position.y === command.position.y && e.rotation === rotation
        return unchanged ? e : { ...e, position: command.position, rotation }
      })
    case 'replace':
      // Only elements that point at an article can be swapped; the plot and buildings have none. A surface also
      // takes the new article's material, so the scene and the catalog say the same thing.
      return updateElement(document, command.id, (e) => {
        if (!('articleId' in e) || e.status === 'existing-remove') return e
        const material = e.kind === 'surface' && command.material ? command.material : undefined
        if (e.articleId === command.articleId && (material === undefined || (e.kind === 'surface' && e.material === material))) return e
        return material ? { ...e, articleId: command.articleId, material } : { ...e, articleId: command.articleId }
      })
    case 'remove': {
      const element = document.elements.find((e) => e.id === command.id)
      if (!element) return document
      if (element.status === 'new') return { ...document, elements: document.elements.filter((e) => e.id !== command.id) }
      return updateElement(document, command.id, (e) => (e.status === 'existing-keep' ? { ...e, status: 'existing-remove' } : e))
    }
    case 'keep':
      return updateElement(document, command.id, (e) => (e.status === 'existing-remove' ? { ...e, status: 'existing-keep' } : e))
    case 'batch':
      return command.commands.reduce(applyCommand, document)
  }
}

/** What a click in the scene does. */
export type Tool =
  | { mode: 'select' }
  | { mode: 'place'; articleId: string }
  | { mode: 'line'; articleId: string; start?: Point2 }

/** An element being dragged. Kept outside the document until the drop, so one drag is one undo step. */
export type Drag = { id: string; position: Point2; offset: Point2; moved: boolean }

export type Perf = { fps: number; drawCalls: number; triangles: number }

type SceneState = {
  document: SceneDocument
  past: SceneDocument[]
  future: SceneDocument[]
  selectedId: string | null
  tool: Tool
  drag: Drag | null
  /** Ground point under the pointer, for the placement preview. */
  hover: Point2 | null
  /** A short message to the customer, in Swedish, for example why something could not be placed. */
  notice: string | null
  detail: 'low' | 'high'
  perf: Perf | null
  /** While on, the scene is drawn every frame so the fps reading means something; otherwise only on change. */
  measuring: boolean
  execute: (command: SceneCommand) => void
  undo: () => void
  redo: () => void
  select: (id: string | null) => void
  setTool: (tool: Tool) => void
  setDrag: (drag: Drag | null) => void
  /** Ends a drag where the pointer was released: one move, or nothing if the spot is not allowed. */
  dropDrag: () => void
  /** Ends a drag that was interrupted (the window lost focus, the browser cancelled the pointer): nothing moves. */
  cancelDrag: () => void
  setHover: (hover: Point2 | null) => void
  setNotice: (notice: string | null) => void
  setDetail: (detail: 'low' | 'high') => void
  setPerf: (perf: Perf) => void
  setMeasuring: (on: boolean) => void
}

export const useSceneStore = create<SceneState>((set, get) => ({
  document: demoScene,
  past: [],
  future: [],
  selectedId: null,
  tool: { mode: 'select' },
  drag: null,
  hover: null,
  notice: null,
  detail: 'low',
  perf: null,
  measuring: false,
  execute: (command) =>
    set((state) => {
      const document = applyCommand(state.document, command)
      if (document === state.document) return state
      return { document, past: [...state.past, state.document], future: [], notice: null }
    }),
  undo: () =>
    set((state) => {
      const previous = state.past.at(-1)
      if (!previous) return state
      return { document: previous, past: state.past.slice(0, -1), future: [state.document, ...state.future] }
    }),
  redo: () =>
    set((state) => {
      const [next, ...rest] = state.future
      if (!next) return state
      return { document: next, past: [...state.past, state.document], future: rest }
    }),
  select: (selectedId) => set({ selectedId }),
  setTool: (tool) => set({ tool, hover: null, notice: null }),
  setDrag: (drag) => set({ drag }),
  dropDrag: () => {
    const { drag, document, execute } = get()
    if (!drag) return
    set({ drag: null })
    if (!drag.moved) return
    const problem = placementProblem(document, [drag.position])
    if (problem) set({ notice: problem })
    else execute({ type: 'move', id: drag.id, position: drag.position })
  },
  cancelDrag: () => set({ drag: null }),
  setHover: (hover) => set({ hover }),
  setNotice: (notice) => set({ notice }),
  setDetail: (detail) => set({ detail }),
  setPerf: (perf) => set({ perf }),
  setMeasuring: (measuring) => set({ measuring, perf: null }),
}))

/**
 * PROTOTYPE: adds 300 plants from the catalog in one undo step and turns on measuring, for the performance target in
 * docs/design.md. Does nothing when no plant is in stock (for example before the catalog has loaded).
 */
export function addStressTest(articles: CatalogArticle[]) {
  const plants = articles.filter((a) => a.geometry === 'point' && a.stockStatus === 'in-stock')
  if (plants.length === 0) return
  const { document, execute, setMeasuring } = useSceneStore.getState()
  execute({ type: 'batch', commands: stressElements(300, plants, document.elements.length).map((element) => ({ type: 'add', element })) })
  setMeasuring(true)
}
