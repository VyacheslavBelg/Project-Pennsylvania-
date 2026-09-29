/**
 * Остаток нормативного срока.
 *
 * Бэкенд отдаёт момент истечения, а не готовую строку: «осталось 9 дн.», посчитанное
 * на сервере, устаревает в ту же секунду. Здесь остаток пересчитывается на клиенте
 * и обновляется, пока карточка открыта.
 */

export interface Remaining {
  /** Срок истёк. */
  overdue: boolean
  /** «9 дней 4 часа», «2 часа 15 минут», «47 секунд». */
  text: string
  /** Доля пройденного срока от 0 до 1 — для полоски прогресса. */
  progress: number
}

const plural = (n: number, one: string, few: string, many: string) => {
  const mod100 = n % 100
  if (mod100 >= 11 && mod100 <= 14) return many
  switch (n % 10) {
    case 1:
      return one
    case 2:
    case 3:
    case 4:
      return few
    default:
      return many
  }
}

const unit = (n: number, forms: [string, string, string]) =>
  `${n} ${plural(n, forms[0], forms[1], forms[2])}`

/**
 * Показываются две крупнейшие ненулевые единицы: «9 дней 4 часа» полезнее,
 * чем «9 дней 4 часа 17 минут 3 секунды», а когда остаются минуты — важны секунды.
 */
function describe(ms: number): string {
  const total = Math.floor(ms / 1000)
  const days = Math.floor(total / 86400)
  const hours = Math.floor((total % 86400) / 3600)
  const minutes = Math.floor((total % 3600) / 60)
  const seconds = total % 60

  const parts: string[] = []

  if (days > 0) parts.push(unit(days, ['день', 'дня', 'дней']))
  if (days > 0 || hours > 0) parts.push(unit(hours, ['час', 'часа', 'часов']))
  if (days === 0) parts.push(unit(minutes, ['минута', 'минуты', 'минут']))
  if (days === 0 && hours === 0) parts.push(unit(seconds, ['секунда', 'секунды', 'секунд']))

  return parts.slice(0, days > 0 ? 2 : 3).join(' ')
}

export function remaining(deadlineIso: string, submittedIso: string, now: number): Remaining {
  const deadline = new Date(deadlineIso).getTime()
  const submitted = new Date(submittedIso).getTime()
  const left = deadline - now
  const span = deadline - submitted

  return {
    overdue: left <= 0,
    text: describe(Math.abs(left)),
    // span может быть нулевым или отрицательным, если срок пересчитали задним числом.
    progress: span > 0 ? Math.min(1, Math.max(0, (now - submitted) / span)) : 1,
  }
}
