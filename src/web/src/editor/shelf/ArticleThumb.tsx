import { useEffect, useRef, useState } from 'react'
import type { CatalogArticle } from '../../api/catalog'
import { hasThumbnail, thumbnails } from './thumbnails'

/**
 * An article's picture: its colour first, then the rendered model once the card has scrolled into view. A picture that
 * cannot be drawn leaves the colour in place.
 */
export function ArticleThumb({ article }: { article: CatalogArticle }) {
  const root = useRef<HTMLSpanElement>(null)
  const [url, setUrl] = useState<string | null>(null)
  const drawable = hasThumbnail(article)

  useEffect(() => {
    const element = root.current
    if (!drawable || !element || typeof IntersectionObserver === 'undefined') return
    let cancelled = false
    const observer = new IntersectionObserver((entries) => {
      if (!entries.some((e) => e.isIntersecting)) return
      observer.disconnect()
      thumbnails.get(article).then(
        (picture) => !cancelled && setUrl(picture),
        () => {},
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
      ) : (
        <span className={`article-thumb__swatch article-thumb__swatch--${article.geometry}`} style={{ background: article.color }} />
      )}
    </span>
  )
}
