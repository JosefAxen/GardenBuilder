import { X } from 'lucide-react'
import { IconButton } from '../../ui'
import { useEditorUiStore } from '../editorUiStore'
import { DreamPanel } from '../dream/DreamPanel'
import { PhotoPanel } from '../photos/PhotoPanel'
import { ProposalPanel } from '../proposals/ProposalPanel'
import { useSceneStore } from '../sceneStore'
import { finishShaping } from '../shapeCommands'
import { viewLocked } from './viewLock'

const TITLES = { photos: 'Foton', proposals: 'Förslag', dream: 'Drömbild' } as const

/** Foton, Förslag or Drömbild, beside the tool rail: a tall card with its own scroll, closed back to the catalogue. */
export function SidePanel({ ready }: { ready: boolean }) {
  const panel = useEditorUiStore((s) => s.panel)
  if (!ready || panel === 'catalog') return null

  const close = () => {
    // Closing Foton while showing where a photo was taken ends that, as Esc does; nothing is left half-done.
    if (viewLocked(useSceneStore.getState().tool.mode)) finishShaping()
    useEditorUiStore.getState().setPanel('catalog')
  }

  return (
    <aside className="side-panel" id="editor-panel" aria-labelledby="side-panel-title">
      <header className="side-panel__header">
        <h2 id="side-panel-title">{TITLES[panel]}</h2>
        <IconButton icon={X} label="Stäng" onClick={close} />
      </header>
      {panel === 'photos' ? <PhotoPanel /> : panel === 'proposals' ? <ProposalPanel /> : <DreamPanel />}
    </aside>
  )
}
