import { Button } from '../ui'
import { keepMine, statusText, takeLatest, useSaveStore } from './saveStore'
import type { DesignLoad } from './useSavedDesign'

/** "Sparad", "Sparar…" or why it is not saved, in the status bar. */
export function SaveStatus() {
  const status = useSaveStore((s) => s.status)
  const text = statusText(status)
  if (!text || status === 'conflict') return null
  const problem = status === 'retrying' || status === 'invalid'
  return (
    <span className={problem ? 'save-status warning' : 'save-status'} role="status">
      {text}
    </span>
  )
}

/** Shown when another tab saved the design first. Nothing is saved until the customer chooses. */
export function SaveConflict() {
  const status = useSaveStore((s) => s.status)
  if (status !== 'conflict') return null
  return (
    <div className="save-conflict ui-card ui-card--elevated" role="alertdialog" aria-labelledby="save-conflict-text">
      <p id="save-conflict-text">{statusText('conflict')}</p>
      <div className="save-conflict__buttons">
        <Button variant="primary" onClick={() => void takeLatest()}>
          Hämta den senaste
        </Button>
        <Button variant="secondary" onClick={keepMine}>
          Behåll min
        </Button>
      </div>
    </div>
  )
}

/** Covers the scene until the customer's design has been fetched, so no change is made to the wrong one. */
export function DesignLoading({ load }: { load: DesignLoad }) {
  if (load === 'ready') return null
  return (
    <div className="design-loading" role="status">
      <p className="design-loading__card ui-card ui-card--elevated">
        <span className="design-loading__spinner" aria-hidden />
        {load === 'loading' ? 'Hämtar din design…' : 'Det gick inte att hämta din design. Försöker igen…'}</p>
    </div>
  )
}
