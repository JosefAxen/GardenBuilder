import { useEffect, useMemo } from 'react'
import { Link } from 'react-router'
import { CatalogPanel } from './CatalogPanel'
import { PricePanel } from './PricePanel'
import { schedulePrice } from './priceStore'
import { useCatalogStore } from './catalogStore'
import { SceneCanvas } from './SceneCanvas'
import { addStressTest, useSceneStore } from './sceneStore'
import { removeSelected, rotateSelected } from './selectionCommands'
import { finishShaping, startDrawing, startShaping } from './shapeCommands'
import { crowdingFootprints, findCrowdedPlants } from './spacing'
import { lookOf } from './existingLook'
import { countMissingArticles } from './catalogChecks'
import { DesignLoading, SaveConflict, SaveStatus } from './SaveStatus'
import { useSavedDesign } from './useSavedDesign'
import './editor.css'

function useKeyboardShortcuts() {
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.target instanceof HTMLInputElement) return
      const { undo, redo, setTool, select } = useSceneStore.getState()
      const key = event.key.toLowerCase()
      if ((event.ctrlKey || event.metaKey) && key === 'z') {
        event.preventDefault()
        if (event.shiftKey) redo()
        else undo()
      } else if ((event.ctrlKey || event.metaKey) && key === 'y') {
        event.preventDefault()
        redo()
      } else if (key === 'delete' || key === 'backspace') removeSelected()
      else if (key === 'escape') {
        setTool({ mode: 'select' })
        select(null)
      } else if (key === 'q') rotateSelected(1)
      else if (key === 'e') rotateSelected(-1)
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [])
}

function StatusBar() {
  const perf = useSceneStore((s) => s.perf)
  const elements = useSceneStore((s) => s.document.elements)
  const detail = useSceneStore((s) => s.detail)
  const notice = useSceneStore((s) => s.notice)
  const articles = useCatalogStore((s) => s.articles)
  const catalogReady = useCatalogStore((s) => s.status === 'ready')
  const measuring = useSceneStore((s) => s.measuring)
  const { setDetail, setMeasuring } = useSceneStore.getState()

  const crowdedCount = useMemo(() => {
    const plants = elements.flatMap((e) => {
      if (e.kind !== 'point') return []
      const article = lookOf(e, articles)
      return article?.geometry === 'point' ? [{ id: e.id, position: e.position, matureWidth: article.matureWidth, removed: e.status === 'existing-remove' }] : []
    })
    return findCrowdedPlants(crowdingFootprints(plants)).size
  }, [elements, articles])

  const placedCount = elements.filter((e) => e.kind === 'point' || e.kind === 'line').length
  // Only once the catalog has loaded; while loading, every article would look missing.
  const missingCount = useMemo(() => (catalogReady ? countMissingArticles(elements, articles) : 0), [catalogReady, elements, articles])
  const format = new Intl.NumberFormat('sv-SE')

  return (
    <div className="editor-status">
      <span>{placedCount} element</span>
      {crowdedCount > 0 && <span className="warning">{crowdedCount} växter står för tätt</span>}
      {missingCount > 0 && <span className="warning">{missingCount} element saknas i katalogen och visas inte</span>}
      {notice && (
        <span className="warning" role="status">
          {notice}
        </span>
      )}
      <span className="spacer" />
      <SaveStatus />
      {/* fps only means something while the scene is drawn every frame. */}
      {measuring && perf && (
        <span className="perf">
          {perf.fps} fps · {perf.drawCalls} ritanrop · {format.format(perf.triangles)} trianglar
        </span>
      )}
      <label>
        <input type="checkbox" checked={measuring} onChange={(e) => setMeasuring(e.target.checked)} /> Mät prestanda
      </label>
      <label>
        <input type="checkbox" checked={detail === 'high'} onChange={(e) => setDetail(e.target.checked ? 'high' : 'low')} /> Detaljerade modeller
      </label>
    </div>
  )
}

export function EditorPage() {
  const past = useSceneStore((s) => s.past)
  const toolMode = useSceneStore((s) => s.tool.mode)
  const shaping = toolMode === 'shape' || toolMode === 'draw'
  const future = useSceneStore((s) => s.future)
  const document = useSceneStore((s) => s.document)
  const { undo, redo, execute } = useSceneStore.getState()
  useKeyboardShortcuts()
  const load = useSavedDesign()
  const ready = load === 'ready'

  // Fetch the catalog once when the editor opens.
  useEffect(() => {
    void useCatalogStore.getState().load()
  }, [])

  // A new price half a second after the design stops changing, including after undo and redo.
  useEffect(() => schedulePrice(document), [document])

  const addStress = () => addStressTest(useCatalogStore.getState().articles)

  const clearNew = () => {
    const current = useSceneStore.getState().document
    execute({
      type: 'batch',
      commands: current.elements.filter((e) => e.kind === 'point' && e.status === 'new').map((e) => ({ type: 'remove', id: e.id })),
    })
  }

  return (
    <div className="editor">
      <header className="editor-header">
        <Link to="/">Drömträdgård</Link>
        <span className="spacer" />
        {shaping ? (
          <>
            <span className="editor-hint">
              {toolMode === 'draw'
                ? 'Klicka ut hörnen. Avsluta genom att klicka på första punkten.'
                : 'Dra i punkterna. Dra i de små punkterna för att böja en kant. Dubbelklicka för att ta bort en punkt.'}
            </span>
            <button onClick={startDrawing} disabled={toolMode === 'draw'}>Rita om</button>
            <button onClick={finishShaping}>Klar</button>
          </>
        ) : (
          <>
            <button onClick={() => startShaping('plot')} disabled={!ready}>Forma tomt</button>
            <button onClick={() => startShaping('building')} disabled={!ready}>Forma hus</button>
          </>
        )}
        <button onClick={addStress} disabled={!ready}>Lägg till 300 växter</button>
        <button onClick={clearNew} disabled={!ready}>Rensa nya växter</button>
        <button onClick={undo} disabled={!ready || past.length === 0} title="Snabbtangent: Ctrl+Z">Ångra</button>
        <button onClick={redo} disabled={!ready || future.length === 0} title="Snabbtangent: Ctrl+Y">Gör om</button>
      </header>
      <CatalogPanel />
      <main className="editor-canvas">
        <SceneCanvas />
        <PricePanel />
        <SaveConflict />
        <DesignLoading load={load} />
      </main>
      <StatusBar />
    </div>
  )
}
