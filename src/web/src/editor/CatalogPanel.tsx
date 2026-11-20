import type { CatalogArticle } from '../api/catalog'
import type { SceneElement, SurfaceMaterial } from '../api/scene'
import { replaceOptions } from './panelGroups'
import { findArticle, useCatalogStore } from './catalogStore'
import { useSceneStore } from './sceneStore'
import { keepSelected, removeSelected, replaceSelected, rotateSelected } from './selectionCommands'

const MATERIAL_NAMES: Record<SurfaceMaterial, string> = {
  grass: 'gräs',
  paving: 'plattor',
  gravel: 'grus',
  decking: 'trädäck',
  bed: 'rabatt',
}

/** The name to show for a selected element, also when it has no article (e.g. an existing lawn). */
function selectionName(element: SceneElement, article: CatalogArticle | undefined): string {
  if (article) return article.name
  if (element.kind === 'surface') return `Befintlig yta (${MATERIAL_NAMES[element.material]})`
  return 'Befintligt element'
}

/** What the selected element can be swapped for. An existing surface without an article can become any surface article. */
function optionsFor(element: SceneElement, article: CatalogArticle | undefined, articles: CatalogArticle[]): CatalogArticle[] {
  if (article) return replaceOptions(article, articles)
  if (element.kind === 'surface') return articles.filter((a) => a.geometry === 'surface' && a.stockStatus === 'in-stock')
  return []
}

function Selection({ element }: { element: SceneElement }) {
  const articles = useCatalogStore((s) => s.articles)
  const article = findArticle('articleId' in element ? element.articleId : null, articles)
  const options = optionsFor(element, article, articles)

  // An existing element marked for removal can only be kept again: there is nothing to move, turn or swap.
  if (element.status === 'existing-remove') {
    return (
      <section className="editor-selection">
        <h2>{selectionName(element, article)} (tas bort)</h2>
        <div className="editor-row">
          <button onClick={keepSelected}>Behåll</button>
        </div>
      </section>
    )
  }

  return (
    <section className="editor-selection">
      <h2>{selectionName(element, article)}</h2>
      <div className="editor-row">
        {element.kind === 'point' && (
          <>
            <button onClick={() => rotateSelected(1)} title="Snabbtangent: Q">⟲ Vrid</button>
            <button onClick={() => rotateSelected(-1)} title="Snabbtangent: E">⟳ Vrid</button>
          </>
        )}
        {(element.kind === 'point' || element.kind === 'line' || element.kind === 'surface') && (
          <button onClick={removeSelected} title="Snabbtangent: Delete">Ta bort</button>
        )}
      </div>
      {options.length > 0 && (
        <>
          <h3>Byt mot</h3>
          <p className="editor-hint">
            {element.status === 'new' ? 'Plats och vridning behålls.' : 'Det befintliga tas bort och det nya hamnar på samma plats.'}
          </p>
          <ul>
            {options.map((option) => (
              <li key={option.id}>
                <span className="swatch" style={{ background: option.color }} />
                <span className="name">{option.name}</span>
                <button onClick={() => replaceSelected(option.id, option.material ?? undefined)}>Byt</button>
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  )
}

/**
 * The selected element and the tool hint, top right in the scene window. The article list moved to the shelf; this
 * becomes the plant label in the next step.
 */
export function CatalogPanel() {
  const tool = useSceneStore((s) => s.tool)
  const selectedId = useSceneStore((s) => s.selectedId)
  const elements = useSceneStore((s) => s.document.elements)
  const selected = elements.find((e) => e.id === selectedId)

  return (
    <aside className="editor-panel selection-panel">
      {selected ? (
        <Selection element={selected} />
      ) : (
        <p className="editor-hint">
          {tool.mode === 'place' && 'Klicka i trädgården för att placera. Esc avslutar.'}
          {tool.mode === 'line' && (tool.start ? 'Klicka där linjen ska sluta.' : 'Klicka där linjen ska börja.')}
          {tool.mode === 'select' && 'Klicka på ett element för att välja det, dra för att flytta.'}
        </p>
      )}
    </aside>
  )
}
