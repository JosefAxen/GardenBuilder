import { create } from 'zustand'
import type { SceneDocument, SceneElement, Vec2 } from '../api/scene'

/**
 * Editor state. The scene document lives here and React Three Fiber renders from it;
 * no state lives only in three.js objects. Every change is a command, which gives undo and redo.
 */
export type SceneCommand =
  | { type: 'add'; element: SceneElement }
  | { type: 'move'; id: string; position: Vec2 }
  | { type: 'remove'; id: string }

type SceneState = {
  document: SceneDocument
  past: SceneDocument[]
  future: SceneDocument[]
  execute: (command: SceneCommand) => void
  undo: () => void
  redo: () => void
}

export function applyCommand(document: SceneDocument, command: SceneCommand): SceneDocument {
  switch (command.type) {
    case 'add':
      return { ...document, elements: [...document.elements, command.element] }
    case 'move':
      return {
        ...document,
        elements: document.elements.map((e) =>
          e.id === command.id && e.kind === 'point' ? { ...e, position: command.position } : e,
        ),
      }
    case 'remove':
      return { ...document, elements: document.elements.filter((e) => e.id !== command.id) }
  }
}

const demoScene: SceneDocument = {
  formatVersion: 1,
  measurementSource: 'estimated',
  elements: [
    { id: 'plot', kind: 'plot', status: 'existing-keep', points: [{ x: -12, y: -9 }, { x: 12, y: -9 }, { x: 12, y: 9 }, { x: -12, y: 9 }] },
    { id: 'house', kind: 'building', status: 'existing-keep', height: 5, roof: 'gable', points: [{ x: -5, y: 2 }, { x: 5, y: 2 }, { x: 5, y: 8 }, { x: -5, y: 8 }] },
    { id: 'lawn', kind: 'surface', status: 'new', material: 'grass', points: [{ x: -11, y: -8 }, { x: 11, y: -8 }, { x: 11, y: 1 }, { x: -11, y: 1 }] },
    { id: 'tree-1', kind: 'point', status: 'new', position: { x: -8, y: -5 }, rotation: 0 },
    { id: 'tree-2', kind: 'point', status: 'new', position: { x: 7, y: -4 }, rotation: 0 },
  ],
}

export const useSceneStore = create<SceneState>((set) => ({
  document: demoScene,
  past: [],
  future: [],
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
}))
