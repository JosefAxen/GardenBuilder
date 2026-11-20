import { Link } from 'react-router'
import '../ui/ui.css'
import './start.css'

/** Customer flow. Each step gets its own route and component as it is built (see docs/design.md, "Kundens flöde"). */
const steps = ['Starta', 'Nuläge', 'Förslag', 'Egen design', 'Drömbild', 'Offertförfrågan']

export function CustomerFlowPage() {
  return (
    <main className="start">
      <div className="start__inner">
        <h1 className="start__mark">Drömträdgård</h1>
        <p className="start__lead">Från foto till offertförfrågan i sex steg.</p>
        <ol className="start__steps ui-card ui-card--elevated">
          {steps.map((step, index) => (
            <li key={step}>
              <span className="start__number num" aria-hidden>
                {index + 1}
              </span>
              {step}
            </li>
          ))}
        </ol>
        <nav className="start__links" aria-label="Gå vidare">
          <Link to="/editor" className="ui-button ui-button--accent ui-button--m">
            Öppna 3D-editorn
          </Link>
          <Link to="/admin" className="ui-button ui-button--secondary ui-button--m">
            Admin
          </Link>
        </nav>
      </div>
    </main>
  )
}
