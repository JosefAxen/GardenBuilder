import { describe, expect, it } from 'vitest'
import { LOGOUT_FAILED, messageFor, screenFor, trySignOut } from './staffLoginSteps'

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
    expect(messageFor('password-unchanged')).toBe('Välj ett annat lösenord än det tillfälliga.')
  })
})

describe('trySignOut', () => {
  it('is true when the server ended the session', async () => {
    expect(await trySignOut(() => Promise.resolve())).toBe(true)
  })

  it('is false when the server could not be reached, so the person is not told they are out', async () => {
    expect(await trySignOut(() => Promise.reject(new Error('offline')))).toBe(false)
    expect(LOGOUT_FAILED).toBe('Det gick inte att logga ut. Försök igen.')
  })
})
