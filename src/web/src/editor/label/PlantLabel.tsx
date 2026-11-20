import { ArrowLeftRight, RotateCcw, RotateCw, Trash2, Undo2 } from 'lucide-react'
import { useId, useState } from 'react'
import type { CatalogArticle } from '../../api/catalog'
import type { SceneElement, SurfaceMaterial } from '../../api/scene'
import { Badge, Button, Card, IconButton } from '../../ui'
import { findArticle, useCatalogStore } from '../catalogStore'
import { replaceOptions } from '../panelGroups'
import { useSceneStore } from '../sceneStore'
import { keepSelected, removeSelected, replaceSelected, rotateSelected } from '../selectionCommands'
import { formatUnitPrice } from '../shelf/shelfCards'
import { labelFacts } from './labelFacts'

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

const STATUS = {
  new: <Badge tone="new">Ny i designen</Badge>,
  'existing-keep': <Badge tone="neutral">Befintlig</Badge>,
  'existing-remove': <Badge tone="cut">Tas bort</Badge>,
}

function Label({ element }: { element: SceneElement }) {
  const articles = useCatalogStore((s) => s.articles)
  const article = findArticle('articleId' in element ? element.articleId : null, articles)
  const options = optionsFor(element, article, articles)
  const [swapping, setSwapping] = useState(false)
  const headingId = useId()
  const name = selectionName(element, article)
  const removing = element.status === 'existing-remove'
  const facts = article && !removing ? labelFacts(article) : []

  return (
    <Card as="section" className="plant-label" aria-labelledby={headingId}>
      <span className="plant-label__hole" aria-hidden />
      <div className="plant-label__status">{STATUS[element.status]}</div>
      <h2 id={headingId} className="plant-label__name">
        {name}
      </h2>

      {facts.length > 0 && (
        <dl className="plant-label__facts">
          {facts.map((fact) => (
            <div key={fact.label} className="plant-label__fact">
              <dt>{fact.label}</dt>
              <span className="plant-label__leader" aria-hidden />
              <dd className="num">{fact.value}</dd>
            </div>
          ))}
        </dl>
      )}

      {article && !removing && (
        <p className="plant-label__price">
          <b className="num">{formatUnitPrice(article.price, article.unit)}</b>
          <span>inkl. moms</span>
        </p>
      )}

      {removing ? (
        <div className="plant-label__actions">
          <Button variant="primary" icon={Undo2} onClick={keepSelected}>
            Behåll
          </Button>
        </div>
      ) : (
        <div className="plant-label__actions">
          {element.kind === 'point' && (
            <>
              <IconButton icon={RotateCcw} label="Vrid åt vänster (Q)" onClick={() => rotateSelected(1)} />
              <IconButton icon={RotateCw} label="Vrid åt höger (E)" onClick={() => rotateSelected(-1)} />
            </>
          )}
          {options.length > 0 && (
            <Button variant="primary" icon={ArrowLeftRight} aria-expanded={swapping} onClick={() => setSwapping(!swapping)}>
              Byt
            </Button>
          )}
          {(element.kind === 'point' || element.kind === 'line' || element.kind === 'surface') && (
            <Button variant="danger" icon={Trash2} onClick={removeSelected} title="Snabbtangent: Delete">
              Ta bort
            </Button>
          )}
        </div>
      )}

      {swapping && !removing && options.length > 0 && (
        <div className="plant-label__swap">
          <h3>Byt mot</h3>
          <p className="plant-label__hint">
            {element.status === 'new' ? 'Plats och vridning behålls.' : 'Det befintliga tas bort och det nya hamnar på samma plats.'}
          </p>
          <ul>
            {options.map((option) => (
              <li key={option.id}>
                <span className="plant-label__swatch" style={{ background: option.color }} aria-hidden />
                <span className="plant-label__option">{option.name}</span>
                <span className="plant-label__option-price num">{formatUnitPrice(option.price, option.unit)}</span>
                <Button size="s" aria-label={`Byt till ${option.name}`} onClick={() => replaceSelected(option.id, option.material ?? undefined)}>
                  Byt
                </Button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </Card>
  )
}

/** The selected element as a nursery plant label, top right in the scene window; absent with nothing selected. */
export function PlantLabel() {
  const selectedId = useSceneStore((s) => s.selectedId)
  const element = useSceneStore((s) => s.document.elements.find((e) => e.id === s.selectedId))
  if (!element) return null
  // A new key per element, so "Byt mot" folds again when another element is chosen.
  return <Label key={selectedId} element={element} />
}

/** What the current tool does, as a quiet line low in the scene window when nothing is selected. */
export function ToolHint() {
  const tool = useSceneStore((s) => s.tool)
  const selected = useSceneStore((s) => s.selectedId !== null)
  if (selected) return null
  const text =
    tool.mode === 'place'
      ? 'Klicka i trädgården för att placera. Esc avslutar.'
      : tool.mode === 'line'
        ? tool.start
          ? 'Klicka där linjen ska sluta.'
          : 'Klicka där linjen ska börja.'
        : tool.mode === 'select'
          ? 'Klicka på ett element för att välja det, dra för att flytta.'
          : null
  return text ? <p className="tool-hint">{text}</p> : null
}
