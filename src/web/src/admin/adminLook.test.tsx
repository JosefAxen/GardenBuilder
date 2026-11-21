// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { CustomerFlowPage } from '../app/CustomerFlowPage'
import { AdminPage } from './AdminPage'
import { StaffLogin } from './StaffLogin'
import { LOGOUT_FAILED } from './staffLoginSteps'

afterEach(cleanup)

describe('the staff login', () => {
  it('is a card under the wordmark, with labelled fields', () => {
    render(<StaffLogin onSignedIn={() => {}} />)
    expect(screen.getByText('Drömträdgård')).toBeTruthy()
    expect(screen.getByText('För personal')).toBeTruthy()
    expect(screen.getByLabelText('E-post')).toBeTruthy()
    expect(screen.getByLabelText('Lösenord')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Logga in' }).className).toContain('ui-button--primary')
  })
})

describe('admin', () => {
  it('has the fir header with the wordmark, and says when logging out failed', async () => {
    render(
      <MemoryRouter>
        <AdminPage me={{ kind: 'staff', name: 'Josef', role: 'sales' } as never} onLogout={vi.fn(async () => false)} />
      </MemoryRouter>,
    )
    expect(screen.getByRole('banner').textContent).toContain('Drömträdgård')
    await userEvent.click(screen.getByRole('button', { name: 'Logga ut' }))
    expect(screen.getByRole('alert').textContent).toContain(LOGOUT_FAILED)
  })
})

describe('the start page', () => {
  it('links to the editor and admin as buttons', () => {
    render(
      <MemoryRouter>
        <CustomerFlowPage />
      </MemoryRouter>,
    )
    const editor = screen.getByRole('link', { name: 'Öppna 3D-editorn' })
    expect(editor.getAttribute('href')).toBe('/editor')
    expect(editor.className).toContain('ui-button--accent')
    const staff = screen.getByRole('link', { name: 'För personal' })
    expect(staff.getAttribute('href')).toBe('/admin')
    expect(staff.className).not.toContain('ui-button')
    expect(screen.getByText('Här börjar editorn i dag')).toBeTruthy()
  })
})
