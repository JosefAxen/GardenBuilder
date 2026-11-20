import { Camera, Check, MapPin, Sparkles, Trash2, Upload } from 'lucide-react'
import { useEffect, useRef } from 'react'
import { Badge, Button, Notice } from '../../ui'
import { useSaveStore } from '../saveStore'
import { useSceneStore } from '../sceneStore'
import { interpretPhotos, loadPhotos, removePhoto, startPin, uploadFiles, usePhotoStore } from './photoStore'
import { confidenceText, useReviewStore } from './reviewStore'

const ACCEPT = 'image/jpeg,image/png,image/webp,image/heic,image/heif'
const MAX_PHOTOS = 6

function Review() {
  const proposals = useReviewStore((s) => s.proposals)
  const house = useReviewStore((s) => s.house)
  const { take, drop, takeAll, takeHouse } = useReviewStore.getState()
  if (proposals.length === 0 && !house) return null

  return (
    <section className="photo-review">
      <h3>Det här såg vi på fotona</h3>
      <p className="panel-hint">Ta med det som stämmer. Du kan flytta och ändra allt efteråt.</p>
      <ul className="photo-proposals">
        {house && (
          <li className="photo-proposal">
            <span className="photo-proposal__name">
              Husets färg och tak
              <span className="photo-proposal__swatches">
                {house.wallColor && <span className="photo-proposal__swatch" style={{ background: house.wallColor }} />}
                {house.roofColor && <span className="photo-proposal__swatch" style={{ background: house.roofColor }} />}
              </span>
            </span>
            <Button size="s" variant="primary" icon={Check} onClick={takeHouse}>
              Ta med
            </Button>
          </li>
        )}
        {proposals.map((p) => (
          <li key={p.element.id} className="photo-proposal">
            <span className="photo-proposal__name">
              {p.label}
              <Badge tone="neutral">{confidenceText(p.confidence)}</Badge>
            </span>
            <Button size="s" variant="primary" aria-label={`Ta med ${p.label}`} onClick={() => take(p.element.id)}>
              Ta med
            </Button>
            <Button size="s" variant="quiet" aria-label={`Ta bort ${p.label}`} onClick={() => drop(p.element.id)}>
              Ta bort
            </Button>
          </li>
        ))}
      </ul>
      {proposals.length > 1 && (
        <Button variant="secondary" icon={Check} onClick={takeAll}>
          Ta med alla
        </Button>
      )}
    </section>
  )
}

/** "Foton": upload up to six photos, show where each was taken, and let the AI propose what the garden has today. */
export function PhotoPanel() {
  const gardenId = useSaveStore((s) => s.gardenId)
  const saveStatus = useSaveStore((s) => s.status)
  const { photos, busy, message, interpret } = usePhotoStore()
  const pinning = useSceneStore((s) => (s.tool.mode === 'pin' ? s.tool : null))
  const input = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (gardenId) void loadPhotos(gardenId)
  }, [gardenId])

  if (!gardenId) {
    return (
      <p className="panel-hint">Forma tomten och huset först, så sparas din trädgård. Sedan kan du ladda upp foton.</p>
    )
  }

  const allPinned = photos.length > 0 && photos.every((p) => p.camera)
  // The job reads the saved design, so a shape change still waiting to be saved must get there first.
  const saved = saveStatus === 'saved' || saveStatus === 'idle'
  const pinned = pinning && photos.find((p) => p.id === pinning.photoId)

  return (
    <div className="panel-body photo-panel">
      <p className="panel-hint">
        Ladda upp 1–6 foton av trädgården från olika håll. Undvik personer och registreringsskyltar.
      </p>
      <input
        ref={input}
        type="file"
        accept={ACCEPT}
        multiple
        hidden
        onChange={(event) => {
          const files = [...(event.target.files ?? [])].slice(0, MAX_PHOTOS - photos.length)
          event.target.value = ''
          void uploadFiles(files)
        }}
      />
      <Button variant="primary" icon={Upload} onClick={() => input.current?.click()} disabled={busy || photos.length >= MAX_PHOTOS}>
        {busy ? 'Laddar upp …' : 'Ladda upp foton'}
      </Button>
      {message && <Notice tone="warn">{message}</Notice>}

      {pinned && (
        <div className="photo-pinning">
          <img src={pinned.url} alt="Fotot du visar var du tog" />
          <p className="photo-pinning__step">
            <MapPin aria-hidden size={16} strokeWidth={2} />
            {pinning.position ? 'Klicka nu åt det håll du fotade.' : 'Klicka där du stod när du tog fotot.'}
          </p>
        </div>
      )}

      <ol className="photo-list">
        {photos.map((photo, index) => (
          <li key={photo.id} className="photo-card">
            <img src={photo.url} alt={`Foto ${index + 1}`} />
            {photo.camera && (
              <span className="photo-card__placed">
                <Badge tone="new">Plats visad</Badge>
              </span>
            )}
            <div className="photo-card__actions">
              <Button size="s" variant={photo.camera ? 'secondary' : 'primary'} icon={Camera} onClick={() => startPin(photo.id)}>
                {photo.camera ? 'Ändra var du stod' : 'Visa var du stod'}
              </Button>
              <Button size="s" variant="quiet" icon={Trash2} aria-label={`Ta bort foto ${index + 1}`} onClick={() => void removePhoto(photo.id)}>
                Ta bort
              </Button>
            </div>
          </li>
        ))}
      </ol>

      {photos.length > 0 && (
        <>
          <Button variant="accent" icon={Sparkles} onClick={() => void interpretPhotos()} disabled={!allPinned || !saved || interpret === 'running'}>
            Tolka foton
          </Button>
          {!allPinned && <p className="panel-hint">Visa var du stod för varje foto först.</p>}
          {interpret === 'running' && (
            <p className="panel-status" role="status">
              Tolkar dina foton …
            </p>
          )}
        </>
      )}

      <Review />
    </div>
  )
}
