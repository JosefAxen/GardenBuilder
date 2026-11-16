import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  changePassword,
  confirmTwoFactorSetup,
  fetchMe,
  getTwoFactorSetup,
  IdentityError,
  login,
  logout,
  verifyTwoFactor,
} from './identity'

const json = (status: number, body: unknown) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

describe('identity api', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('turns a 400 body into IdentityError with its code', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => json(400, { code: 'invalid-credentials' })))

    const error = await login('anna@firma.se', 'fel').catch((e: unknown) => e)

    expect(error).toBeInstanceOf(IdentityError)
    expect((error as IdentityError).code).toBe('invalid-credentials')
  })

  it('turns a 401 session-expired into IdentityError', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => json(401, { code: 'session-expired' })))

    const error = await changePassword('ett långt lösenord').catch((e: unknown) => e)

    expect((error as IdentityError).code).toBe('session-expired')
  })

  it('reports a network failure as unavailable', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => Promise.reject(new TypeError('Failed to fetch'))))

    const error = await fetchMe().catch((e: unknown) => e)

    expect((error as IdentityError).code).toBe('unavailable')
  })

  it('reports a server error as unavailable', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response('', { status: 500 })))

    const error = await verifyTwoFactor('123456', false).catch((e: unknown) => e)

    expect((error as IdentityError).code).toBe('unavailable')
  })

  it('sends credentials same-origin and never stores anything', async () => {
    const fetchMock = vi.fn(async (url: string) => {
      if (url.endsWith('/me')) return json(200, { kind: 'staff', name: 'Anna', role: 'admin' })
      if (url.endsWith('/two-factor/setup')) return json(200, { sharedKey: 'abcd efgh', authenticatorUri: 'otpauth://totp/x', recoveryCodes: ['A'] })
      if (url.endsWith('/login') || url.endsWith('/password')) return json(200, { next: 'two-factor' })
      return new Response(null, { status: 204 })
    })
    vi.stubGlobal('fetch', fetchMock)
    const storage = () => ({ setItem: vi.fn(), getItem: vi.fn(), removeItem: vi.fn(), clear: vi.fn(), key: vi.fn(), length: 0 })
    const local = storage()
    const session = storage()
    vi.stubGlobal('localStorage', local)
    vi.stubGlobal('sessionStorage', session)

    expect(await fetchMe()).toEqual({ kind: 'staff', name: 'Anna', role: 'admin' })
    expect(await login('anna@firma.se', 'lösenord')).toBe('two-factor')
    expect(await changePassword('ett långt lösenord')).toBe('two-factor')
    await getTwoFactorSetup()
    await confirmTwoFactorSetup('123456')
    await verifyTwoFactor('123456', false)
    await logout()

    for (const call of fetchMock.mock.calls) {
      expect((call as unknown as [string, RequestInit])[1]?.credentials).toBe('same-origin')
    }
    expect(local.setItem).not.toHaveBeenCalled()
    expect(session.setItem).not.toHaveBeenCalled()
  })
})
