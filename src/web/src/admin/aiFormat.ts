import type { AiJobStatus } from '../api/ai'

const ore = new Intl.NumberFormat('sv-SE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const kronor = new Intl.NumberFormat('sv-SE', { maximumFractionDigits: 0 })

/** Swedish number formatting puts a no-break space between thousands; a plain space reads the same. */
const plain = (text: string) => text.replace(/ /g, ' ')

/** "12,40 kr". */
export const formatSek = (amount: number) => `${plain(ore.format(amount))} kr`

/** "12,40 kr av 500 kr i dag". */
export const budgetLine = (today: number, budget: number) => `${formatSek(today)} av ${plain(kronor.format(budget))} kr i dag`

const JOB_LINES: Record<AiJobStatus, string> = {
  queued: 'Väntar i kön…',
  running: 'Frågar AI:n…',
  succeeded: 'Klart',
  failed: 'Misslyckades',
}

export const jobLine = (status: AiJobStatus) => JOB_LINES[status]
