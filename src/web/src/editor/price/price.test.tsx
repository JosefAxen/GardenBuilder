// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import type { PriceEstimate } from '../../api/pricing'
import { useEditorUiStore } from '../editorUiStore'
import { useKeyboardShortcuts } from '../keyboard'
import { formatRange } from '../priceFormat'
import { usePriceStore } from '../priceStore'
import { useSceneStore } from '../sceneStore'
import { PriceDetails } from './PriceDetails'
import { PriceSummary } from './PriceSummary'

const range = (low: number, high: number) => ({ low, high })
const estimate: PriceEstimate = {
  lines: [{ label: 'Skogslönn', quantity: 1, unit: 'piece', low: 3450, high: 3450 } as PriceEstimate['lines'][number]],
  subtotals: { material: range(31200, 44900), labor: range(15400, 23800), removal: range(2000, 2500), fixed: range(0, 0) },
  total: range(48600, 71200),
  vat: range(9720, 14240),
  confidence: 'medium',
  missingArticles: 0,
}

function Price() {
  useKeyboardShortcuts()
  return (
    <>
      <PriceSummary />
      <PriceDetails />
      <p>utanför</p>
    </>
  )
}

afterEach(cleanup)

beforeEach(() => {
  usePriceStore.setState({ estimate, loading: false, error: null })
  useEditorUiStore.setState({ priceOpen: false })
  useSceneStore.setState({ selectedId: null, tool: { mode: 'select' } })
})

describe('the price in the header', () => {
  it('shows the range with VAT and the estimate note', () => {
    render(<Price />)
    const summary = screen.getByRole('button', { name: /Ungefärligt pris/ })
    expect(summary.textContent).toContain(formatRange(48600, 71200))
    expect(summary.textContent).toContain('inkl. moms · uppskattade mått')
  })

  it('opens and closes the details with a click', async () => {
    render(<Price />)
    const summary = screen.getByRole('button', { name: /Ungefärligt pris/ })
    await userEvent.click(summary)
    expect(summary.getAttribute('aria-expanded')).toBe('true')
    expect(screen.getByText('Säkerhet: medel')).toBeTruthy()
    await userEvent.click(summary)
    expect(screen.queryByText('Säkerhet: medel')).toBeNull()
  })

  it('a click outside closes the details', async () => {
    render(<Price />)
    await userEvent.click(screen.getByRole('button', { name: /Ungefärligt pris/ }))
    fireEvent.pointerDown(screen.getByText('utanför'))
    expect(useEditorUiStore.getState().priceOpen).toBe(false)
  })

  it('Esc closes the details and keeps the selected element', async () => {
    useSceneStore.getState().select('tree-1')
    render(<Price />)
    await userEvent.click(screen.getByRole('button', { name: /Ungefärligt pris/ }))
    fireEvent.keyDown(window, { key: 'Escape' })
    expect(useEditorUiStore.getState().priceOpen).toBe(false)
    expect(useSceneStore.getState().selectedId).toBe('tree-1')
  })

  it.each([
    ['invalid-scene', 'Priset kan inte räknas, eftersom designen har fel.'],
    ['unavailable', 'Priset kunde inte hämtas. Försök igen om en stund.'],
  ] as const)('says why there is no price (%s)', (error, text) => {
    usePriceStore.setState({ estimate: null, loading: false, error })
    render(<Price />)
    expect(screen.getByText(text)).toBeTruthy()
  })

  it('says it is counting while a new price is on its way', () => {
    usePriceStore.setState({ estimate, loading: true, error: null })
    render(<Price />)
    expect(screen.getByText('Räknar…')).toBeTruthy()
  })
})
