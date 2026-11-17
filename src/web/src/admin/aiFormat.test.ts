import { describe, expect, it } from 'vitest'
import { budgetLine, formatSek, jobLine } from './aiFormat'

describe('aiFormat', () => {
  it('writes kronor the Swedish way', () => {
    expect(formatSek(12.4)).toBe('12,40 kr')
    expect(formatSek(0)).toBe('0,00 kr')
    expect(formatSek(1234.5)).toBe('1 234,50 kr')
  })

  it('writes the budget line with whole kronor for the budget', () => {
    expect(budgetLine(12.4, 500)).toBe('12,40 kr av 500 kr i dag')
  })

  it('says what a job is doing in Swedish', () => {
    expect(jobLine('queued')).toBe('Väntar i kön…')
    expect(jobLine('running')).toBe('Frågar AI:n…')
    expect(jobLine('succeeded')).toBe('Klart')
    expect(jobLine('failed')).toBe('Misslyckades')
  })
})
