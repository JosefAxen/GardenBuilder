import { Link } from 'react-router'
import type { StaffMe } from './RequireStaff'
import { ROLE_NAMES } from './staffLoginSteps'
import './admin.css'

/** Admin for sales staff and administrators (see docs/design.md, "Säljarens flöde i admin"). Shown only to signed-in staff. */
export function AdminPage({ me, onLogout }: { me: StaffMe; onLogout: () => Promise<void> }) {
  return (
    <main>
      <header className="admin-header">
        <h1>Admin</h1>
        <span className="admin-user">
          Inloggad som {me.name} ({ROLE_NAMES[me.role]})
        </span>
        <button onClick={() => void onLogout()}>Logga ut</button>
      </header>
      <p>Ärendelista, katalog och inställningar kommer här.</p>
      <Link to="/">Till startsidan</Link>
    </main>
  )
}
