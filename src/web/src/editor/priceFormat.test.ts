import { describe, expect, it } from 'vitest'
import { formatMissing, formatRange } from './priceFormat'

/** Intl puts a narrow no-break space between thousands; compare with plain spaces. */
const plain = (text: string) => text.replace(/\s/g, ' ')

describe('formatRange', () => {
  it('formats a range with spaces and an en dash', () => {
    expect(plain(formatRange(42300, 51100))).toBe('42 300 – 51 100 kr')
  })

  it('formats equal ends as one amount', () => {
    expect(plain(formatRange(4700, 4700))).toBe('4 700 kr')
  })
})

describe('formatMissing', () => {
  it('says sak for one and saker for more', () => {
    expect(formatMissing(1)).toBe('1 sak i designen saknas i katalogen och ingår inte i priset.')
    expect(formatMissing(3)).toBe('3 saker i designen saknas i katalogen och ingår inte i priset.')
  })
})
