import type { IdentityErrorCode, NextStep } from '../api/identity'

/** The screens of the staff login, in the order a first login goes through them. */
export type Screen = 'login' | 'password' | 'setup' | 'recovery' | 'code'

/** The screen for the step the server says comes next. */
export function screenFor(next: NextStep): Screen {
  switch (next) {
    case 'two-factor':
      return 'code'
    case 'change-password':
      return 'password'
    case 'setup-two-factor':
      return 'setup'
  }
}

const MESSAGES: Record<IdentityErrorCode, string> = {
  'invalid-credentials': 'Fel e-post eller lösenord.',
  'locked-out': 'Kontot är låst. Försök igen om en stund, eller be en administratör om hjälp.',
  'temporary-password-expired': 'Det tillfälliga lösenordet har gått ut. Be en administratör om ett nytt.',
  'invalid-code': 'Koden stämmer inte. Försök igen.',
  'password-too-short': 'Lösenordet måste ha minst 12 tecken.',
  'password-unchanged': 'Välj ett annat lösenord än det tillfälliga.',
  'session-expired': 'Inloggningen tog för lång tid. Börja om.',
  unavailable: 'Det gick inte att nå servern. Försök igen om en stund.',
}

export function messageFor(code: IdentityErrorCode): string {
  return MESSAGES[code]
}

export const LOGOUT_FAILED = 'Det gick inte att logga ut. Försök igen.'

/**
 * Logs out. False when the server could not be reached: the session may still be valid then, so the person must not
 * be shown the login as if they were out (on a shared computer the next person would be signed in).
 */
export async function trySignOut(logout: () => Promise<void>): Promise<boolean> {
  try {
    await logout()
    return true
  } catch {
    return false
  }
}

export const ROLE_NAMES = { sales: 'Säljare', admin: 'Administratör' } as const
