import type { LucideIcon } from 'lucide-react'
import { useRef } from 'react'
import { focusItem, rovingIndex } from './roving'

type Option<T extends string> = { value: T; label: string; icon?: LucideIcon }

type Props<T extends string> = {
  label: string
  value: T
  options: Option<T>[]
  onChange: (value: T) => void
  disabled?: boolean
  /** Why it is disabled, shown as a tooltip. */
  disabledReason?: string
}

/** A choice of one of a few modes (Ovanifrån / 3D) as a radio group. */
export function SegmentedControl<T extends string>({ label, value, options, onChange, disabled, disabledReason }: Props<T>) {
  const group = useRef<HTMLDivElement>(null)
  const current = Math.max(0, options.findIndex((o) => o.value === value))

  return (
    <div
      ref={group}
      role="radiogroup"
      aria-label={label}
      aria-disabled={disabled || undefined}
      title={disabled ? disabledReason : undefined}
      className={disabled ? 'ui-segmented is-disabled' : 'ui-segmented'}
      onKeyDown={(event) => {
        if (disabled) return
        const next = rovingIndex(event, current, options.length)
        if (next === null) return
        event.preventDefault()
        onChange(options[next].value)
        focusItem(group.current, '[role="radio"]', next)
      }}
    >
      {options.map(({ value: optionValue, label: optionLabel, icon: Icon }) => {
        const checked = optionValue === value
        return (
          <button
            key={optionValue}
            type="button"
            role="radio"
            aria-checked={checked}
            disabled={disabled}
            tabIndex={checked ? 0 : -1}
            className={checked ? 'ui-segment is-checked' : 'ui-segment'}
            onClick={() => !checked && onChange(optionValue)}
          >
            {Icon && <Icon aria-hidden size={18} strokeWidth={1.75} />}
            {optionLabel}
          </button>
        )
      })}
    </div>
  )
}
