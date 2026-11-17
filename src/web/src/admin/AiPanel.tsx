import { type FormEvent, useCallback, useEffect, useRef, useState } from 'react'
import {
  AI_STEPS,
  AiSettingsError,
  fetchAiSettings,
  fetchJob,
  saveAiSettings,
  startConnectionTest,
  type AiJob,
  type AiSettings,
  type AiStep,
} from '../api/ai'
import { budgetLine, formatSek, jobLine } from './aiFormat'

const QUOTA_LABELS: Record<AiStep, string> = {
  'photo-interpretation': 'Fototolkningar per trädgård',
  'design-proposal': 'Designomgångar per trädgård',
  'dream-image': 'Drömbilder per trädgård',
}

const SWITCH_LABELS: Record<AiStep, string> = {
  'photo-interpretation': 'Fototolkning på',
  'design-proposal': 'Designförslag på',
  'dream-image': 'Drömbild på',
}

const POLL_MS = 2_000

/**
 * The AI box in admin, for administrators (docs/superpowers/specs/2026-11-17-ai-jobs-design.md, "Testjobbet i
 * admin"): the connection test, today's cost and the settings.
 */
export function AiPanel() {
  const [settings, setSettings] = useState<AiSettings | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [problems, setProblems] = useState<string[]>([])
  const [saved, setSaved] = useState(false)
  const [job, setJob] = useState<AiJob | null>(null)
  const [testError, setTestError] = useState<string | null>(null)
  const polling = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)
  // Bumped to fetch the settings and today's cost again, for example after a test or a save.
  const [reloads, setReloads] = useState(0)
  const load = useCallback(() => setReloads((n) => n + 1), [])

  useEffect(() => {
    let current = true
    fetchAiSettings()
      .then((fetched) => {
        if (!current) return
        setSettings(fetched)
        setLoadError(null)
      })
      .catch((error: unknown) => current && setLoadError(error instanceof Error ? error.message : 'Inställningarna kunde inte hämtas.'))
    return () => {
      current = false
    }
  }, [reloads])

  useEffect(() => () => clearTimeout(polling.current), [])

  const follow = async (id: string) => {
    try {
      const current = await fetchJob(id)
      setJob(current)
      if (current.status === 'queued' || current.status === 'running') {
        polling.current = setTimeout(() => void follow(id), POLL_MS)
      } else {
        // The call is in today's cost now.
        load()
      }
    } catch (error) {
      setTestError(error instanceof Error ? error.message : 'Jobbet kunde inte hämtas.')
    }
  }

  const test = async () => {
    clearTimeout(polling.current)
    setJob(null)
    setTestError(null)
    try {
      await follow(await startConnectionTest())
    } catch (error) {
      setTestError(error instanceof Error ? error.message : 'Testet kunde inte startas.')
    }
  }

  const save = async (event: FormEvent) => {
    event.preventDefault()
    if (!settings) return
    setProblems([])
    setSaved(false)
    try {
      await saveAiSettings(settings)
      setSaved(true)
      load()
    } catch (error) {
      setProblems(error instanceof AiSettingsError ? error.problems : [error instanceof Error ? error.message : 'Sparandet misslyckades.'])
    }
  }

  const busy = job !== null && (job.status === 'queued' || job.status === 'running')

  return (
    <section className="ai-panel" aria-labelledby="ai-panel-title">
      <h2 id="ai-panel-title">AI</h2>

      {loadError && <p role="alert">{loadError}</p>}
      {settings && <p className="ai-budget">{budgetLine(settings.todayCost, settings.dailyBudget)}</p>}

      <div className="ai-test">
        <button onClick={() => void test()} disabled={busy}>
          Testa AI-anslutningen
        </button>
        {job && <p role="status">{jobLine(job.status)}</p>}
        {job?.status === 'succeeded' && job.result && (
          <dl>
            <dt>Svar</dt>
            <dd>{job.result.answer}</dd>
            <dt>Modell</dt>
            <dd>{job.result.model}</dd>
            <dt>Tid</dt>
            <dd>{(job.result.durationMs / 1000).toLocaleString('sv-SE', { maximumFractionDigits: 1 })} s</dd>
            <dt>Kostnad</dt>
            <dd>{formatSek(job.result.cost)}</dd>
          </dl>
        )}
        {job?.status === 'failed' && <p role="alert">{job.failure}</p>}
        {testError && <p role="alert">{testError}</p>}
      </div>

      {settings && (
        <form className="ai-settings" onSubmit={(event) => void save(event)}>
          <label>
            Dagsbudget (kr)
            <input
              type="number"
              min={0}
              step={1}
              value={settings.dailyBudget}
              onChange={(e) => setSettings({ ...settings, dailyBudget: Number(e.target.value) })}
            />
          </label>
          {AI_STEPS.map((step) => (
            <label key={`quota-${step}`}>
              {QUOTA_LABELS[step]}
              <input
                type="number"
                min={0}
                step={1}
                value={settings.quotas[step]}
                onChange={(e) => setSettings({ ...settings, quotas: { ...settings.quotas, [step]: Number(e.target.value) } })}
              />
            </label>
          ))}
          {AI_STEPS.map((step) => (
            <label key={`switch-${step}`} className="ai-switch">
              <input
                type="checkbox"
                checked={settings.enabled[step]}
                onChange={(e) => setSettings({ ...settings, enabled: { ...settings.enabled, [step]: e.target.checked } })}
              />
              {SWITCH_LABELS[step]}
            </label>
          ))}
          <button type="submit">Spara</button>
          {saved && <p role="status">Sparat.</p>}
          {problems.length > 0 && (
            <ul role="alert">
              {problems.map((problem) => (
                <li key={problem}>{problem}</li>
              ))}
            </ul>
          )}
        </form>
      )}
    </section>
  )
}
