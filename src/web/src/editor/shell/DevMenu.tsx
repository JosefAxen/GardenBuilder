import { Wrench } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { Button, IconButton } from '../../ui'
import { useCatalogStore } from '../catalogStore'
import { addStressTest, useSceneStore } from '../sceneStore'

const number = new Intl.NumberFormat('sv-SE')

/** Remove every new plant: the counterpart of "Lägg till 300 växter", one undo step. */
function clearNewPlants() {
  const { document, execute } = useSceneStore.getState()
  execute({
    type: 'batch',
    commands: document.elements.filter((e) => e.kind === 'point' && e.status === 'new').map((e) => ({ type: 'remove', id: e.id })),
  })
}

/** Tools for measuring and testing the editor, kept out of the customer's way behind one button. */
export function DevMenu({ ready }: { ready: boolean }) {
  const [open, setOpen] = useState(false)
  const root = useRef<HTMLDivElement>(null)
  const measuring = useSceneStore((s) => s.measuring)
  const detail = useSceneStore((s) => s.detail)
  const perf = useSceneStore((s) => s.perf)
  const { setMeasuring, setDetail } = useSceneStore.getState()

  useEffect(() => {
    if (!open) return
    const onPointerDown = (event: PointerEvent) => {
      if (!root.current?.contains(event.target as Node)) setOpen(false)
    }
    window.addEventListener('pointerdown', onPointerDown)
    return () => window.removeEventListener('pointerdown', onPointerDown)
  }, [open])

  return (
    <div
      ref={root}
      className="dev-menu"
      onKeyDown={(event) => {
        if (event.key === 'Escape' && open) {
          // Esc closes the menu only; the editor's own Esc would also clear the selection.
          event.stopPropagation()
          setOpen(false)
        }
      }}
    >
      <IconButton icon={Wrench} label="Utvecklarverktyg" aria-expanded={open} aria-haspopup="true" onClick={() => setOpen(!open)} />
      {open && (
        <div className="dev-menu__panel ui-card ui-card--elevated" role="group" aria-label="Utvecklarverktyg">
          <Button size="s" disabled={!ready} onClick={() => addStressTest(useCatalogStore.getState().articles)}>
            Lägg till 300 växter
          </Button>
          <Button size="s" disabled={!ready} onClick={clearNewPlants}>
            Rensa nya växter
          </Button>
          <label className="dev-menu__check">
            <input type="checkbox" checked={measuring} onChange={(e) => setMeasuring(e.target.checked)} /> Mät prestanda
          </label>
          <label className="dev-menu__check">
            <input type="checkbox" checked={detail === 'high'} onChange={(e) => setDetail(e.target.checked ? 'high' : 'low')} /> Detaljerade
            modeller
          </label>
          {/* fps only means something while the scene is drawn every frame. */}
          {measuring && perf && (
            <p className="dev-menu__perf num">
              {perf.fps} fps · {perf.drawCalls} ritanrop · {number.format(perf.triangles)} trianglar
            </p>
          )}
        </div>
      )}
    </div>
  )
}
