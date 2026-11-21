import type { LucideIcon } from 'lucide-react'
import type { ButtonHTMLAttributes } from 'react'

type Props = Omit<ButtonHTMLAttributes<HTMLButtonElement>, 'aria-label' | 'children'> & {
  /** The accessible name, in Swedish; also the tooltip unless a title is given. */
  label: string
  icon: LucideIcon
  /** Pressed or current, e.g. the active tool. */
  active?: boolean
  /** A small text under the icon, as on the tool rail. */
  caption?: string
  tone?: 'linen' | 'fir'
}

export function IconButton({ label, icon: Icon, active, caption, tone = 'linen', className, title, type = 'button', ...rest }: Props) {
  return (
    <button
      type={type}
      aria-label={label}
      aria-pressed={active === undefined ? undefined : active}
      title={title ?? label}
      className={['ui-icon-button', caption && 'ui-icon-button--captioned', `ui-icon-button--${tone}`, active && 'is-active', className]
        .filter(Boolean)
        .join(' ')}
      {...rest}
    >
      <Icon aria-hidden size={caption ? 21 : 18} strokeWidth={1.75} />
      {caption && <span className="ui-icon-button__caption">{caption}</span>}
    </button>
  )
}
