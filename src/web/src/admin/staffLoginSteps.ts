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
  'session-expired': 'Inloggningen tog för lång tid. Börja om.',
  unavailable: 'Det gick inte att nå servern. Försök igen om en stund.',
}

export function messageFor(code: IdentityErrorCode): string {
  return MESSAGES[code]
}

export const ROLE_NAMES = { sales: 'Säljare', admin: 'Administratör' } as const
