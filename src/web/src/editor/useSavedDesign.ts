import { useEffect, useState } from 'react'
import { fetchLatestDesign } from '../api/designs'
import { hasUnsavedChanges, saveOnLeave, scheduleSave, startSaving } from './saveStore'
import { useSceneStore } from './sceneStore'

export type DesignLoad = 'loading' | 'retrying' | 'ready'

const FIRST_RETRY_MS = 2_000
const MAX_RETRY_MS = 60_000

/**
 * Opens the customer's latest design and keeps saving it. Until the server has answered nothing is saved: a change
 * before that could create a second design next to the customer's real one. A customer without a design keeps the
 * demo garden, and nothing is stored until the first change.
 */
export function useSavedDesign(): DesignLoad {
  const [load, setLoad] = useState<DesignLoad>('loading')

  useEffect(() => {
    let cancelled = false
    let timer: ReturnType<typeof setTimeout> | undefined
    let attempt = 0

    const fetchLatest = async () => {
      try {
        const saved = await fetchLatestDesign()
        if (cancelled) return
        if (saved) {
          // Saving starts first, so showing the loaded design is not taken for a change.
          startSaving(saved, saved.scene)
          useSceneStore.getState().load(saved.scene)
        } else {
          const shown = useSceneStore.getState().document
          startSaving(null, shown)
        }
        setLoad('ready')
      } catch {
        if (cancelled) return
        setLoad('retrying')
        timer = setTimeout(() => void fetchLatest(), Math.min(FIRST_RETRY_MS * 2 ** attempt++, MAX_RETRY_MS))
      }
    }
    void fetchLatest()

    return () => {
      cancelled = true
      clearTimeout(timer)
    }
  }, [])

  // Every new document is a change to save, including after undo and redo.
  useEffect(
    () =>
      useSceneStore.subscribe((state, previous) => {
        if (state.document !== previous.document) scheduleSave(state.document)
      }),
    [],
  )

  useEffect(() => {
    const onPageHide = () => saveOnLeave(useSceneStore.getState().document)
    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      if (hasUnsavedChanges()) event.preventDefault()
    }
    window.addEventListener('pagehide', onPageHide)
    window.addEventListener('beforeunload', onBeforeUnload)
    return () => {
      window.removeEventListener('pagehide', onPageHide)
      window.removeEventListener('beforeunload', onBeforeUnload)
    }
  }, [])

  return load
}
