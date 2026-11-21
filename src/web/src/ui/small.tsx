import { CircleAlert, Info, TriangleAlert } from 'lucide-react'
import { type ReactNode, useId } from 'react'

/** A filter choice, e.g. a catalogue category. */
export function Chip({ selected, onClick, children }: { selected: boolean; onClick: () => void; children: ReactNode }) {
  return (
    <button type="button" aria-pressed={selected} className={selected ? 'ui-chip is-selected' : 'ui-chip'} onClick={onClick}>
      {children}
    </button>
  )
}

/** A linen surface; elevated cards float over the scene with the lift shadow. */
export function Card({
  as: Element = 'div',
  elevated = true,
  className,
  children,
  ...rest
}: {
  as?: 'div' | 'section' | 'aside'
  elevated?: boolean
  className?: string
  children: ReactNode
  id?: string
  'aria-label'?: string
  'aria-labelledby'?: string
}) {
  return (
    <Element className={['ui-card', elevated && 'ui-card--elevated', className].filter(Boolean).join(' ')} {...rest}>
      {children}
    </Element>
  )
}

/** A small status label: new in the design, neutral facts, warnings, or marked for removal. */
export function Badge({ tone, children }: { tone: 'new' | 'neutral' | 'warn' | 'cut'; children: ReactNode }) {
  return <span className={`ui-badge ui-badge--${tone}`}>{children}</span>
}

/**
 * A label with its input. The input is passed as a render function so it gets the id and description that tie it
 * to the label, hint and error.
 */
export function Field({
  label,
  hint,
  error,
  children,
}: {
  label: string
  hint?: string
  error?: string
  children: (props: { id: string; 'aria-describedby'?: string; 'aria-invalid'?: true }) => ReactNode
}) {
  const id = useId()
  const hintId = hint ? `${id}-hint` : undefined
  const errorId = error ? `${id}-error` : undefined
  const describedBy = [hintId, errorId].filter(Boolean).join(' ') || undefined

  return (
    <div className={error ? 'ui-field has-error' : 'ui-field'}>
      <label htmlFor={id} className="ui-field__label">
        {label}
      </label>
      {children({ id, 'aria-describedby': describedBy, 'aria-invalid': error ? true : undefined })}
      {hint && (
        <p id={hintId} className="ui-field__hint">
          {hint}
        </p>
      )}
      {error && (
        <p id={errorId} className="ui-field__error">
          {error}
        </p>
      )}
    </div>
  )
}

const NOTICE_ICONS = { info: Info, warn: TriangleAlert, error: CircleAlert }

/** A message in the flow of a panel. Warnings and errors are announced to screen readers. */
export function Notice({ tone, children, action }: { tone: 'info' | 'warn' | 'error'; children: ReactNode; action?: ReactNode }) {
  const Icon = NOTICE_ICONS[tone]
  return (
    <div className={`ui-notice ui-notice--${tone}`} role={tone === 'info' ? undefined : 'alert'}>
      <Icon aria-hidden size={17} strokeWidth={1.9} className="ui-notice__icon" />
      <div className="ui-notice__body">{children}</div>
      {action && <div className="ui-notice__action">{action}</div>}
    </div>
  )
}
