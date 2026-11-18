import { useEffect, useRef } from 'react'
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
      <p className="editor-hint">Ta med det som stämmer. Du kan flytta och ändra allt efteråt.</p>
      {house && (
        <div className="photo-proposal">
          <span className="name">
            Husets färg och tak
            {house.wallColor && <span className="swatch" style={{ background: house.wallColor }} />}
            {house.roofColor && <span className="swatch" style={{ background: house.roofColor }} />}
          </span>
          <button onClick={takeHouse}>Ta med</button>
        </div>
      )}
      <ul>
        {proposals.map((p) => (
          <li key={p.element.id} className="photo-proposal">
            <span className="name">
              {p.label} <small>({confidenceText(p.confidence)})</small>
            </span>
            <button onClick={() => take(p.element.id)}>Ta med</button>
            <button onClick={() => drop(p.element.id)}>Ta bort</button>
          </li>
        ))}
      </ul>
      {proposals.length > 1 && <button onClick={takeAll}>Ta med alla</button>}
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
      <aside className="editor-panel">
        <h2>Foton</h2>
        <p className="editor-hint">Forma tomten och huset först, så sparas din trädgård. Sedan kan du ladda upp foton.</p>
      </aside>
    )
  }

  const allPinned = photos.length > 0 && photos.every((p) => p.camera)
  // The job reads the saved design, so a shape change still waiting to be saved must get there first.
  const saved = saveStatus === 'saved' || saveStatus === 'idle'
  const pinned = pinning && photos.find((p) => p.id === pinning.photoId)

  return (
    <aside className="editor-panel photo-panel">
      <h2>Foton</h2>
      <p className="editor-hint">
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
      <button onClick={() => input.current?.click()} disabled={busy || photos.length >= MAX_PHOTOS}>
        {busy ? 'Laddar upp …' : 'Ladda upp foton'}
      </button>
      {message && (
        <p className="warning" role="status">
          {message}
        </p>
      )}

      {pinned && (
        <div className="photo-pinning">
          <img src={pinned.url} alt="Fotot du visar var du tog" />
          <p className="editor-hint">
            {pinning.position ? 'Klicka nu åt det håll du fotade.' : 'Klicka där du stod när du tog fotot.'}
          </p>
        </div>
      )}

      <ol className="photo-list">
        {photos.map((photo, index) => (
          <li key={photo.id}>
            <img src={photo.url} alt={`Foto ${index + 1}`} />
            <div className="editor-row">
              <button onClick={() => startPin(photo.id)}>{photo.camera ? 'Ändra var du stod' : 'Visa var du stod'}</button>
              <button onClick={() => void removePhoto(photo.id)}>Ta bort</button>
            </div>
          </li>
        ))}
      </ol>

      {photos.length > 0 && (
        <>
          <button onClick={() => void interpretPhotos()} disabled={!allPinned || !saved || interpret === 'running'}>
            Tolka foton
          </button>
          {!allPinned && <p className="editor-hint">Visa var du stod för varje foto först.</p>}
          {interpret === 'running' && <p role="status">Tolkar dina foton …</p>}
        </>
      )}

      <Review />
    </aside>
  )
}
