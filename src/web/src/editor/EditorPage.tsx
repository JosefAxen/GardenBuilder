import { Link } from 'react-router'
import { SceneCanvas } from './SceneCanvas'
import { useSceneStore } from './sceneStore'

export function EditorPage() {
  const { undo, redo, past, future } = useSceneStore()
  return (
    <div style={{ display: 'grid', gridTemplateRows: 'auto 1fr', height: '100dvh' }}>
      <header style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', padding: '0.5rem 1rem' }}>
        <Link to="/">Drömträdgård</Link>
        <span style={{ flex: 1 }} />
        <button onClick={undo} disabled={past.length === 0}>
          Ångra
        </button>
        <button onClick={redo} disabled={future.length === 0}>
          Gör om
        </button>
      </header>
      <SceneCanvas />
    </div>
  )
}
