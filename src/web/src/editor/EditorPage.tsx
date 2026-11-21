import { useEffect } from 'react'
import { useKeyboardShortcuts } from './keyboard'
import { PriceDetails } from './price/PriceDetails'
import { PriceSummary } from './price/PriceSummary'
import { schedulePrice } from './priceStore'
import { useCatalogStore } from './catalogStore'
import { SceneCanvas } from './SceneCanvas'
import { useSceneStore } from './sceneStore'
import { DesignLoading, SaveConflict } from './SaveStatus'
import { EditorHeader } from './shell/EditorHeader'
import { ShapingStrip } from './shell/ShapingStrip'
import { SidePanel } from './shell/SidePanel'
import { StatusLine } from './shell/StatusLine'
import { ToolRail } from './shell/ToolRail'
import { ViewSwitch } from './shell/ViewSwitch'
import { PlantLabel, ToolHint } from './label/PlantLabel'
import { Shelf } from './shelf/Shelf'
import { ThumbnailStudio } from './shelf/ThumbnailStudio'
import { useSavedDesign } from './useSavedDesign'
import './editor.css'
import './shell/shell.css'
import './shell/panels.css'
import './price/price.css'
import './shelf/shelf.css'
import './label/label.css'

export function EditorPage() {
  const document = useSceneStore((s) => s.document)
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
            <SidePanel ready={ready} />
          </div>
        </div>
        <ShapingStrip />
        <PlantLabel />
        <ToolHint />
        <StatusLine ready={ready} />
        <SaveConflict />
        <DesignLoading load={load} />
      </main>
      <PriceDetails />
      <Shelf ready={ready} />
      <ThumbnailStudio />
    </div>
  )
}
