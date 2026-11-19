import { useEffect } from 'react'
import { useSaveStore } from '../saveStore'
import { chooseProposal, loadProposals, setWishes, startProposals, tryProposal, useProposalStore } from './proposalStore'

const STYLES: [string, string][] = [['natural', 'Naturlig'], ['modern', 'Modern'], ['classic', 'Klassisk'], ['rural', 'Lantlig']]
const BUDGETS: [string, string][] = [
  ['under-50k', 'Under 50 000 kr'],
  ['50k-150k', '50 000–150 000 kr'],
  ['150k-300k', '150 000–300 000 kr'],
  ['over-300k', 'Över 300 000 kr'],
]
const CARE: [string, string][] = [['low', 'Låg'], ['medium', 'Medel'], ['high', 'Hög']]
const ZONES = ['I', 'II', 'III', 'IV', 'V', 'VI', 'VII', 'VIII']
const kronor = new Intl.NumberFormat('sv-SE', { maximumFractionDigits: 0 })

/** "Förslag": the customer's wishes, then two whole-garden proposals to try, undo or choose. */
export function ProposalPanel() {
  const gardenId = useSaveStore((s) => s.gardenId)
  const saveStatus = useSaveStore((s) => s.status)
  const { wishes, state, proposals, tried, message, problems } = useProposalStore()

  useEffect(() => {
    if (gardenId) void loadProposals(gardenId)
  }, [gardenId])

  if (!gardenId) {
    return (
      <aside className="editor-panel">
        <h2>Förslag</h2>
        <p className="editor-hint">Forma tomten och huset först, så sparas din trädgård. Sedan kan du få förslag.</p>
      </aside>
    )
  }

  const saved = saveStatus === 'saved' || saveStatus === 'idle'

  return (
    <aside className="editor-panel proposal-panel">
      <h2>Förslag</h2>
      <p className="editor-hint">
        Berätta vad du vill ha, så tar vi fram två förslag på hela trädgården ur vårt lager. Det du har behållit står kvar.
      </p>

      <label>
        Stil
        <select value={wishes.style} onChange={(e) => setWishes({ style: e.target.value })}>
          {STYLES.map(([key, name]) => <option key={key} value={key}>{name}</option>)}
        </select>
      </label>
      <label>
        Budget
        <select value={wishes.budget} onChange={(e) => setWishes({ budget: e.target.value })}>
          {BUDGETS.map(([key, name]) => <option key={key} value={key}>{name}</option>)}
        </select>
      </label>
      <label>
        Skötsel
        <select value={wishes.care} onChange={(e) => setWishes({ care: e.target.value })}>
          {CARE.map(([key, name]) => <option key={key} value={key}>{name}</option>)}
        </select>
      </label>
      <label>
        Växtzon
        <select value={wishes.zone} onChange={(e) => setWishes({ zone: Number(e.target.value) })}>
          {ZONES.map((name, i) => <option key={name} value={i + 1}>Zon {name}</option>)}
        </select>
      </label>
      <p className="editor-hint">Zon I är mildast, i Skåne; zon VIII är kallast, i fjällen.</p>
      <label>
        Önskemål
        <textarea
          maxLength={300}
          rows={3}
          value={wishes.note}
          placeholder="Till exempel plats för grill eller barnens lek"
          onChange={(e) => setWishes({ note: e.target.value })}
        />
      </label>
      {problems.length > 0 && (
        <ul className="warning" role="status">
          {problems.map((p) => <li key={p}>{p}</li>)}
        </ul>
      )}

      <button onClick={() => void startProposals()} disabled={state === 'running' || !saved}>
        Ge mig förslag
      </button>
      {state === 'running' && <p role="status">Tar fram förslag …</p>}
      {message && <p role="status" className={state === 'failed' ? 'warning' : undefined}>{message}</p>}

      {proposals.map((proposal, index) => (
        <section key={index} className={tried === index ? 'proposal-card tried' : 'proposal-card'}>
          <h3>{proposal.name}</h3>
          <p>{proposal.motivation}</p>
          <p className="proposal-price">
            cirka {kronor.format(proposal.low)}–{kronor.format(proposal.high)} kr
            {proposal.overBudget && <span className="warning"> · Över din budget</span>}
          </p>
          <div className="editor-row">
            <button onClick={() => tryProposal(index)}>Prova</button>
            <button onClick={() => void chooseProposal(index)}>Välj det här</button>
          </div>
        </section>
      ))}
      {tried !== null && <p className="editor-hint">Ångra tar tillbaka trädgården som den var.</p>}
    </aside>
  )
}
