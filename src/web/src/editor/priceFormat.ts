const kronor = new Intl.NumberFormat('sv-SE', { maximumFractionDigits: 0 })

/** "42 300 – 51 100 kr", or "4 700 kr" when both ends are the same. */
export function formatRange(low: number, high: number): string {
  return low === high ? `${kronor.format(low)} kr` : `${kronor.format(low)} – ${kronor.format(high)} kr`
}

/** The note about new elements whose article is missing from the catalog. */
export function formatMissing(count: number): string {
  return `${count} ${count === 1 ? 'sak' : 'saker'} i designen saknas i katalogen och ingår inte i priset.`
}
