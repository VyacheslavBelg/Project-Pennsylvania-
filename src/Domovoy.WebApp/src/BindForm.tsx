import { useEffect, useMemo, useState } from 'react'
import { Button, CellList, CellSimple, Input, Panel, Spinner, Typography } from '@maxhub/max-ui'
import { ApiError, bindBuilding, searchAddresses, type AddressCandidate } from './api'

/** Столько вариантов отдаёт поиск; если пришло столько же — вариантов может быть больше. */
const SEARCH_LIMIT = 10

/** Сколько символов нужно, чтобы поиск имел смысл. */
const MIN_QUERY = 3

const messageOf = (e: unknown) =>
  e instanceof ApiError ? e.message : 'Что-то пошло не так. Попробуйте ещё раз.'

/**
 * Общее начало адресов — одной строкой над списком, в строках только то, чем дома
 * отличаются. Так же устроен выбор дома в боте: у корпусов одного дома различается
 * только хвост адреса.
 */
function splitCommonPrefix(addresses: string[]): { common: string; tails: string[] } {
  const parts = addresses.map((a) => a.split(', '))
  let common = 0

  if (parts.length > 1) {
    const limit = Math.min(...parts.map((p) => p.length)) - 1
    while (common < limit && parts.every((p) => p[common].toLowerCase() === parts[0][common].toLowerCase())) {
      common++
    }
  }

  return {
    common: parts[0]?.slice(0, common).join(', ') ?? '',
    tails: parts.map((p) => p.slice(common).join(', ')),
  }
}

/**
 * Выбор дома в мини-приложении.
 *
 * Бот не может удалять сообщения пользователя, и каждый введённый в чат адрес оставался
 * в переписке. Ввод в форме в чат не попадает, а результат приходит туда сам: бэкенд
 * заменяет экран ввода адреса карточкой дома.
 */
export function BindForm() {
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<AddressCandidate[] | null>(null)
  const [searching, setSearching] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [binding, setBinding] = useState<AddressCandidate | null>(null)
  const [bound, setBound] = useState<string | null>(null)

  const insideMax = Boolean(window.WebApp?.initData)
  const trimmed = query.trim()
  const tooShort = trimmed.length < MIN_QUERY

  useEffect(() => {
    // Короткий запрос не ищется; прежние результаты при этом просто не показываются.
    if (tooShort) {
      return
    }

    // Запрос уходит, когда пользователь перестал печатать, а не на каждую букву.
    const controller = new AbortController()
    const timer = setTimeout(() => {
      setSearching(true)
      searchAddresses(trimmed, controller.signal)
        .then((found) => {
          setResults(found)
          setError(null)
        })
        .catch((e: unknown) => {
          if (!controller.signal.aborted) setError(messageOf(e))
        })
        .finally(() => {
          if (!controller.signal.aborted) setSearching(false)
        })
    }, 400)

    return () => {
      clearTimeout(timer)
      controller.abort()
    }
  }, [trimmed, tooShort])

  const { common, tails } = useMemo(
    () => splitCommonPrefix((results ?? []).map((r) => r.display)),
    [results],
  )

  const choose = async (candidate: AddressCandidate) => {
    setBinding(candidate)
    setError(null)

    try {
      const { address } = await bindBuilding(candidate, trimmed)
      setBound(address)
      // Карточка дома уже в чате — форма своё сделала.
      setTimeout(() => window.WebApp?.close?.(), 1500)
    } catch (e) {
      setError(messageOf(e))
    } finally {
      setBinding(null)
    }
  }

  if (bound) {
    return (
      <Panel className="app">
        <header className="app__header">
          <Typography.Title>✅ Дом привязан</Typography.Title>
          <Typography.Body>{bound}</Typography.Body>
        </header>
        <CellList mode="island">
          <CellSimple title="Карточка дома уже в чате с ботом" subtitle="Там же — «Сообщить о проблеме»" />
        </CellList>
        {typeof window.WebApp?.close === 'function' && (
          <Button size="large" stretched onClick={() => window.WebApp?.close?.()}>
            Вернуться в чат
          </Button>
        )}
      </Panel>
    )
  }

  const renderResults = () => {
    if (tooShort) {
      return <CellSimple title="Начните вводить адрес" subtitle="Улица и номер дома, например «Баумана 15»" />
    }

    if (searching && !results) {
      return <CellSimple title="Ищем дом…" before={<Spinner />} />
    }

    if (!results || results.length === 0) {
      return (
        <CellSimple
          title="Ничего не нашлось"
          subtitle="Проверьте написание или добавьте город, например «Казань Баумана 15»"
        />
      )
    }

    return results.map((candidate, i) => (
      <CellSimple
        key={candidate.fiasId ?? candidate.buildingId ?? candidate.display}
        title={tails[i]}
        subtitle={candidate.managingOrganization ?? undefined}
        after={binding === candidate ? <Spinner /> : undefined}
        showChevron={binding !== candidate}
        disabled={binding !== null}
        onClick={() => binding === null && void choose(candidate)}
        separator={i < results.length - 1}
      />
    ))
  }

  return (
    <Panel className="app">
      <header className="app__header">
        <Typography.Title>Выбор дома</Typography.Title>
        <Typography.Body>Дом определяет, кто отвечает за проблему и какие правила применимы</Typography.Body>
      </header>

      {!insideMax && (
        <CellList mode="island">
          <CellSimple
            title="Откройте форму из чата с ботом"
            subtitle="Искать адрес можно и здесь, но привязать дом получится только внутри MAX"
          />
        </CellList>
      )}

      <Input
        placeholder="Улица и номер дома"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        withClearButton
        autoFocus
      />

      {error && !tooShort && (
        <CellList mode="island">
          <CellSimple title="Не получилось" subtitle={error} />
        </CellList>
      )}

      <CellList header={(!tooShort && common) || undefined} mode="island">
        {renderResults()}
      </CellList>

      {!tooShort && results && results.length >= SEARCH_LIMIT && (
        <Typography.Body className="app__hint">
          Показаны первые {results.length}. Если вашего дома нет — уточните адрес: корпус, строение или город.
        </Typography.Body>
      )}

      {!tooShort && results && results.length > 0 && results.every((r) => !r.knownHouse) && (
        <Typography.Body className="app__hint">
          Адреса — из государственного адресного реестра (ФИАС). Сведений об управляющей организации
          этих домов у нас пока нет.
        </Typography.Body>
      )}
    </Panel>
  )
}
