import { Link } from 'react-router'
import '../ui/ui.css'
import './start.css'

/** Customer flow. Each step gets its own route and component as it is built (see docs/design.md, "Kundens flöde"). */
const steps: [string, string][] = [
  ['Starta', 'Ladda upp foton och ange adress.'],
  ['Nuläge', 'Trädgården byggs upp i 3D och du rättar det som blev fel.'],
  ['Förslag', 'Två förslag på hela trädgården ur lagret.'],
  ['Egen design', 'Byt, flytta och lägg till, med priset som ett spann.'],
  ['Drömbild', 'En bild av designen från en vinkel du väljer.'],
  ['Offertförfrågan', 'Skicka designen, så hör en säljare av sig.'],
]
/** The step the editor opens at today, until the customer flow is built (delprojekt 12). */
const EDITOR_STEP = 3

export function CustomerFlowPage() {
  return (
    <main className="start">
      <div className="start__inner">
        <h1 className="start__mark">Drömträdgård</h1>
        <p className="start__lead">Från foto till offertförfrågan i sex steg.</p>
        <ol className="start__steps ui-card ui-card--elevated">
          {steps.map(([step, what], index) => (
            <li key={step} className={index === EDITOR_STEP ? 'is-current' : undefined}>
              <span className="start__number num" aria-hidden>
                {index + 1}
              </span>
              <span className="start__step">
                <span className="start__name">{step}</span>
                <span className="start__what">{what}</span>
                {index === EDITOR_STEP && <span className="start__now">Här börjar editorn i dag</span>}
              </span>
            </li>
          ))}
        </ol>
        <Link to="/editor" className="ui-button ui-button--accent ui-button--m start__open">
          Öppna 3D-editorn
        </Link>
        <Link to="/admin" className="start__staff">
          För personal
        </Link>
      </div>
    </main>
  )
}
