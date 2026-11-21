import { useEffect, useRef, useState } from 'react'
import type { CatalogArticle } from '../../api/catalog'
import { hasThumbnail, thumbnails } from './thumbnails'

/**
 * An article's picture: a quiet frame first, then the rendered model once the card has scrolled into view. Articles
 * with no model, and pictures that cannot be drawn, show their colour instead.
 */
export function ArticleThumb({ article }: { article: CatalogArticle }) {
  const root = useRef<HTMLSpanElement>(null)
  const [url, setUrl] = useState<string | null>(null)
  const [failed, setFailed] = useState(false)
  // Without IntersectionObserver there is no lazy drawing, so the colour sample stays.
  const drawable = hasThumbnail(article) && typeof IntersectionObserver !== 'undefined'

  useEffect(() => {
    const element = root.current
    if (!drawable || !element) return
    let cancelled = false
    const observer = new IntersectionObserver((entries) => {
      if (!entries.some((e) => e.isIntersecting)) return
      observer.disconnect()
      thumbnails.get(article).then(
        (picture) => !cancelled && setUrl(picture),
        () => !cancelled && setFailed(true),
      )
    })
    observer.observe(element)
    return () => {
      cancelled = true
      observer.disconnect()
    }
  }, [article, drawable])

  return (
    <span ref={root} className="article-thumb" aria-hidden>
      {url ? (
        <img src={url} alt="" draggable={false} />
      ) : drawable && !failed ? (
        // A quiet frame while the picture is drawn, so a fresh visit does not show a row of identical colour discs.
        <span className="article-thumb__waiting" />
      ) : (
        <span className={`article-thumb__swatch article-thumb__swatch--${article.geometry}`} style={{ background: article.color }} />
      )}
    </span>
  )
}
