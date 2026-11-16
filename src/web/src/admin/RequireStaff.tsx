import { type ReactNode, useCallback, useEffect, useState } from 'react'
import { fetchMe, logout, type Me } from '../api/identity'
import { StaffLogin } from './StaffLogin'
import { messageFor } from './staffLoginSteps'

export type StaffMe = Extract<Me, { kind: 'staff' }>

type State = { status: 'loading' } | { status: 'staff'; me: StaffMe } | { status: 'login' } | { status: 'unavailable' }

/**
 * Shows its content only to signed-in staff, and the login to everyone else. Asks the server every time instead of
 * remembering anything, so a session the server ended (blocked, 12 hours passed) shows the login at once.
 */
export function RequireStaff({ children }: { children: (me: StaffMe, signOut: () => Promise<void>) => ReactNode }) {
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

  const signOut = useCallback(async () => {
    try {
      await logout()
    } finally {
      setState({ status: 'login' })
    }
  }, [])

  switch (state.status) {
    case 'loading':
      return <main>Laddar…</main>
    case 'unavailable':
      return (
        <main>
          <p role="alert">{messageFor('unavailable')}</p>
          <button onClick={askAgain}>Försök igen</button>
        </main>
      )
    case 'login':
      return <StaffLogin onSignedIn={askAgain} />
    case 'staff':
      return children(state.me, signOut)
  }
}
