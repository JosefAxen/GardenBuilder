import { useMemo } from 'react'
import { Badge } from '../../ui'
import { useCatalogStore } from '../catalogStore'
import { countMissingArticles } from '../catalogChecks'
import { lookOf } from '../existingLook'
import { useSceneStore } from '../sceneStore'
import { crowdingFootprints, findCrowdedPlants } from '../spacing'
import { DevMenu } from './DevMenu'

/** Counts and warnings about the design, along the bottom of the scene window. */
export function StatusLine({ ready }: { ready: boolean }) {
  const elements = useSceneStore((s) => s.document.elements)
  const notice = useSceneStore((s) => s.notice)
  const articles = useCatalogStore((s) => s.articles)
  const catalogReady = useCatalogStore((s) => s.status === 'ready')

  const crowdedCount = useMemo(() => {
    const plants = elements.flatMap((e) => {
      if (e.kind !== 'point') return []
      const article = lookOf(e, articles)
      return article?.geometry === 'point'
        ? [{ id: e.id, position: e.position, matureWidth: article.matureWidth, removed: e.status === 'existing-remove' }]
        : []
    })
    return findCrowdedPlants(crowdingFootprints(plants)).size
  }, [elements, articles])

  const placedCount = elements.filter((e) => e.kind === 'point' || e.kind === 'line').length
  const removedCount = elements.filter((e) => e.status === 'existing-remove').length
  // Only once the catalog has loaded; while loading, every article would look missing.
  const missingCount = useMemo(() => (catalogReady ? countMissingArticles(elements, articles) : 0), [catalogReady, elements, articles])

  return (
    <div className="status-line">
      <span className="num">{placedCount} element</span>
      {removedCount > 0 && (
        <span className="status-line__removed num">
          <i aria-hidden /> {removedCount} {removedCount === 1 ? 'markerat' : 'markerade'} för borttagning
        </span>
      )}
      {crowdedCount > 0 && <Badge tone="warn">{crowdedCount} växter står för tätt</Badge>}
      {missingCount > 0 && <Badge tone="warn">{missingCount} element saknas i katalogen och visas inte</Badge>}
      {notice && (
        <span role="status">
          <Badge tone="warn">{notice}</Badge>
        </span>
      )}
      <DevMenu ready={ready} />
    </div>
  )
}
