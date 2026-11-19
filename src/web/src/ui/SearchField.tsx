import { Search, X } from 'lucide-react'
import { useId, useRef } from 'react'

type Props = {
  /** The accessible name; visually hidden, the placeholder carries the hint. */
  label: string
  value: string
  onChange: (value: string) => void
  placeholder?: string
}

export function SearchField({ label, value, onChange, placeholder }: Props) {
  const id = useId()
  const input = useRef<HTMLInputElement>(null)

  return (
    <div className="ui-search">
      <label htmlFor={id} className="visually-hidden">
        {label}
      </label>
      <Search aria-hidden size={17} strokeWidth={1.9} className="ui-search__icon" />
      <input
        ref={input}
        id={id}
        type="search"
        value={value}
        placeholder={placeholder}
        autoComplete="off"
        spellCheck={false}
        onChange={(event) => onChange(event.target.value)}
        onKeyDown={(event) => {
          // Esc empties the field first; with nothing to empty it travels on (e.g. to clear the selection).
          if (event.key === 'Escape' && value) {
            event.stopPropagation()
            onChange('')
          }
        }}
      />
      {value && (
        <button
          type="button"
          className="ui-search__clear"
          aria-label="Rensa sökning"
          onClick={() => {
            onChange('')
            input.current?.focus()
          }}
        >
          <X aria-hidden size={15} strokeWidth={2} />
        </button>
      )}
    </div>
  )
}
