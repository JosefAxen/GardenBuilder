import { LogOut } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'
import { Button, Card, Notice } from '../ui'
import { AiPanel } from './AiPanel'
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
    <div className="admin">
      <header className="admin-header">
        <Link to="/" className="admin-header__mark">
          Drömträdgård <span>Admin</span>
        </Link>
        <span className="admin-header__user">
          {me.name} · {ROLE_NAMES[me.role]}
        </span>
        <Button variant="quiet" size="s" icon={LogOut} className="admin-header__logout" onClick={() => void logOut()}>
          Logga ut
        </Button>
      </header>
      <main className="admin-main">
        {logoutFailed && <Notice tone="error">{LOGOUT_FAILED}</Notice>}
        <Card as="section" className="admin-card" aria-labelledby="admin-cases-title">
          <h2 id="admin-cases-title">Ärenden</h2>
          <p className="admin-muted">Ärendelista, katalog och inställningar kommer här.</p>
        </Card>
        {me.role === 'admin' && <AiPanel />}
      </main>
    </div>
  )
}
