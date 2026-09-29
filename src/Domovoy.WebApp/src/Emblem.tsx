import type { ReactNode } from 'react'

export type Tone = 'blue' | 'amber' | 'green' | 'red'

/**
 * Цветной значок раздела.
 *
 * Подложка полупрозрачная, а не сплошная: так один и тот же цвет читается
 * и на светлом, и на тёмном фоне мессенджера, без второго набора значений.
 */
export function Emblem({ tone, children }: { tone: Tone; children: ReactNode }) {
  return <span className={`emblem emblem--${tone}`}>{children}</span>
}
