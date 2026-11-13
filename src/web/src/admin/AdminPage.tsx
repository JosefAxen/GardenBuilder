import { Link } from 'react-router'

/** Admin for sales staff and administrators (see docs/design.md, "Säljarens flöde i admin"). */
export function AdminPage() {
  return (
    <main>
      <h1>Admin</h1>
      <p>Ärendelista, katalog och inställningar kommer här.</p>
      <Link to="/">Till startsidan</Link>
    </main>
  )
}
