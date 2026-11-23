import { Download, Eye, Save, Sparkles, Trash2, X } from 'lucide-react'
import { type KeyboardEvent as ReactKeyboardEvent, useEffect, useRef, useState } from 'react'
import type { DreamImage } from '../../api/dreams'
import type { SceneView } from '../../api/scene'
import { Badge, Button, Field, IconButton, Notice } from '../../ui'
import { useSaveStore } from '../saveStore'
import { useSceneStore } from '../sceneStore'
import { createDream, FAILED, loadDreams, QUOTA, remaining, removeDream, setSendPhotos, useDreamStore } from './dreamStore'
import { currentView, renderView, showView } from './viewCapture'
import { viewProblem } from './viewRules'
import './dream.css'

/** One shared empty list: a new [] on every read would look like a change to the store and render forever. */
const NO_VIEWS: SceneView[] = []
const QUOTA_REACHED = 'Du har skapat fem drömbilder för den här trädgården.'
const NOT_3D = 'Byt till 3D-läget för att spara en vy.'
const date = new Intl.DateTimeFormat('sv-SE', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })

/** "Drömbild": saved camera views, a photo-like image made from one of them, and the garden's gallery. */
export function DreamPanel() {
  const gardenId = useSaveStore((s) => s.gardenId)
  const saveStatus = useSaveStore((s) => s.status)
  const revision = useSaveStore((s) => s.revision)
  const views = useSceneStore((s) => s.document.views ?? NO_VIEWS)
  const { images, state, message, sendPhotos } = useDreamStore()
  const [open, setOpen] = useState<DreamImage | null>(null)

  useEffect(() => {
    if (gardenId) void loadDreams(gardenId)
  }, [gardenId])

  if (!gardenId) {
    return <p className="panel-hint">Forma tomten och huset först, så sparas din trädgård. Sedan kan du skapa drömbilder.</p>
  }

  const left = remaining(images)
  const saved = saveStatus === 'saved' || saveStatus === 'idle'
  const working = state === 'working'

  const create = async (view: SceneView) => {
    try {
      await createDream(view, await renderView(view))
    } catch {
      useDreamStore.setState({ state: 'failed', message: FAILED })
    }
  }

  return (
    <div className="panel-body dream-panel">
      <p className="panel-hint">
        Spara en vy av din trädgård, så gör vi en fotolik bild av den. Bilden är en inspiration; växterna och måtten är de du
        har ritat.
      </p>

      <section className="dream-section" aria-labelledby="dream-views-title">
        <h3 id="dream-views-title" className="dream-section__title">
          Dina vyer
        </h3>
        {views.length === 0 ? (
          <p className="panel-hint">Ställ in kameran i 3D så som du vill se trädgården, ge vyn ett namn och spara den.</p>
        ) : (
          <ul className="dream-views">
            {views.map((view) => (
              <li key={view.id} className="dream-view">
                <span className="dream-view__name">{view.name}</span>
                <div className="dream-view__actions">
                  <IconButton icon={Eye} label={`Visa ${view.name}`} onClick={() => showSaved(view)} />
                  <IconButton
                    icon={Trash2}
                    label={`Ta bort ${view.name}`}
                    onClick={() => useSceneStore.getState().execute({ type: 'remove-view', id: view.id })}
                  />
                  <Button size="s" variant="accent" icon={Sparkles} disabled={working || left === 0 || !saved} onClick={() => void create(view)}>
                    Skapa drömbild
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        )}
        <SaveViewForm />
      </section>

      <label className="dream-photos">
        <input type="checkbox" checked={sendPhotos} onChange={(e) => setSendPhotos(e.target.checked)} />
        <span>
          <span className="dream-photos__label">Skicka med mina foton (blir mer likt din trädgård)</span>
          <span className="dream-photos__hint">
            {sendPhotos
              ? 'Upp till två av dina foton, tagna åt samma håll som vyn, skickas med till bildtjänsten.'
              : 'Bara 3D-vyn skickas till bildtjänsten.'}
          </span>
        </span>
      </label>

      <p className="dream-quota num">
        {left} av {QUOTA} drömbilder kvar
      </p>
      {left === 0 && <p className="panel-hint">{QUOTA_REACHED}</p>}
      {working && (
        <p className="panel-status" role="status">
          Skapar drömbild … det tar ungefär en minut
        </p>
      )}
      {message && state === 'failed' && <Notice tone="warn">{message}</Notice>}

      {images.length > 0 && (
        <section className="dream-section" aria-labelledby="dream-gallery-title">
          <h3 id="dream-gallery-title" className="dream-section__title">
            Dina drömbilder
          </h3>
          <ul className="dream-gallery">
            {images.map((image) => (
              <li key={image.id}>
                <DreamCard image={image} stale={image.revision < revision} onOpen={() => setOpen(image)} />
              </li>
            ))}
          </ul>
        </section>
      )}

      {open && <DreamOverlay image={open} onClose={() => setOpen(null)} />}
    </div>
  )
}

/** "Visa": leaves the top view first, since a saved view is always a 3D camera. */
function showSaved(view: SceneView) {
  const scene = useSceneStore.getState()
  if (scene.view === 'top') {
    scene.setView('perspective')
    requestAnimationFrame(() => showView(view))
  } else showView(view)
}

function SaveViewForm() {
  const [name, setName] = useState('')
  const [problem, setProblem] = useState<string | null>(null)

  const save = () => {
    const view = currentView(name.trim())
    if (!view) {
      setProblem(NOT_3D)
      return
    }
    const why = viewProblem(view, useSceneStore.getState().document)
    if (why) {
      setProblem(why)
      return
    }
    useSceneStore.getState().execute({ type: 'add-view', view })
    setName('')
    setProblem(null)
  }

  return (
    <form
      className="dream-save"
      onSubmit={(e) => {
        e.preventDefault()
        save()
      }}
    >
      <Field label="Namn på vyn" error={problem ?? undefined}>
        {(field) => (
          <input
            {...field}
            value={name}
            maxLength={40}
            placeholder="Till exempel Från altanen"
            onChange={(e) => {
              setName(e.target.value)
              setProblem(null)
            }}
          />
        )}
      </Field>
      <Button type="submit" variant="secondary" icon={Save}>
        Spara den här vyn
      </Button>
    </form>
  )
}

function DreamCard({ image, stale, onOpen }: { image: DreamImage; stale: boolean; onOpen: () => void }) {
  const failed = image.status === 'failed'
  const pending = image.status === 'queued' || image.status === 'running'
  const label = `${image.viewName}, ${date.format(new Date(image.createdAt))}`

  return (
    <article className={failed ? 'dream-card is-failed' : 'dream-card'} aria-label={label}>
      {image.status === 'succeeded' && image.imageUrl ? (
        <button type="button" className="dream-card__picture" onClick={onOpen} aria-label={`Visa ${image.viewName} större`}>
          <img src={image.imageUrl} alt={`Drömbild ${image.viewName}`} />
        </button>
      ) : (
        <div className="dream-card__picture">
          {pending ? <div className="dream-card__waiting" aria-hidden /> : <img src={image.renderUrl} alt={`3D-vyn ${image.viewName}`} />}
        </div>
      )}
      <div className="dream-card__body">
        <div className="dream-card__badges">
          {image.status === 'succeeded' && <Badge tone="new">Inspirationsbild</Badge>}
          {image.test && <Badge tone="warn">Testbild</Badge>}
          {pending && <Badge tone="neutral">Skapas …</Badge>}
        </div>
        <p className="dream-card__title">
          {image.viewName}
          <span className="dream-card__date num">{date.format(new Date(image.createdAt))}</span>
        </p>
        {stale && !failed && <p className="dream-card__note">Designen har ändrats sedan bilden skapades</p>}
        {failed && (
          <>
            <p className="dream-card__note">{FAILED}</p>
            <Button size="s" variant="danger" icon={Trash2} onClick={() => void removeDream(image.id)}>
              Ta bort
            </Button>
          </>
        )}
      </div>
    </article>
  )
}

/** The image large. Esc and "Stäng" close it; Esc stops here, so the scene keeps its selection. */
function DreamOverlay({ image, onClose }: { image: DreamImage; onClose: () => void }) {
  const dialog = useRef<HTMLDivElement>(null)
  const opener = useRef<Element | null>(document.activeElement)

  useEffect(() => {
    dialog.current?.querySelector<HTMLButtonElement>('.dream-overlay__close')?.focus()
    const back = opener.current
    return () => {
      if (back instanceof HTMLElement) back.focus()
    }
  }, [])

  const onKeyDown = (event: ReactKeyboardEvent) => {
    if (event.key !== 'Escape') return
    event.stopPropagation()
    event.nativeEvent.stopImmediatePropagation()
    onClose()
  }

  return (
    <div ref={dialog} className="dream-overlay" role="dialog" aria-modal="true" aria-label={image.viewName} onKeyDown={onKeyDown}>
      <div className="dream-overlay__frame">
        <img src={image.imageUrl ?? image.renderUrl} alt={`Drömbild ${image.viewName}`} />
        <div className="dream-overlay__bar">
          <div className="dream-overlay__text">
            <Badge tone="new">Inspirationsbild</Badge>
            <p>Offerten gäller designen, inte bilden.</p>
          </div>
          <div className="dream-overlay__actions">
            {image.downloadUrl && (
              <a className="ui-button ui-button--secondary ui-button--s" href={image.downloadUrl} download="drombild.jpg">
                <Download aria-hidden size={16} strokeWidth={1.9} />
                Ladda ner
              </a>
            )}
            <Button className="dream-overlay__close" size="s" variant="primary" icon={X} onClick={onClose}>
              Stäng
            </Button>
          </div>
        </div>
      </div>
    </div>
  )
}

