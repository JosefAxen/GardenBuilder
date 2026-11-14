/**
 * A new UUID version 7, the id format used everywhere in the system (time ordered, like Ids.New() in C#).
 * Layout: 48-bit Unix time in milliseconds, version 7, then random bits with the RFC 9562 variant.
 */
export function newId(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(16))
  const time = Date.now()
  for (let i = 0; i < 6; i++) bytes[i] = Math.floor(time / 2 ** (8 * (5 - i))) & 0xff
  bytes[6] = (bytes[6] & 0x0f) | 0x70 // version 7
  bytes[8] = (bytes[8] & 0x3f) | 0x80 // variant 10
  const hex = Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('')
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`
}
