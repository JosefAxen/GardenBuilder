/**
 * The identity API (Identity module, /api/identity). Sessions live only in the server's HttpOnly cookies: nothing about
 * a login is ever kept in the browser's storage, and the web asks /me whenever it needs to know who is signed in.
 */

export type StaffRole = 'sales' | 'admin'

export type Me = { kind: 'none' } | { kind: 'customer' } | { kind: 'staff'; name: string; role: StaffRole }

/** Where a staff member goes after a right password or a new one. */
export type NextStep = 'two-factor' | 'change-password' | 'setup-two-factor'

export type TwoFactorSetup = { sharedKey: string; authenticatorUri: string }

export type IdentityErrorCode =
  | 'invalid-credentials'
  | 'locked-out'
  | 'temporary-password-expired'
  | 'invalid-code'
  | 'password-too-short'
  | 'password-unchanged'
  | 'session-expired'
  | 'unavailable'

/** A refusal from the server with its code, or 'unavailable' when the server could not be reached or failed. */
export class IdentityError extends Error {
  readonly code: IdentityErrorCode

  constructor(code: IdentityErrorCode) {
    super(code)
    this.name = 'IdentityError'
    this.code = code
  }
}

const BASE = '/api/identity'

async function request(path: string, init: RequestInit = {}): Promise<Response> {
  let response: Response
  try {
    response = await fetch(BASE + path, { ...init, credentials: 'same-origin', signal: AbortSignal.timeout(10_000) })
  } catch {
    throw new IdentityError('unavailable')
  }

  if (response.status === 400 || response.status === 401) {
    const body = (await response.json().catch(() => null)) as { code?: IdentityErrorCode } | null
    throw new IdentityError(body?.code ?? 'unavailable')
  }

  if (!response.ok) throw new IdentityError('unavailable')
  return response
}

const post = (path: string, body: unknown) =>
  request(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })

export async function fetchMe(): Promise<Me> {
  return (await (await request('/me')).json()) as Me
}

export async function login(email: string, password: string): Promise<NextStep> {
  return ((await (await post('/staff/login', { email, password })).json()) as { next: NextStep }).next
}

export async function changePassword(newPassword: string): Promise<NextStep> {
  return ((await (await post('/staff/password', { newPassword })).json()) as { next: NextStep }).next
}

export async function getTwoFactorSetup(): Promise<TwoFactorSetup> {
  return (await (await request('/staff/two-factor/setup')).json()) as TwoFactorSetup
}

/** Confirms the app with its first code. Signs the person in and returns the recovery codes, shown once. */
export async function confirmTwoFactorSetup(code: string): Promise<string[]> {
  return ((await (await post('/staff/two-factor/setup', { code })).json()) as { recoveryCodes: string[] }).recoveryCodes
}

export async function verifyTwoFactor(code: string, recovery: boolean): Promise<void> {
  await post('/staff/two-factor', { code, recovery })
}

export async function logout(): Promise<void> {
  await post('/staff/logout', {})
}
