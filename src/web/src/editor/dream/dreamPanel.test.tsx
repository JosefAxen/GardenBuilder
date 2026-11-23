// @vitest-environment jsdom
import { cleanup, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { PerspectiveCamera, Vector3 } from 'three'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { DreamImage } from '../../api/dreams'
import type { SceneDocument, SceneView } from '../../api/scene'
import { useKeyboardShortcuts } from '../keyboard'
import { useSaveStore } from '../saveStore'
import { useSceneStore } from '../sceneStore'
import { DreamPanel } from './DreamPanel'
import { FAILED, useDreamStore } from './dreamStore'
import { registerBridge } from './viewCapture'

const plot = {
  kind: 'plot' as const,
  id: 'plot',
  status: 'existing-keep' as const,
  measurementSource: 'customer' as const,
  sloped: false,
  points: [{ x: 0, y: 0 }, { x: 20, y: 0 }, { x: 20, y: 20 }, { x: 0, y: 20 }],
}
const tree = { kind: 'point' as const, id: 'tree', status: 'new' as const, measurementSource: 'estimated' as const, articleId: 'thuja', position: { x: 5, y: 5 }, rotation: 0 }
const view: SceneView = { id: 'v1', name: 'Från gatan', eye: { x: 10, y: -5, height: 1.7 }, target: { x: 10, y: 10, height: 1 }, fieldOfView: 50 }

function image(id: string, status: DreamImage['status'], extra: Partial<DreamImage> = {}): DreamImage {
  return {
    id,
    status,
    viewName: 'Från gatan',
    revision: 2,
    test: false,
    createdAt: '2026-11-22T10:00:00Z',
    imageUrl: status === 'succeeded' ? `https://blob/${id}/image.jpg` : null,
    downloadUrl: status === 'succeeded' ? `https://blob/${id}/image.jpg?rscd=attachment` : null,
    renderUrl: `https://blob/${id}/render.jpg`,
    failure: status === 'failed' ? 'Bildtjänsten avböjde.' : null,
    ...extra,
  }
}

/** The panel with the editor's keyboard shortcuts, as on the page. */
function Editor() {
  useKeyboardShortcuts()
  return <DreamPanel />
}

function show(views: SceneView[], images: DreamImage[]) {
  useSceneStore.getState().load({ formatVersion: 3, elements: [plot, tree], views } as SceneDocument)
  vi.stubGlobal('fetch', vi.fn(async (url: string) => {
    const one = images.find((i) => url.endsWith(`/${i.id}`))
    return new Response(JSON.stringify(one ?? images), { status: 200 })
  }))
  render(<Editor />)
}

beforeEach(() => {
  useSaveStore.setState({ gardenId: 'g1', designId: 'd1', status: 'saved', revision: 2 })
  useDreamStore.setState({ gardenId: null, images: [], state: 'idle', message: null, sendPhotos: true })
  const camera = new PerspectiveCamera(50)
  camera.position.set(10, 1.7, 5)
  registerBridge({ gl: {} as never, scene: {} as never, camera, controls: { target: new Vector3(10, 1, -10), update: () => {} }, invalidate: () => {} })
})

afterEach(() => {
  cleanup()
  registerBridge(null)
  vi.unstubAllGlobals()
})

describe('the Drömbild panel', () => {
  it('explains how to save a view when there is none', async () => {
    show([], [])

    expect(await screen.findByText(/Ställ in kameran i 3D/)).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Skapa drömbild' })).toBeNull()
  })

  it('opens on a design saved before views existed (no views field)', async () => {
    useSceneStore.getState().load({ formatVersion: 2, elements: [plot, tree] } as SceneDocument)
    vi.stubGlobal('fetch', vi.fn(async () => new Response('[]', { status: 200 })))

    render(<Editor />)

    expect(await screen.findByText(/Ställ in kameran i 3D/)).toBeTruthy()
  })

  it('a blank name is refused with the rule', async () => {
    show([], [])

    await userEvent.click(await screen.findByRole('button', { name: 'Spara den här vyn' }))

    expect(screen.getByText('Ge vyn ett namn.')).toBeTruthy()
    expect(useSceneStore.getState().document.views).toEqual([])
  })

  it('a named view is saved as one undoable step', async () => {
    show([], [])

    await userEvent.type(await screen.findByLabelText('Namn på vyn'), 'Från altanen')
    await userEvent.click(screen.getByRole('button', { name: 'Spara den här vyn' }))

    expect(useSceneStore.getState().document.views?.map((v) => v.name)).toEqual(['Från altanen'])
    useSceneStore.getState().undo()
    expect(useSceneStore.getState().document.views).toEqual([])
  })

  it('with no images left the button is off and says why', async () => {
    show([view], [1, 2, 3, 4, 5].map((n) => image(`i${n}`, 'succeeded')))

    expect(await screen.findByText('0 av 5 drömbilder kvar')).toBeTruthy()
    const button = screen.getByRole('button', { name: 'Skapa drömbild' }) as HTMLButtonElement
    expect(button.disabled).toBe(true)
    expect(screen.getByText('Du har skapat fem drömbilder för den här trädgården.')).toBeTruthy()
  })

  it('a failed image shows the 3D view, the explanation and Ta bort', async () => {
    show([view], [image('bad', 'failed')])

    const card = await screen.findByRole('article', { name: /Från gatan/ })
    expect((within(card).getByRole('img') as HTMLImageElement).src).toBe('https://blob/bad/render.jpg')
    expect(within(card).getByText(FAILED)).toBeTruthy()
    expect(within(card).getByRole('button', { name: 'Ta bort' })).toBeTruthy()
    expect(screen.getByText('5 av 5 drömbilder kvar')).toBeTruthy()
  })

  it('a finished test image is marked Inspirationsbild and Testbild', async () => {
    show([view], [image('ok', 'succeeded', { test: true })])

    const card = await screen.findByRole('article', { name: /Från gatan/ })
    expect(within(card).getByText('Inspirationsbild')).toBeTruthy()
    expect(within(card).getByText('Testbild')).toBeTruthy()
  })

  it('an image from an older design says the design has changed', async () => {
    useSaveStore.setState({ revision: 3 })
    show([view], [image('old', 'succeeded')])

    const card = await screen.findByRole('article', { name: /Från gatan/ })
    expect(within(card).getByText('Designen har ändrats sedan bilden skapades')).toBeTruthy()
  })

  it('Esc closes the enlarged image without clearing the scene selection', async () => {
    show([view], [image('ok', 'succeeded')])
    useSceneStore.getState().select('tree')

    await userEvent.click(await screen.findByRole('button', { name: /Visa Från gatan större/ }))
    const dialog = screen.getByRole('dialog')
    expect(within(dialog).getByText('Offerten gäller designen, inte bilden.')).toBeTruthy()
    expect(within(dialog).getByRole('link', { name: 'Ladda ner' }).getAttribute('href')).toBe('https://blob/ok/image.jpg?rscd=attachment')

    await userEvent.keyboard('{Escape}')

    expect(screen.queryByRole('dialog')).toBeNull()
    expect(useSceneStore.getState().selectedId).toBe('tree')
  })

  it('opening an image fetches fresh links, so Ladda ner works after ten minutes', async () => {
    useSceneStore.getState().load({ formatVersion: 3, elements: [plot, tree], views: [view] } as SceneDocument)
    const stale = image('ok', 'succeeded')
    const fresh = { ...stale, downloadUrl: 'https://blob/ok/image.jpg?sig=new&rscd=attachment' }
    vi.stubGlobal('fetch', vi.fn(async (url: string) =>
      new Response(JSON.stringify(url.endsWith('/ok') ? fresh : [stale]), { status: 200 })))
    render(<Editor />)

    await userEvent.click(await screen.findByRole('button', { name: /Visa Från gatan större/ }))

    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByRole('link', { name: 'Ladda ner' }).getAttribute('href')).toBe(fresh.downloadUrl)
  })
})
