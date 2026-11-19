import type { LucideIcon } from 'lucide-react'
import { useRef } from 'react'
import { focusItem, rovingIndex } from './roving'

type Tab<T extends string> = { value: T; label: string; icon?: LucideIcon }

type Props<T extends string> = {
  /** Names the tab list for screen readers. */
  label: string
  value: T
  tabs: Tab<T>[]
  onChange: (value: T) => void
  /** The id of the panel the tabs control, if there is one. */
  controls?: string
}

/** Tabs on a linen track; arrow keys move between them, only the selected tab is in the tab order. */
export function Tabs<T extends string>({ label, value, tabs, onChange, controls }: Props<T>) {
  const list = useRef<HTMLDivElement>(null)
  const current = Math.max(0, tabs.findIndex((t) => t.value === value))

  return (
    <div
      ref={list}
      role="tablist"
      aria-label={label}
      className="ui-tabs"
      onKeyDown={(event) => {
        const next = rovingIndex(event, current, tabs.length)
        if (next === null) return
        event.preventDefault()
        onChange(tabs[next].value)
        focusItem(list.current, '[role="tab"]', next)
      }}
    >
      {tabs.map(({ value: tabValue, label: tabLabel, icon: Icon }) => {
        const selected = tabValue === value
        return (
          <button
            key={tabValue}
            type="button"
            role="tab"
            aria-selected={selected}
            aria-controls={controls}
            tabIndex={selected ? 0 : -1}
            className={selected ? 'ui-tab is-selected' : 'ui-tab'}
            onClick={() => onChange(tabValue)}
          >
            {Icon && <Icon aria-hidden size={16} strokeWidth={1.9} />}
            {tabLabel}
          </button>
        )
      })}
    </div>
  )
}
