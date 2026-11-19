import type { LucideIcon } from 'lucide-react'
import type { ButtonHTMLAttributes } from 'react'

type Props = ButtonHTMLAttributes<HTMLButtonElement> & {
  /** primary: fir; accent: terracotta (the one most important action); secondary: linen; quiet: no surface; danger: removal. */
  variant?: 'primary' | 'accent' | 'secondary' | 'quiet' | 'danger'
  size?: 'm' | 's'
  icon?: LucideIcon
}

export function Button({ variant = 'secondary', size = 'm', icon: Icon, className, children, type = 'button', ...rest }: Props) {
  return (
    <button type={type} className={['ui-button', `ui-button--${variant}`, `ui-button--${size}`, className].filter(Boolean).join(' ')} {...rest}>
      {Icon && <Icon aria-hidden size={size === 's' ? 16 : 18} strokeWidth={1.9} />}
      {children}
    </button>
  )
}
