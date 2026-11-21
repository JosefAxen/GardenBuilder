// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { House, Trees } from 'lucide-react'
import { useState } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { Button, IconButton, Notice, SearchField, SegmentedControl, Tabs } from './index'

afterEach(cleanup)

type Panel = 'catalog' | 'photos' | 'proposals'
const TABS = [
  { value: 'catalog' as const, label: 'Katalog' },
  { value: 'photos' as const, label: 'Foton' },
  { value: 'proposals' as const, label: 'Förslag' },
]

function TabsHarness({ onChange }: { onChange: (v: Panel) => void }) {
  const [value, setValue] = useState<Panel>('proposals')
  return (
    <Tabs
      label="Panel"
      value={value}
      tabs={TABS}
      onChange={(v) => {
        setValue(v)
        onChange(v)
      }}
    />
  )
}

describe('Tabs', () => {
  it('ArrowRight from the last tab wraps to the first and only the selected tab is in the tab order', async () => {
    const onChange = vi.fn()
    render(<TabsHarness onChange={onChange} />)
    const last = screen.getByRole('tab', { name: 'Förslag' })
    expect(last.tabIndex).toBe(0)
    expect(screen.getByRole('tab', { name: 'Katalog' }).tabIndex).toBe(-1)

    last.focus()
    await userEvent.keyboard('{ArrowRight}')

    expect(onChange).toHaveBeenCalledWith('catalog')
    const first = screen.getByRole('tab', { name: 'Katalog' })
    expect(first.getAttribute('aria-selected')).toBe('true')
    expect(first.tabIndex).toBe(0)
    expect(document.activeElement).toBe(first)
  })
})

describe('SegmentedControl', () => {
  const options = [
    { value: 'top' as const, label: 'Ovanifrån', icon: House },
    { value: 'perspective' as const, label: '3D', icon: Trees },
  ]

  it('arrow keys change the value', async () => {
    const onChange = vi.fn()
    render(<SegmentedControl label="Vy" value="perspective" options={options} onChange={onChange} />)
    screen.getByRole('radio', { name: '3D' }).focus()
    await userEvent.keyboard('{ArrowLeft}')
    expect(onChange).toHaveBeenCalledWith('top')
  })

  it('does nothing when disabled', async () => {
    const onChange = vi.fn()
    render(<SegmentedControl label="Vy" value="perspective" options={options} onChange={onChange} disabled />)
    await userEvent.click(screen.getByRole('radio', { name: 'Ovanifrån' }))
    expect(onChange).not.toHaveBeenCalled()
  })
})

describe('buttons', () => {
  it('an IconButton is named by its label', () => {
    render(<IconButton label="Utvecklarverktyg" icon={House} />)
    expect(screen.getByRole('button', { name: 'Utvecklarverktyg' })).toBeTruthy()
  })

  it('a disabled Button does not fire onClick', async () => {
    const onClick = vi.fn()
    render(
      <Button disabled onClick={onClick}>
        Spara
      </Button>,
    )
    await userEvent.click(screen.getByRole('button', { name: 'Spara' }))
    expect(onClick).not.toHaveBeenCalled()
  })
})

describe('SearchField', () => {
  function Harness() {
    const [value, setValue] = useState('')
    return <SearchField label="Sök i katalogen" value={value} onChange={setValue} />
  }

  it('types, shows a clear button only with text, and clears', async () => {
    render(<Harness />)
    expect(screen.queryByRole('button', { name: 'Rensa sökning' })).toBeNull()
    const input = screen.getByRole('searchbox', { name: 'Sök i katalogen' }) as HTMLInputElement
    await userEvent.type(input, 'lönn')
    expect(input.value).toBe('lönn')
    await userEvent.click(screen.getByRole('button', { name: 'Rensa sökning' }))
    expect(input.value).toBe('')
    expect(screen.queryByRole('button', { name: 'Rensa sökning' })).toBeNull()
  })
})

describe('Notice', () => {
  it('an error is announced as an alert', () => {
    render(<Notice tone="error">Katalogen kunde inte hämtas.</Notice>)
    expect(screen.getByRole('alert').textContent).toContain('Katalogen kunde inte hämtas.')
  })

  it('info is not an alert', () => {
    render(<Notice tone="info">Laddar katalogen…</Notice>)
    expect(screen.queryByRole('alert')).toBeNull()
  })
})
