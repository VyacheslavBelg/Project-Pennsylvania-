// Адрес бэкенда задаётся на сборке. Пока прод-хостинг не поднят (задача 0.5),
// переменная пустая, и приложение обязано осмысленно работать без API.

const baseUrl: string = import.meta.env.VITE_API_BASE_URL ?? ''

export const apiConfigured = baseUrl.length > 0

/** Вариант дома из поиска: свой дом по идентификатору либо адрес из реестра. */
export interface AddressCandidate {
  buildingId: number | null
  display: string
  knownHouse: boolean
  managingOrganization: string | null
  fiasId: string | null
  source: string
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

export const searchAddresses = (query: string, signal?: AbortSignal) =>
  request<AddressCandidate[]>(`/api/buildings/search?query=${encodeURIComponent(query)}`, { signal })

/**
 * Привязка дома. Пользователя бэкенд определяет по initData — параметрам, подписанным
 * платформой. Вне MAX их нет, и привязка честно отказывает.
 */
export const bindBuilding = (candidate: AddressCandidate, query: string) =>
  request<{ address: string }>('/api/me/building', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Max-Init-Data': window.WebApp?.initData ?? '',
    },
    body: JSON.stringify(
      candidate.buildingId !== null
        ? { buildingId: candidate.buildingId }
        : { fiasId: candidate.fiasId, query },
    ),
  })
