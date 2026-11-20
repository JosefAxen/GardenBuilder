import { X } from 'lucide-react'
import { IconButton } from '../../ui'
import { useEditorUiStore } from '../editorUiStore'
import { PhotoPanel } from '../photos/PhotoPanel'
import { ProposalPanel } from '../proposals/ProposalPanel'

const TITLES = { photos: 'Foton', proposals: 'Förslag' } as const

/** Foton or Förslag, beside the tool rail: a tall card with its own scroll, closed back to the catalogue. */
export function SidePanel({ ready }: { ready: boolean }) {
  const panel = useEditorUiStore((s) => s.panel)
  if (!ready || panel === 'catalog') return null

  return (
    <aside className="side-panel" id="editor-panel" aria-labelledby="side-panel-title">
      <header className="side-panel__header">
        <h2 id="side-panel-title">{TITLES[panel]}</h2>
        <IconButton icon={X} label="Stäng" onClick={() => useEditorUiStore.getState().setPanel('catalog')} />
      </header>
      {panel === 'photos' ? <PhotoPanel /> : <ProposalPanel />}
    </aside>
  )
}
