// Адрес бэкенда задаётся на сборке. Пока прод-хостинг не поднят (задача 0.5),
// переменная пустая, и приложение обязано осмысленно работать без API.

const baseUrl: string = import.meta.env.VITE_API_BASE_URL ?? ''

export const apiConfigured = baseUrl.length > 0

export interface BuildingSource {
  kind: string
  name?: string | null
  actualAt?: string | null
  isTestData: boolean
}

export interface Building {
  id: number
  address: { region: string; city: string; street: string; house: string; full: string }
  management: { kind: string; name?: string | null; phone?: string | null; emergencyPhone?: string | null }
  buildYear?: number | null
  floors?: number | null
  entrances?: number | null
  source: BuildingSource
}

export class ApiError extends Error {}

async function request<T>(path: string, signal?: AbortSignal): Promise<T> {
  if (!apiConfigured) {
    throw new ApiError('Адрес сервиса не настроен')
  }

  const response = await fetch(`${baseUrl}${path}`, { signal })

  if (!response.ok) {
    throw new ApiError(`Сервис ответил ${response.status}`)
  }

  return (await response.json()) as T
}

export const searchBuildings = (query: string, signal?: AbortSignal) =>
  request<Building[]>(`/api/buildings/search?query=${encodeURIComponent(query)}`, signal)

export const getBuilding = (id: number, signal?: AbortSignal) =>
  request<Building>(`/api/buildings/${id}`, signal)
