import { useEffect, useMemo } from 'react'
import { Link } from 'react-router'
import type { CatalogArticle } from '../api/catalog'
import { findArticle, useCatalogStore } from './catalogStore'
import { stressElements } from './demoScene'
import { SceneCanvas } from './SceneCanvas'
import { useSceneStore } from './sceneStore'
import { findCrowdedPlants } from './spacing'
import './editor.css'

const ROTATE_STEP = Math.PI / 12

function rotateSelected(direction: 1 | -1) {
  const { document, selectedId, execute } = useSceneStore.getState()
  const element = document.elements.find((e) => e.id === selectedId)
  if (element?.kind !== 'point') return
  execute({ type: 'move', id: element.id, position: element.position, rotation: element.rotation + direction * ROTATE_STEP })
}

function removeSelected() {
  const { selectedId, execute, select, document } = useSceneStore.getState()
  const element = document.elements.find((e) => e.id === selectedId)
  // The plot and the house are not catalog items and cannot be removed here.
  if (!element || (element.kind !== 'point' && element.kind !== 'line')) return
  execute({ type: 'remove', id: element.id })
  select(null)
}

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

function CatalogPanel() {
  const tool = useSceneStore((s) => s.tool)
  const selectedId = useSceneStore((s) => s.selectedId)
  const elements = useSceneStore((s) => s.document.elements)
  const articles = useCatalogStore((s) => s.articles)
  const { setTool, execute, select } = useSceneStore.getState()

  const selected = elements.find((e) => e.id === selectedId)
  const selectedArticle = findArticle(selected && 'articleId' in selected ? selected.articleId : null)
  const categories = useMemo(() => {
    const groups = new Map<string, CatalogArticle[]>()
    for (const article of articles) groups.set(article.category, [...(groups.get(article.category) ?? []), article])
    return groups
  }, [articles])

  const startPlacing = (article: CatalogArticle) => {
    select(null)
    setTool(article.geometry === 'point' ? { mode: 'place', articleId: article.id } : { mode: 'line', articleId: article.id })
  }

  return (
    <aside className="editor-panel">
      {selected && selectedArticle ? (
        <section className="editor-selection">
          <h2>{selectedArticle.name}</h2>
          <div className="editor-row">
            {selected.kind === 'point' && (
              <>
                <button onClick={() => rotateSelected(1)} title="Snabbtangent: Q">⟲ Vrid</button>
                <button onClick={() => rotateSelected(-1)} title="Snabbtangent: E">⟳ Vrid</button>
              </>
            )}
            {selected.kind !== 'surface' && (
              <button onClick={removeSelected} title="Snabbtangent: Delete">Ta bort</button>
            )}
          </div>
          <p className="editor-hint">Välj "Byt" på en artikel nedan för att byta ut den här. Plats och vridning behålls.</p>
        </section>
      ) : (
        <p className="editor-hint">
          {tool.mode === 'place' && 'Klicka i trädgården för att placera. Esc avslutar.'}
          {tool.mode === 'line' && (tool.start ? 'Klicka där linjen ska sluta.' : 'Klicka där linjen ska börja.')}
          {tool.mode === 'select' && 'Klicka på ett element för att välja det, dra för att flytta.'}
        </p>
      )}

      {[...categories].map(([category, items]) => (
        <section key={category}>
          <h3>{category}</h3>
          <ul>
            {items.map((article) => {
              const active = tool.mode !== 'select' && tool.articleId === article.id
              const canReplace =
                selected !== undefined && selectedArticle !== undefined && selectedArticle.geometry === article.geometry && selectedArticle.id !== article.id
              return (
                <li key={article.id} className={active ? 'active' : undefined}>
                  <span className="swatch" style={{ background: article.color }} />
                  <span className="name">{article.name}</span>
                  {canReplace && (
                    <button onClick={() => execute({ type: 'replace', id: selected.id, articleId: article.id })}>Byt</button>
                  )}
                  {/* Surfaces need a polygon tool, which does not exist yet; they can only be swapped. */}
                  {article.geometry !== 'surface' && (
                    <button onClick={() => (active ? setTool({ mode: 'select' }) : startPlacing(article))}>{active ? 'Klar' : 'Lägg till'}</button>
                  )}
                </li>
              )
            })}
          </ul>
        </section>
      ))}
    </aside>
  )
}

function StatusBar() {
  const perf = useSceneStore((s) => s.perf)
  const elements = useSceneStore((s) => s.document.elements)
  const detail = useSceneStore((s) => s.detail)
  const notice = useSceneStore((s) => s.notice)
  const articles = useCatalogStore((s) => s.articles)
  const { setDetail } = useSceneStore.getState()

  const crowdedCount = useMemo(() => {
    const plants = elements.flatMap((e) => {
      if (e.kind !== 'point') return []
      const article = findArticle(e.articleId, articles)
      return article?.geometry === 'point' && article.matureWidth ? [{ id: e.id, position: e.position, matureWidth: article.matureWidth }] : []
    })
    return findCrowdedPlants(plants).size
  }, [elements, articles])

  const placedCount = elements.filter((e) => e.kind === 'point' || e.kind === 'line').length
  const format = new Intl.NumberFormat('sv-SE')

  return (
    <div className="editor-status">
      <span>{placedCount} element</span>
      {crowdedCount > 0 && <span className="warning">{crowdedCount} växter står för tätt</span>}
      {notice && (
        <span className="warning" role="status">
          {notice}
        </span>
      )}
      <span className="spacer" />
      {perf && (
        <span className="perf">
          {perf.fps} fps · {perf.drawCalls} ritanrop · {format.format(perf.triangles)} trianglar
        </span>
      )}
      <label>
        <input type="checkbox" checked={detail === 'high'} onChange={(e) => setDetail(e.target.checked ? 'high' : 'low')} /> Detaljerade modeller
      </label>
    </div>
  )
}

export function EditorPage() {
  const past = useSceneStore((s) => s.past)
  const future = useSceneStore((s) => s.future)
  const { undo, redo, execute } = useSceneStore.getState()
  useKeyboardShortcuts()

  // Fetch the catalog once when the editor opens.
  useEffect(() => {
    void useCatalogStore.getState().load()
  }, [])

  const addStress = () => {
    const plants = useCatalogStore.getState().articles.filter((a) => a.geometry === 'point' && a.stockStatus === 'in-stock')
    if (plants.length === 0) return
    const seed = useSceneStore.getState().document.elements.length
    execute({ type: 'batch', commands: stressElements(300, plants, seed).map((element) => ({ type: 'add', element })) })
  }

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
        <button onClick={addStress}>Lägg till 300 växter</button>
        <button onClick={clearNew}>Rensa nya växter</button>
        <button onClick={undo} disabled={past.length === 0} title="Snabbtangent: Ctrl+Z">Ångra</button>
        <button onClick={redo} disabled={future.length === 0} title="Snabbtangent: Ctrl+Y">Gör om</button>
      </header>
      <CatalogPanel />
      <main className="editor-canvas">
        <SceneCanvas />
      </main>
      <StatusBar />
    </div>
  )
}
