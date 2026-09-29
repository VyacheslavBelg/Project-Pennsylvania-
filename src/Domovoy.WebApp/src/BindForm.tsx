import { useEffect, useState } from 'react'
import { Button, Input, Panel, Spinner, Typography } from '@maxhub/max-ui'
import { ApiError, bindBuilding, searchAddresses, type AddressItem, type AddressSearchResult } from './api'

/** Сколько символов нужно, чтобы поиск имел смысл. */
const MIN_QUERY = 3

const messageOf = (e: unknown) =>
  e instanceof ApiError ? e.message : 'Что-то пошло не так. Попробуйте ещё раз.'

/** Крупное сообщение по центру: пустой поиск, загрузка, ошибка. */
function Notice({ icon, title, text }: { icon: string; title: string; text?: string }) {
  return (
    <div className="notice">
      <div className="notice__icon">{icon}</div>
      <div className="notice__title">{title}</div>
      {text && <div className="notice__text">{text}</div>}
    </div>
  )
}

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
        <div className="card card--center">
          <div className="notice__icon">✅</div>
          <Typography.Title>Дом привязан</Typography.Title>
          <p className="card__address card__address--center">{bound}</p>
          <p className="footnote">Карточка дома уже в чате с ботом — там же «Сообщить о проблеме».</p>
        </div>
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
        <div className="card">
          <Notice
            icon="🔍"
            title="Начните вводить адрес"
            text="Улица и номер дома — например, «Баумана 15». Город можно добавить, если домов найдётся много."
          />
        </div>
      )
    }

    if (searching && !found) {
      return (
        <div className="card">
          <Notice icon="⏳" title="Ищем дом…" />
        </div>
      )
    }

    if (error) {
      return (
        <div className="card">
          <Notice icon="⚠️" title="Не получилось" text={error} />
        </div>
      )
    }

    if (!found || found.total === 0) {
      return (
        <div className="card">
          <Notice
            icon="🏚"
            title="Ничего не нашлось"
            text="Проверьте написание или добавьте город — например, «Казань Баумана 15»."
          />
        </div>
      )
    }

    return found.groups.map((group, gi) => (
      <section className="group" key={group.header || gi}>
        {group.header && <h3 className="group__header">{group.header}</h3>}

        <div className="group__body">
          {group.items.map((item, i) => (
            <button
              type="button"
              className="row"
              key={item.fiasId ?? item.buildingId ?? item.display}
              disabled={binding !== null}
              onClick={() => void choose(item)}
            >
              <span className="row__main">
                <span className="row__title">{item.label}</span>
                {item.managingOrganization && (
                  <span className="row__note">{item.managingOrganization}</span>
                )}
              </span>
              <span className="row__tail">
                {binding === item ? <Spinner /> : <span className="row__chevron">›</span>}
              </span>
              {i < group.items.length - 1 && <span className="row__divider" />}
            </button>
          ))}
        </div>
      </section>
    ))
  }

  return (
    <Panel className="app">
      <header className="app__header">
        <Typography.Title>Выбор дома</Typography.Title>
        <Typography.Body className="app__lead">
          Дом определяет, кто отвечает за проблему и какие правила применимы
        </Typography.Body>
      </header>

      {!insideMax && (
        <div className="banner">
          Откройте форму из чата с ботом: искать адрес можно и здесь, но привязать дом
          получится только внутри MAX.
        </div>
      )}

      <div className="card card--field">
        <Input
          placeholder="Улица и номер дома"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          withClearButton
          autoFocus
        />
      </div>

      {renderResults()}

      {!tooShort && found && found.limitReached && (
        <p className="footnote">
          Показаны {found.total} самых подходящих — больше адресный реестр за один запрос
          не отдаёт. Если вашего дома нет, уточните адрес: город, корпус или строение.
        </p>
      )}

      {!tooShort && found && found.fromRegistryOnly && (
        <p className="footnote">
          Адреса — из государственного адресного реестра (ФИАС). Сведений об управляющей
          организации этих домов у нас пока нет: разбор проблемы и срок от них не зависят.
        </p>
      )}
    </Panel>
  )
}
