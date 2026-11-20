import { type ReactNode, useCallback, useEffect, useState } from 'react'
import { fetchMe, logout, type Me } from '../api/identity'
import { Button, Notice } from '../ui'
import { StaffLogin } from './StaffLogin'
import './admin.css'
import { messageFor, trySignOut } from './staffLoginSteps'

export type StaffMe = Extract<Me, { kind: 'staff' }>

type State = { status: 'loading' } | { status: 'staff'; me: StaffMe } | { status: 'login' } | { status: 'unavailable' }

/**
 * Shows its content only to signed-in staff, and the login to everyone else. Asks the server every time instead of
 * remembering anything, so a session the server ended (blocked, 12 hours passed) shows the login at once.
 */
export function RequireStaff({ children }: { children: (me: StaffMe, signOut: () => Promise<boolean>) => ReactNode }) {
  const [state, setState] = useState<State>({ status: 'loading' })
  // Bumped to ask the server again, for example after a login.
  const [asked, setAsked] = useState(0)

  useEffect(() => {
    let current = true
    fetchMe()
      .then((me) => current && setState(me.kind === 'staff' ? { status: 'staff', me } : { status: 'login' }))
      .catch(() => current && setState({ status: 'unavailable' }))
    return () => {
      current = false
    }
  }, [asked])

  const askAgain = useCallback(() => setAsked((n) => n + 1), [])

  /** True when signed out. On false the admin stays, and says the logout failed. */
  const signOut = useCallback(async () => {
    const out = await trySignOut(logout)
    if (out) setState({ status: 'login' })
    return out
  }, [])

  switch (state.status) {
    case 'loading':
      return <main className="staff-login"><p className="staff-login__loading">Laddar…</p></main>
    case 'unavailable':
      return (
        <main className="staff-login">
          <div className="staff-login__card ui-card ui-card--elevated">
            <Notice tone="error">{messageFor('unavailable')}</Notice>
            <Button variant="primary" onClick={askAgain}>
              Försök igen
            </Button>
          </div>
        </main>
      )
    case 'login':
      return <StaffLogin onSignedIn={askAgain} />
    case 'staff':
      return children(state.me, signOut)
  }
}
