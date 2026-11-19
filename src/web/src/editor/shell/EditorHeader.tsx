import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { SaveStatus } from '../SaveStatus'

/**
 * The fir header: the wordmark and save status on the left, the price on the right. The step bar and
 * "Begär offert" come with the customer flow and the quote request (delprojekt 11 and 12).
 */
export function EditorHeader({ price }: { price?: ReactNode }) {
  return (
    <header className="editor-header">
      <div className="editor-brand">
        <Link to="/" className="editor-brand__mark">
          Drömträdgård
        </Link>
        <SaveStatus />
      </div>
      <div className="editor-header__end">{price}</div>
    </header>
  )
}
