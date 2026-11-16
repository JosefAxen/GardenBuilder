import QRCode from 'qrcode'
import { type FormEvent, useCallback, useEffect, useState } from 'react'
import {
  changePassword,
  confirmTwoFactorSetup,
  getTwoFactorSetup,
  IdentityError,
  login,
  type TwoFactorSetup,
  verifyTwoFactor,
} from '../api/identity'
import { messageFor, type Screen, screenFor } from './staffLoginSteps'
import './admin.css'

/**
 * The staff login, one screen at a time. The server decides the next step; nothing about the login is stored in the
 * browser. A first login goes: login → password → setup → recovery. Later logins: login → code.
 */
export function StaffLogin({ onSignedIn }: { onSignedIn: () => void }) {
  const [screen, setScreen] = useState<Screen>('login')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [recoveryCodes, setRecoveryCodes] = useState<string[]>([])

  // Stable, because the setup screen fetches its QR code in an effect that depends on it: a new function each render
  // would fetch again on every render.
  const setupFailed = useCallback((code: 'session-expired' | 'unavailable') => {
    setError(messageFor(code))
    if (code === 'session-expired') setScreen('login')
  }, [])

  /** Runs one step. A step whose session ran out sends the person back to the start, with the reason shown. */
  async function run(step: () => Promise<void>) {
    setBusy(true)
    setError(null)
    try {
      await step()
    } catch (e) {
      const code = e instanceof IdentityError ? e.code : 'unavailable'
      setError(messageFor(code))
      if (code === 'session-expired' || code === 'temporary-password-expired') setScreen('login')
    } finally {
      setBusy(false)
    }
  }

  return (
    <main className="staff-login">
      {screen === 'login' && <LoginForm busy={busy} onSubmit={(email, password) => run(async () => setScreen(screenFor(await login(email, password))))} />}
      {screen === 'password' && (
        <PasswordForm
          busy={busy}
          onMismatch={() => setError('Lösenorden är inte lika.')}
          onSubmit={(password) => run(async () => setScreen(screenFor(await changePassword(password))))}
        />
      )}
      {screen === 'setup' && (
        <SetupForm
          busy={busy}
          onError={setupFailed}
          onSubmit={(code) =>
            run(async () => {
              setRecoveryCodes(await confirmTwoFactorSetup(code))
              setScreen('recovery')
            })
          }
        />
      )}
      {screen === 'recovery' && <RecoveryCodes codes={recoveryCodes} onDone={onSignedIn} />}
      {screen === 'code' && <CodeForm busy={busy} onSubmit={(code, recovery) => run(async () => { await verifyTwoFactor(code, recovery); onSignedIn() })} />}
      {error && (
        <p className="staff-login-error" role="alert">
          {error}
        </p>
      )}
    </main>
  )
}

function LoginForm({ busy, onSubmit }: { busy: boolean; onSubmit: (email: string, password: string) => void }) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const submit = (event: FormEvent) => {
    event.preventDefault()
    onSubmit(email, password)
  }

  return (
    <form onSubmit={submit}>
      <h1>Logga in</h1>
      <label>
        E-post
        <input type="email" autoComplete="username" required value={email} onChange={(e) => setEmail(e.target.value)} />
      </label>
      <label>
        Lösenord
        <input type="password" autoComplete="current-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
      </label>
      <button type="submit" disabled={busy}>
        Logga in
      </button>
    </form>
  )
}

function PasswordForm({ busy, onSubmit, onMismatch }: { busy: boolean; onSubmit: (password: string) => void; onMismatch: () => void }) {
  const [password, setPassword] = useState('')
  const [repeat, setRepeat] = useState('')
  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (password !== repeat) onMismatch()
    else onSubmit(password)
  }

  return (
    <form onSubmit={submit}>
      <h1>Välj ett nytt lösenord</h1>
      <p className="staff-login-hint">Minst 12 tecken.</p>
      <label>
        Nytt lösenord
        <input type="password" autoComplete="new-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
      </label>
      <label>
        Upprepa lösenordet
        <input type="password" autoComplete="new-password" required value={repeat} onChange={(e) => setRepeat(e.target.value)} />
      </label>
      <button type="submit" disabled={busy}>
        Spara lösenord
      </button>
    </form>
  )
}

function SetupForm({ busy, onSubmit, onError }: { busy: boolean; onSubmit: (code: string) => void; onError: (code: 'session-expired' | 'unavailable') => void }) {
  const [setup, setSetup] = useState<(TwoFactorSetup & { qr: string }) | null>(null)
  const [code, setCode] = useState('')

  useEffect(() => {
    let current = true
    getTwoFactorSetup()
      .then(async (s) => ({ ...s, qr: await QRCode.toDataURL(s.authenticatorUri, { margin: 1, width: 200 }) }))
      .then((s) => current && setSetup(s))
      .catch((e: unknown) => current && onError(e instanceof IdentityError && e.code === 'session-expired' ? 'session-expired' : 'unavailable'))
    return () => {
      current = false
    }
  }, [onError])

  const submit = (event: FormEvent) => {
    event.preventDefault()
    onSubmit(code)
  }

  return (
    <form onSubmit={submit}>
      <h1>Koppla tvåfaktor</h1>
      <p>
        Skanna QR-koden med en autentiseringsapp, till exempel Microsoft Authenticator eller Google Authenticator. Går det inte att
        skanna, skriv in nyckeln:
      </p>
      {setup && (
        <>
          <img className="staff-login-qr" src={setup.qr} alt="QR-kod för autentiseringsappen" width={200} height={200} />
          <code className="staff-login-key">{setup.sharedKey}</code>
        </>
      )}
      <label>
        Kod från appen
        <input inputMode="numeric" autoComplete="one-time-code" required value={code} onChange={(e) => setCode(e.target.value)} />
      </label>
      <button type="submit" disabled={busy || !setup}>
        Bekräfta
      </button>
    </form>
  )
}

function RecoveryCodes({ codes, onDone }: { codes: string[]; onDone: () => void }) {
  return (
    <section>
      <h1>Dina reservkoder</h1>
      <p>Spara koderna på ett säkert ställe. Varje kod fungerar en gång, om du inte har mobilen till hands. De visas bara nu.</p>
      <ul className="staff-login-codes">
        {codes.map((code) => (
          <li key={code}>
            <code>{code}</code>
          </li>
        ))}
      </ul>
      <button onClick={onDone}>Jag har sparat koderna</button>
    </section>
  )
}

function CodeForm({ busy, onSubmit }: { busy: boolean; onSubmit: (code: string, recovery: boolean) => void }) {
  const [code, setCode] = useState('')
  const [recovery, setRecovery] = useState(false)
  const submit = (event: FormEvent) => {
    event.preventDefault()
    onSubmit(code, recovery)
  }

  return (
    <form onSubmit={submit}>
      <h1>Ange kod</h1>
      <label>
        {recovery ? 'Reservkod' : 'Kod från appen'}
        <input
          inputMode={recovery ? 'text' : 'numeric'}
          autoComplete="one-time-code"
          required
          value={code}
          onChange={(e) => setCode(e.target.value)}
        />
      </label>
      <button type="button" className="staff-login-link" onClick={() => { setRecovery(!recovery); setCode('') }}>
        {recovery ? 'Använd appen' : 'Använd en reservkod'}
      </button>
      <button type="submit" disabled={busy}>
        Logga in
      </button>
    </form>
  )
}
