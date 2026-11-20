import { Check, Plus } from 'lucide-react'
import type { CatalogArticle } from '../../api/catalog'
import { Badge } from '../../ui'
import { useSceneStore } from '../sceneStore'
import type { ShelfCard } from './shelfCards'
import { formatUnitPrice } from './shelfCards'

function startPlacing(article: CatalogArticle) {
  const { setTool, select } = useSceneStore.getState()
  select(null)
  setTool(article.geometry === 'point' ? { mode: 'place', articleId: article.id } : { mode: 'line', articleId: article.id })
}

/** The picture slot: the article's colour until a rendered thumbnail exists (Task 7). */
function Thumb({ article }: { article: CatalogArticle }) {
  return (
    <span className="article-card__thumb" aria-hidden>
      <span className="article-card__swatch" style={{ background: article.color }} />
    </span>
  )
}

/** One article on the shelf: picture, name, size, price and "Lägg till" (or "Klar" while it is being placed). */
export function ArticleCard({ card, ready }: { card: ShelfCard; ready: boolean }) {
  const { article, title, detail } = card
  const tool = useSceneStore((s) => s.tool)
  const placing = (tool.mode === 'place' || tool.mode === 'line') && tool.articleId === article.id
  const outOfStock = article.stockStatus === 'out-of-stock'
  const surface = article.geometry === 'surface'
  const name = detail ? `${title} ${detail}` : title

  return (
    <li className={['article-card', placing && 'is-placing', outOfStock && 'is-out'].filter(Boolean).join(' ')}>
      <div className="article-card__media">
        <Thumb article={article} />
        {outOfStock ? (
          <span className="article-card__badge">
            <Badge tone="neutral">Slut i lager</Badge>
          </span>
        ) : (
          !surface &&
          (placing ? (
            <button
              type="button"
              className="article-card__add is-done"
              aria-label={`Klar med ${name}`}
              title="Klar"
              onClick={() => useSceneStore.getState().setTool({ mode: 'select' })}
            >
              <Check aria-hidden size={16} strokeWidth={2.2} />
            </button>
          ) : (
            <button
              type="button"
              className="article-card__add"
              aria-label={`Lägg till ${name}`}
              title="Lägg till"
              disabled={!ready}
              onClick={() => startPlacing(article)}
            >
              <Plus aria-hidden size={16} strokeWidth={2.2} />
            </button>
          ))
        )}
        {placing && <span className="article-card__placing">Klicka i trädgården</span>}
      </div>
      <p className="article-card__title">{title}</p>
      <p className="article-card__detail">{detail ?? (surface ? 'Välj en yta i trädgården för att byta' : ' ')}</p>
      <p className="article-card__price num">{formatUnitPrice(article.price, article.unit)}</p>
    </li>
  )
}
