// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { useSceneStore } from '../sceneStore'
import { DevMenu } from './DevMenu'
import { ToolRail } from './ToolRail'
import { ViewSwitch } from './ViewSwitch'

afterEach(cleanup)

beforeEach(() => {
  useSceneStore.setState({ view: 'perspective', tool: { mode: 'select' }, past: [], future: [], measuring: false, detail: 'low' })
})

describe('ViewSwitch', () => {
  it('Ovanifrån shows the plan from above', async () => {
    render(<ViewSwitch />)
    await userEvent.click(screen.getByRole('radio', { name: 'Ovanifrån' }))
    expect(useSceneStore.getState().view).toBe('top')
  })

  it('is locked while the plot is shaped', () => {
    useSceneStore.setState({ tool: { mode: 'shape', id: 'plot' }, view: 'top' })
    render(<ViewSwitch />)
    for (const radio of screen.getAllByRole('radio')) expect((radio as HTMLButtonElement).disabled).toBe(true)
  })
})

describe('DevMenu', () => {
  it('is closed by default and opens with all four controls', async () => {
    render(<DevMenu ready />)
    expect(screen.queryByText('Lägg till 300 växter')).toBeNull()

    await userEvent.click(screen.getByRole('button', { name: 'Utvecklarverktyg' }))

    expect(screen.getByRole('button', { name: 'Lägg till 300 växter' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Rensa nya växter' })).toBeTruthy()
    expect(screen.getByRole('checkbox', { name: 'Mät prestanda' })).toBeTruthy()
    expect(screen.getByRole('checkbox', { name: 'Detaljerade modeller' })).toBeTruthy()
  })

  it('Esc closes it', async () => {
    render(<DevMenu ready />)
    await userEvent.click(screen.getByRole('button', { name: 'Utvecklarverktyg' }))
    await userEvent.keyboard('{Escape}')
    expect(screen.queryByText('Lägg till 300 växter')).toBeNull()
  })
})

describe('ToolRail', () => {
  it('Ångra is enabled once there is something to undo', () => {
    const { rerender } = render(<ToolRail ready />)
    expect((screen.getByRole('button', { name: /Ångra/ }) as HTMLButtonElement).disabled).toBe(true)

    const document = useSceneStore.getState().document
    useSceneStore.setState({ past: [document] })
    rerender(<ToolRail ready />)

    expect((screen.getByRole('button', { name: /Ångra/ }) as HTMLButtonElement).disabled).toBe(false)
  })

  it('everything waits until the design has loaded', () => {
    render(<ToolRail ready={false} />)
    expect((screen.getByRole('button', { name: /Forma tomten/ }) as HTMLButtonElement).disabled).toBe(true)
  })
})
