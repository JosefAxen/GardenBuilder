import { Link } from 'react-router'

/** Customer flow. Each step gets its own route and component as it is built (see docs/design.md, "Kundens flöde"). */
const steps = ['Starta', 'Nuläge', 'Förslag', 'Egen design', 'Drömbild', 'Offertförfrågan']

export function CustomerFlowPage() {
  return (
    <main>
      <h1>Drömträdgård</h1>
      <p>Från foto till offertförfrågan i sex steg.</p>
      <ol>
        {steps.map((step) => (
          <li key={step}>{step}</li>
        ))}
      </ol>
      <p>
        <Link to="/editor">Öppna 3D-editorn</Link> · <Link to="/admin">Admin</Link>
      </p>
    </main>
  )
}
