import { useEffect } from 'react'
import { CatalogPanel } from './CatalogPanel'
import { useEditorUiStore } from './editorUiStore'
import { useKeyboardShortcuts } from './keyboard'
import { PhotoPanel } from './photos/PhotoPanel'
import { ProposalPanel } from './proposals/ProposalPanel'
import { PriceDetails } from './price/PriceDetails'
import { PriceSummary } from './price/PriceSummary'
import { schedulePrice } from './priceStore'
import { useCatalogStore } from './catalogStore'
import { SceneCanvas } from './SceneCanvas'
import { useSceneStore } from './sceneStore'
import { DesignLoading, SaveConflict } from './SaveStatus'
import { EditorHeader } from './shell/EditorHeader'
import { ShapingStrip } from './shell/ShapingStrip'
import { StatusLine } from './shell/StatusLine'
import { ToolRail } from './shell/ToolRail'
import { ViewSwitch } from './shell/ViewSwitch'
import { Shelf } from './shelf/Shelf'
import { useSavedDesign } from './useSavedDesign'
import './editor.css'
import './shell/shell.css'
import './price/price.css'
import './shelf/shelf.css'

export function EditorPage() {
  const document = useSceneStore((s) => s.document)
  const panel = useEditorUiStore((s) => s.panel)
  useKeyboardShortcuts()
  const load = useSavedDesign()
  const ready = load === 'ready'

  // Fetch the catalog once when the editor opens.
  useEffect(() => {
    void useCatalogStore.getState().load()
  }, [])

  // A new price half a second after the design stops changing, including after undo and redo.
  useEffect(() => schedulePrice(document), [document])

  return (
    <div className="editor">
      <EditorHeader price={<PriceSummary />} />
      <main className="editor-window">
        <SceneCanvas />
        <div className="editor-window__top-left">
          <ToolRail ready={ready} />
          <div className="editor-window__beside-rail">
            <ViewSwitch />
            {ready && panel !== 'catalog' && (
              <aside className="side-panel" id="editor-panel">
                {panel === 'photos' ? <PhotoPanel /> : <ProposalPanel />}
              </aside>
            )}
          </div>
        </div>
        <ShapingStrip />
        <CatalogPanel />
        <StatusLine ready={ready} />
        <SaveConflict />
        <DesignLoading load={load} />
      </main>
      <PriceDetails />
      <Shelf ready={ready} />
    </div>
  )
}
