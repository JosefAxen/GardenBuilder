import type { KeyboardEvent } from 'react'

/**
 * Arrow-key movement for a group where one item is selected (tabs, segmented control): Left/Up and Right/Down step
 * with wrap-around, Home and End jump. Returns the new index, or null for any other key.
 */
export function rovingIndex(event: KeyboardEvent, current: number, count: number): number | null {
  switch (event.key) {
    case 'ArrowRight':
    case 'ArrowDown':
      return (current + 1) % count
    case 'ArrowLeft':
    case 'ArrowUp':
      return (current - 1 + count) % count
    case 'Home':
      return 0
    case 'End':
      return count - 1
    default:
      return null
  }
}

/** Moves focus to the item at index inside the group element, after React has re-rendered. */
export function focusItem(group: HTMLElement | null, selector: string, index: number) {
  requestAnimationFrame(() => group?.querySelectorAll<HTMLElement>(selector)[index]?.focus())
}
