import { create } from 'zustand'
import type { SceneDocument, SceneElement, Point2 } from '../api/scene'
import { demoScene } from './demoScene'

/**
 * Editor state. The scene document lives here and React Three Fiber renders from it;
 * no state lives only in three.js objects. Every change to the document is a command, which gives undo and redo.
 */
export type SceneCommand =
  | { type: 'add'; element: SceneElement }
  | { type: 'move'; id: string; position: Point2; rotation?: number }
  /** Swaps the article but keeps place and rotation, so the customer can quickly try alternatives. */
  | { type: 'replace'; id: string; articleId: string }
  | { type: 'remove'; id: string }
  /** Several commands that undo and redo as one step. */
  | { type: 'batch'; commands: SceneCommand[] }

export function applyCommand(document: SceneDocument, command: SceneCommand): SceneDocument {
  switch (command.type) {
    case 'add':
      return { ...document, elements: [...document.elements, command.element] }
    case 'move':
      return {
        ...document,
        elements: document.elements.map((e) =>
          e.id === command.id && e.kind === 'point'
            ? { ...e, position: command.position, rotation: command.rotation ?? e.rotation }
            : e,
        ),
      }
    case 'replace':
      return {
        ...document,
        // Only elements that point at an article can be swapped; the plot and buildings have none.
        elements: document.elements.map((e) =>
          e.id === command.id && 'articleId' in e ? { ...e, articleId: command.articleId } : e,
        ),
      }
    case 'remove':
      return { ...document, elements: document.elements.filter((e) => e.id !== command.id) }
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
  detail: 'low' | 'high'
  perf: Perf | null
  execute: (command: SceneCommand) => void
  undo: () => void
  redo: () => void
  select: (id: string | null) => void
  setTool: (tool: Tool) => void
  setDrag: (drag: Drag | null) => void
  setHover: (hover: Point2 | null) => void
  setDetail: (detail: 'low' | 'high') => void
  setPerf: (perf: Perf) => void
}

export const useSceneStore = create<SceneState>((set) => ({
  document: demoScene,
  past: [],
  future: [],
  selectedId: null,
  tool: { mode: 'select' },
  drag: null,
  hover: null,
  detail: 'low',
  perf: null,
  execute: (command) =>
    set((state) => ({
      document: applyCommand(state.document, command),
      past: [...state.past, state.document],
      future: [],
    })),
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
  setTool: (tool) => set({ tool, hover: null }),
  setDrag: (drag) => set({ drag }),
  setHover: (hover) => set({ hover }),
  setDetail: (detail) => set({ detail }),
  setPerf: (perf) => set({ perf }),
}))
