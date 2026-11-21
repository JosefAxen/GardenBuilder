import { Box, Grid2x2 } from 'lucide-react'
import { SegmentedControl } from '../../ui'
import { useSceneStore } from '../sceneStore'
import { viewLocked } from './viewLock'

const OPTIONS = [
  { value: 'top' as const, label: 'Ovanifrån', icon: Grid2x2 },
  { value: 'perspective' as const, label: '3D', icon: Box },
]

/** Ovanifrån / 3D, chosen by the customer; locked to Ovanifrån while shaping or pinning a photo. */
export function ViewSwitch() {
  const view = useSceneStore((s) => s.view)
  const mode = useSceneStore((s) => s.tool.mode)
  const locked = viewLocked(mode)

  return (
    <SegmentedControl
      label="Vy"
      value={view}
      options={OPTIONS}
      onChange={(next) => useSceneStore.getState().setView(next)}
      disabled={locked}
      disabledReason="Vyn är ovanifrån medan du formar eller markerar ett foto"
    />
  )
}
