// Адрес бэкенда задаётся на сборке переменной VITE_API_BASE_URL.
// Пустое значение означает, что сервис не подключён, и приложение говорит об этом прямо.

const baseUrl: string = import.meta.env.VITE_API_BASE_URL ?? ''

export const apiConfigured = baseUrl.length > 0

/** Дом в выдаче поиска. */
export interface AddressItem {
  /** Подпись внутри группы: то, чем дом отличается от соседей по населённому пункту. */
  label: string
  /** Полный адрес — для подтверждения выбора. */
  display: string
  buildingId: number | null
  fiasId: string | null
  managingOrganization: string | null
}

/** Дома одного населённого пункта под общим заголовком. */
export interface AddressGroup {
  header: string
  items: AddressItem[]
}

/**
 * Раскладку готовит бэкенд, а не форма: она одна на бота и мини-приложение,
 * и списки в них выглядят одинаково.
 */
export interface AddressSearchResult {
  total: number
  /** Выдача упёрлась в предел реестра — вариантов может быть больше. */
  limitReached: boolean
  /** Все найденные дома распознаны по реестру, сведений об управляющей организации нет. */
  fromRegistryOnly: boolean
  groups: AddressGroup[]
}

/** Данные жителя для подстановки в обращение. */
export interface Profile {
  fullName: string | null
  apartment: string | null
  address: string | null
}

export class ApiError extends Error {}

/** Текст ошибки для человека: бэкенд отдаёт его в поле detail. */
async function readError(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as { detail?: string; title?: string }
    if (body.detail) return body.detail
  } catch {
    // тело не JSON — остаётся общий текст ниже
  }

  return `Сервис ответил ${response.status}`
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  if (!apiConfigured) {
    throw new ApiError('Сервис ещё не подключён')
  }

  let response: Response
  try {
    response = await fetch(`${baseUrl}${path}`, init)
  } catch (e) {
    if (init?.signal?.aborted) throw e
    throw new ApiError('Нет связи с сервисом. Проверьте интернет и попробуйте ещё раз.')
  }

  if (!response.ok) {
    throw new ApiError(await readError(response))
  }

  return (await response.json()) as T
}

/** Заголовок с подписанными платформой параметрами: по ним бэкенд узнаёт пользователя. */
const authorized = (): HeadersInit => ({
  'Content-Type': 'application/json',
  'X-Max-Init-Data': window.WebApp?.initData ?? '',
})

export const loadProfile = (signal?: AbortSignal) =>
  request<Profile>('/api/me/profile', { headers: authorized(), signal })

export const saveProfile = (fullName: string, apartment: string) =>
  request<{ saved: boolean }>('/api/me/profile', {
    method: 'POST',
    headers: authorized(),
    body: JSON.stringify({ fullName, apartment }),
  })

export const saveDescription = (text: string) =>
  request<{ saved: boolean }>('/api/me/request/description', {
    method: 'POST',
    headers: authorized(),
    body: JSON.stringify({ text }),
  })

export const searchAddresses = (query: string, signal?: AbortSignal) =>
  request<AddressSearchResult>(`/api/buildings/search?query=${encodeURIComponent(query)}`, { signal })

/**
 * Привязка дома. Пользователя бэкенд определяет по initData — параметрам, подписанным
 * платформой. Вне MAX их нет, и привязка честно отказывает.
 */
export const bindBuilding = (item: AddressItem, query: string) =>
  request<{ address: string }>('/api/me/building', {
    method: 'POST',
    headers: authorized(),
    body: JSON.stringify(
      item.buildingId !== null
        ? { buildingId: item.buildingId }
        : { fiasId: item.fiasId, query },
    ),
  })
