import { describe, expect, it } from 'vitest'
import { messageFor, screenFor } from './staffLoginSteps'

describe('screenFor', () => {
  it('maps each next step to its screen', () => {
    expect(screenFor('two-factor')).toBe('code')
    expect(screenFor('change-password')).toBe('password')
    expect(screenFor('setup-two-factor')).toBe('setup')
  })
})

describe('messageFor', () => {
  it('has a Swedish message for every error code', () => {
    expect(messageFor('invalid-credentials')).toBe('Fel e-post eller lösenord.')
    expect(messageFor('locked-out')).toBe('Kontot är låst. Försök igen om en stund, eller be en administratör om hjälp.')
    expect(messageFor('temporary-password-expired')).toBe('Det tillfälliga lösenordet har gått ut. Be en administratör om ett nytt.')
    expect(messageFor('invalid-code')).toBe('Koden stämmer inte. Försök igen.')
    expect(messageFor('password-too-short')).toBe('Lösenordet måste ha minst 12 tecken.')
    expect(messageFor('session-expired')).toBe('Inloggningen tog för lång tid. Börja om.')
    expect(messageFor('unavailable')).toBe('Det gick inte att nå servern. Försök igen om en stund.')
  })
})
