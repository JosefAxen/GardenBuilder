import { useState } from 'react'
import { Link } from 'react-router'
import type { StaffMe } from './RequireStaff'
import { LOGOUT_FAILED, ROLE_NAMES } from './staffLoginSteps'
import './admin.css'

/** Admin for sales staff and administrators (see docs/design.md, "Säljarens flöde i admin"). Shown only to signed-in staff. */
export function AdminPage({ me, onLogout }: { me: StaffMe; onLogout: () => Promise<boolean> }) {
  const [logoutFailed, setLogoutFailed] = useState(false)
  const logOut = async () => {
    setLogoutFailed(false)
    setLogoutFailed(!(await onLogout()))
  }

  return (
    <main>
      <header className="admin-header">
        <h1>Admin</h1>
        <span className="admin-user">
          Inloggad som {me.name} ({ROLE_NAMES[me.role]})
        </span>
        <button onClick={() => void logOut()}>Logga ut</button>
      </header>
      {logoutFailed && (
        <p className="staff-login-error" role="alert">
          {LOGOUT_FAILED}
        </p>
      )}
      <p>Ärendelista, katalog och inställningar kommer här.</p>
      <Link to="/">Till startsidan</Link>
    </main>
  )
}
