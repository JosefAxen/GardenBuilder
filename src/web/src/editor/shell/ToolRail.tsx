import { House, MousePointer2, Redo2, Spline, Undo2 } from 'lucide-react'
import { IconButton } from '../../ui'
import { useSceneStore } from '../sceneStore'
import { finishShaping, startShaping } from '../shapeCommands'

/** The floating tool rail, top left in the scene window: what the customer does to the garden. */
export function ToolRail({ ready }: { ready: boolean }) {
  const tool = useSceneStore((s) => s.tool)
  const elements = useSceneStore((s) => s.document.elements)
  const canUndo = useSceneStore((s) => s.past.length > 0)
  const canRedo = useSceneStore((s) => s.future.length > 0)
  const { undo, redo, setTool, select } = useSceneStore.getState()

  const shapingId = tool.mode === 'shape' || tool.mode === 'draw' ? tool.id : null
  const shapingKind = shapingId ? elements.find((e) => e.id === shapingId)?.kind : null

  const chooseSelect = () => {
    if (shapingId || tool.mode === 'pin') finishShaping()
    else setTool({ mode: 'select' })
    select(null)
  }

  return (
    <nav className="tool-rail" aria-label="Verktyg">
      <IconButton
        icon={MousePointer2}
        caption="Välj"
        label="Välj och flytta"
        active={tool.mode === 'select'}
        disabled={!ready}
        onClick={chooseSelect}
      />
      <IconButton
        icon={Spline}
        caption="Tomt"
        label="Forma tomten"
        active={shapingKind === 'plot'}
        disabled={!ready}
        onClick={() => startShaping('plot')}
      />
      <IconButton
        icon={House}
        caption="Hus"
        label="Forma huset"
        active={shapingKind === 'building'}
        disabled={!ready}
        onClick={() => startShaping('building')}
      />
      <span className="tool-rail__separator" role="separator" />
      <IconButton icon={Undo2} caption="Ångra" label="Ångra (Ctrl+Z)" disabled={!ready || !canUndo} onClick={undo} />
      <IconButton icon={Redo2} caption="Gör om" label="Gör om (Ctrl+Y)" disabled={!ready || !canRedo} onClick={redo} />
    </nav>
  )
}
