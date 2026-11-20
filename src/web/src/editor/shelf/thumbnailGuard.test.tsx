// @vitest-environment jsdom
import { cleanup, render } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { thuja } from '../testArticles'
import { ThumbnailGuard } from './ThumbnailStudio'
import { renderThumbnail, useThumbnailQueue } from './thumbnails'

afterEach(cleanup)

function Broken(): never {
  throw new Error('GLB 404')
}

describe('the thumbnail guard', () => {
  it('a model that fails to load rejects that picture and leaves the editor standing', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {})
    useThumbnailQueue.setState({ queue: [] })
    const picture = renderThumbnail(thuja)

    const { container } = render(
      <div>
        <p>editorn</p>
        <ThumbnailGuard>
          <Broken />
        </ThumbnailGuard>
      </div>,
    )

    await expect(picture).rejects.toThrow('GLB 404')
    expect(useThumbnailQueue.getState().queue).toHaveLength(0)
    expect(container.textContent).toContain('editorn')
  })
})
