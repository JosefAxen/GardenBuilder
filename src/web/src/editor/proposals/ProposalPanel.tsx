import { Check, Eye, Sparkles } from 'lucide-react'
import { useEffect } from 'react'
import { Badge, Button, Field, Notice } from '../../ui'
import { useSaveStore } from '../saveStore'
import { useSceneStore } from '../sceneStore'
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
  const { wishes, state, proposals, message, problems, stale } = useProposalStore()
  // The proposal on show is the one whose scene is the scene now; after "Ångra" none is.
  const document = useSceneStore((s) => s.document)
  const tried = proposals.findIndex((p) => p.scene === document)

  useEffect(() => {
    if (gardenId) void loadProposals(gardenId)
  }, [gardenId])

  if (!gardenId) {
    return <p className="panel-hint">Forma tomten och huset först, så sparas din trädgård. Sedan kan du få förslag.</p>
  }

  const saved = saveStatus === 'saved' || saveStatus === 'idle'

  return (
    <div className="panel-body proposal-panel">
      <p className="panel-hint">
        Berätta vad du vill ha, så tar vi fram två förslag på hela trädgården ur vårt lager. Det du har behållit står kvar.
      </p>

      <div className="proposal-wishes">
        <Field label="Stil">
          {(field) => (
            <select {...field} value={wishes.style} onChange={(e) => setWishes({ style: e.target.value })}>
              {STYLES.map(([key, name]) => (
                <option key={key} value={key}>
                  {name}
                </option>
              ))}
            </select>
          )}
        </Field>
        <Field label="Budget">
          {(field) => (
            <select {...field} value={wishes.budget} onChange={(e) => setWishes({ budget: e.target.value })}>
              {BUDGETS.map(([key, name]) => (
                <option key={key} value={key}>
                  {name}
                </option>
              ))}
            </select>
          )}
        </Field>
        <Field label="Skötsel">
          {(field) => (
            <select {...field} value={wishes.care} onChange={(e) => setWishes({ care: e.target.value })}>
              {CARE.map(([key, name]) => (
                <option key={key} value={key}>
                  {name}
                </option>
              ))}
            </select>
          )}
        </Field>
        <Field label="Växtzon" hint="Zon I är mildast, i Skåne; zon VIII är kallast, i fjällen.">
          {(field) => (
            <select {...field} value={wishes.zone} onChange={(e) => setWishes({ zone: Number(e.target.value) })}>
              {ZONES.map((name, i) => (
                <option key={name} value={i + 1}>
                  Zon {name}
                </option>
              ))}
            </select>
          )}
        </Field>
        <div className="proposal-wishes__wide">
          <Field label="Önskemål">
            {(field) => (
              <textarea
                {...field}
                maxLength={300}
                rows={3}
                value={wishes.note}
                placeholder="Till exempel plats för grill eller barnens lek"
                onChange={(e) => setWishes({ note: e.target.value })}
              />
            )}
          </Field>
        </div>
      </div>
      {problems.length > 0 && (
        <Notice tone="warn">
          <ul className="panel-list">
            {problems.map((p) => (
              <li key={p}>{p}</li>
            ))}
          </ul>
        </Notice>
      )}

      <Button variant="accent" icon={Sparkles} onClick={() => void startProposals()} disabled={state === 'running' || !saved}>
        Ge mig förslag
      </Button>
      {state === 'running' && (
        <p className="panel-status" role="status">
          Tar fram förslag …
        </p>
      )}
      {message &&
        (state === 'failed' ? (
          <Notice tone="warn">{message}</Notice>
        ) : (
          <p className="panel-status" role="status">
            {message}
          </p>
        ))}

      {proposals.map((proposal, index) => (
        <section key={index} className={tried === index ? 'proposal-card is-tried' : 'proposal-card'} aria-label={proposal.name}>
          {tried === index && (
            <span className="proposal-card__shown">
              <Badge tone="new">Visas nu</Badge>
            </span>
          )}
          <h3>{proposal.name}</h3>
          <p className="proposal-card__why">{proposal.motivation}</p>
          <p className="proposal-card__price num">
            cirka {kronor.format(proposal.low)}–{kronor.format(proposal.high)} kr
            {proposal.overBudget && <Badge tone="warn">Över din budget</Badge>}
          </p>
          {!stale && (
            <div className="proposal-card__actions">
              <Button size="s" variant="secondary" icon={Eye} onClick={() => tryProposal(index)}>
                Prova
              </Button>
              <Button size="s" variant="primary" icon={Check} onClick={() => void chooseProposal(index)}>
                Välj det här
              </Button>
            </div>
          )}
        </section>
      ))}
      {tried >= 0 && <p className="panel-hint">Ångra tar tillbaka trädgården som den var.</p>}
    </div>
  )
}
