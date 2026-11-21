import { Check, PenLine } from 'lucide-react'
import { Button } from '../../ui'
import { useSceneStore } from '../sceneStore'
import { finishShaping, startDrawing } from '../shapeCommands'

/** While the plot or house is shaped: what to do, "Rita om" and "Klar", floating top centre in the scene window. */
export function ShapingStrip() {
  const mode = useSceneStore((s) => s.tool.mode)
  if (mode !== 'shape' && mode !== 'draw') return null

  return (
    <div className="shaping-strip ui-card ui-card--elevated" role="region" aria-label="Formning">
      <p className="shaping-strip__hint">
        {mode === 'draw'
          ? 'Klicka ut hörnen. Avsluta genom att klicka på första punkten.'
          : 'Dra i punkterna. Dra i de små punkterna för att böja en kant. Dubbelklicka för att ta bort en punkt.'}
      </p>
      <Button size="s" variant="secondary" icon={PenLine} onClick={startDrawing} disabled={mode === 'draw'}>
        Rita om
      </Button>
      <Button size="s" variant="primary" icon={Check} onClick={finishShaping}>
        Klar
      </Button>
    </div>
  )
}
