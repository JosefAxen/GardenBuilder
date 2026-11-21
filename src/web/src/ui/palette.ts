/**
 * The Plantskolan colours as values, for code that cannot read CSS variables (the 3D scene, tests).
 * Must match tokens.css exactly; the spec's token table is the source.
 */
export const palette = {
  fir: '#1C392D',
  fir2: '#244536',
  fir3: '#2F5241',
  onFir: '#F3EEE4',
  onFir2: '#C3CEC2',
  /** Only on fir: on fir2 it falls below AA contrast. */
  onFir3: '#97A99B',
  linen: '#F8F4EC',
  linen2: '#EFE9DE',
  linen3: '#E4DBCC',
  ink: '#1E2923',
  ink2: '#48544D',
  ink3: '#626C65',
  terra: '#BC522B',
  terraInk: '#9C3F1E',
  terraSoft: '#F6E2D6',
  cut: '#2A2420',
  warn: '#7E520C',
  warnSoft: '#F5E6C4',
  error: '#A3392B',
  errorSoft: '#F7E0DA',
  sceneTop: '#EDE3D3',
  sceneMid: '#E2D5C1',
  sceneBottom: '#D5C4AA',
  white: '#FFFFFF',
} as const

export type PaletteName = keyof typeof palette
