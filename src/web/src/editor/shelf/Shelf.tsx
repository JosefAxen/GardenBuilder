import { Images, Layers, LayoutGrid } from 'lucide-react'
import { useMemo, useRef } from 'react'
import { Button, Chip, Notice, SearchField, SegmentedControl } from '../../ui'
import { useCatalogStore } from '../catalogStore'
import { type EditorPanel, useEditorUiStore } from '../editorUiStore'
import { groupForPanel } from '../panelGroups'
import { ArticleCard } from './ArticleCard'
import { shelfCards } from './shelfCards'

const PANELS: { value: EditorPanel; label: string; icon: typeof LayoutGrid }[] = [
  { value: 'catalog', label: 'Katalog', icon: LayoutGrid },
  { value: 'photos', label: 'Foton', icon: Images },
  { value: 'proposals', label: 'Förslag', icon: Layers },
]

/**
 * The shelf along the bottom: the panel tabs, catalogue search and category chips on the left, the articles as a
 * row of cards on the right. Foton and Förslag open beside the tool rail; the catalogue stays on the shelf meanwhile.
 */
export function Shelf({ ready }: { ready: boolean }) {
  const panel = useEditorUiStore((s) => s.panel)
  const query = useEditorUiStore((s) => s.query)
  const category = useEditorUiStore((s) => s.category)
  const status = useCatalogStore((s) => s.status)
  const articles = useCatalogStore((s) => s.articles)
  const categories = useMemo(() => groupForPanel(articles), [articles])
  const cards = useMemo(() => shelfCards(categories, category, query), [categories, category, query])
  const { setPanel, setQuery, setCategory } = useEditorUiStore.getState()
  const row = useRef<HTMLUListElement>(null)

  return (
    <section className="editor-shelf" aria-label="Katalog och paneler">
      <div className="editor-shelf__side">
        <SegmentedControl variant="track" label="Panel bredvid verktygen" value={panel} options={PANELS} onChange={(p) => ready && setPanel(p)} />
        <>
            <SearchField label="Sök i katalogen" value={query} onChange={setQuery} placeholder={`Sök bland ${articles.length} artiklar`} />
            <div className="shelf-chips" role="group" aria-label="Kategori">
              <Chip selected={category === null} onClick={() => setCategory(null)}>
                Alla
              </Chip>
              {categories.map(({ category: name }) => (
                <Chip key={name} selected={category === name} onClick={() => setCategory(name)}>
                  {name}
                </Chip>
              ))}
            </div>
        </>
      </div>

      <div className="editor-shelf__content">
        {status === 'loading' ? (
          <p className="shelf-message">Laddar katalogen…</p>
        ) : status === 'error' ? (
          <div className="shelf-message">
            <Notice
              tone="error"
              action={
                <Button size="s" onClick={() => void useCatalogStore.getState().load()}>
                  Försök igen
                </Button>
              }
            >
              Katalogen kunde inte hämtas.
            </Notice>
          </div>
        ) : cards.length === 0 ? (
          <p className="shelf-message">{query.trim() ? `Inga artiklar matchar ”${query.trim()}”` : 'Inga artiklar i den här kategorin.'}</p>
        ) : (
          <ul
            ref={row}
            className="shelf-cards"
            aria-label="Artiklar"
            onWheel={(event) => {
              // A mouse wheel scrolls down; on a row that only scrolls sideways, turn that into sideways.
              if (row.current && Math.abs(event.deltaY) > Math.abs(event.deltaX)) row.current.scrollLeft += event.deltaY
            }}
          >
            {cards.map((card) => (
              <ArticleCard key={card.article.id} card={card} ready={ready} />
            ))}
          </ul>
        )}
      </div>
    </section>
  )
}
