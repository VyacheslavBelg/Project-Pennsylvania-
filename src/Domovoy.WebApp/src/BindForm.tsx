import { Fragment, useEffect, useState } from 'react'
import { Button, CellList, CellSimple, Input, Panel, Spinner, Typography } from '@maxhub/max-ui'
import { ApiError, bindBuilding, searchAddresses, type AddressItem, type AddressSearchResult } from './api'

/** Сколько символов нужно, чтобы поиск имел смысл. */
const MIN_QUERY = 3

const messageOf = (e: unknown) =>
  e instanceof ApiError ? e.message : 'Что-то пошло не так. Попробуйте ещё раз.'

/**
 * Выбор дома в мини-приложении.
 *
 * Бот не может удалять сообщения пользователя — в личном диалоге MAX отвечает на это 403, —
 * и каждый введённый в чат адрес оставался в переписке. Ввод в форме в чат не попадает,
 * а результат приходит туда сам: бэкенд заменяет экран ввода карточкой дома.
 *
 * Раскладку списка — группировку по населённым пунктам и подписи строк — готовит бэкенд,
 * чтобы выдача в форме и в боте выглядела одинаково.
 */
export function BindForm() {
  const [query, setQuery] = useState('')
  const [found, setFound] = useState<AddressSearchResult | null>(null)
  const [searching, setSearching] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [binding, setBinding] = useState<AddressItem | null>(null)
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
        .then((result) => {
          setFound(result)
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

  const choose = async (item: AddressItem) => {
    setBinding(item)
    setError(null)

    try {
      const { address } = await bindBuilding(item, trimmed)
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
      return (
        <CellList mode="island">
          <CellSimple title="Начните вводить адрес" subtitle="Улица и номер дома, например «Баумана 15»" />
        </CellList>
      )
    }

    if (searching && !found) {
      return (
        <CellList mode="island">
          <CellSimple title="Ищем дом…" before={<Spinner />} />
        </CellList>
      )
    }

    if (!found || found.total === 0) {
      return (
        <CellList mode="island">
          <CellSimple
            title="Ничего не нашлось"
            subtitle="Проверьте написание или добавьте город, например «Казань Баумана 15»"
          />
        </CellList>
      )
    }

    return found.groups.map((group, gi) => (
      <CellList key={group.header || gi} header={group.header || undefined} mode="island">
        {group.items.map((item, i) => (
          <CellSimple
            key={item.fiasId ?? item.buildingId ?? item.display}
            title={item.label}
            subtitle={item.managingOrganization ?? undefined}
            after={binding === item ? <Spinner /> : undefined}
            showChevron={binding !== item}
            disabled={binding !== null}
            onClick={() => binding === null && void choose(item)}
            separator={i < group.items.length - 1}
          />
        ))}
      </CellList>
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

      <Fragment>{renderResults()}</Fragment>

      {!tooShort && found && found.limitReached && (
        <Typography.Body className="app__hint">
          Показаны {found.total} самых подходящих — больше адресный реестр за один запрос не отдаёт.
          Если вашего дома нет, уточните адрес: город, корпус или строение.
        </Typography.Body>
      )}

      {!tooShort && found && found.fromRegistryOnly && (
        <Typography.Body className="app__hint">
          Адреса — из государственного адресного реестра (ФИАС). Сведений об управляющей организации
          этих домов у нас пока нет.
        </Typography.Body>
      )}
    </Panel>
  )
}
