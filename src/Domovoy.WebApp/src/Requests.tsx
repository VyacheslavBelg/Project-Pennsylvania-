import { useEffect, useState } from 'react'
import { Button, CellList, CellSimple, Panel, Spinner, Typography } from '@maxhub/max-ui'
import { ApiError, loadRequests, type RequestCard } from './api'
import { remaining } from './Countdown'

const messageOf = (e: unknown) =>
  e instanceof ApiError ? e.message : 'Что-то пошло не так. Попробуйте ещё раз.'

const MONTHS = [
  'января', 'февраля', 'марта', 'апреля', 'мая', 'июня',
  'июля', 'августа', 'сентября', 'октября', 'ноября', 'декабря',
]

/** «29 сентября», с годом — только если год не текущий. */
function shortDate(iso: string): string {
  const d = new Date(iso)
  const base = `${d.getDate()} ${MONTHS[d.getMonth()]}`
  return d.getFullYear() === new Date().getFullYear() ? base : `${base} ${d.getFullYear()}`
}

interface StatusLook {
  label: string
  tone: 'wait' | 'late' | 'done'
  icon: string
}

/** Состояние обращения — те же формулировки, что показывает бот. */
function describeStatus(status: string): StatusLook {
  switch (status) {
    case 'Submitted':
      return { label: 'Ждём ответа', tone: 'wait', icon: '⏳' }
    case 'Answered':
      return { label: 'Ответ получен', tone: 'done', icon: '✅' }
    case 'Breached':
      return { label: 'Срок нарушен', tone: 'late', icon: '❗' }
    case 'Escalated':
      return { label: 'Жалоба подготовлена', tone: 'late', icon: '📨' }
    case 'Closed':
      return { label: 'Закрыто', tone: 'done', icon: '✅' }
    default:
      return { label: 'Черновик', tone: 'wait', icon: '✏️' }
  }
}

/** Строка «свойство — значение» с общей колонкой: из них складывается ровная сетка. */
function Fact({ name, value }: { name: string; value: string }) {
  return (
    <div className="fact">
      <span className="fact__name">{name}</span>
      <span className="fact__value">{value}</span>
    </div>
  )
}

/** Живой отсчёт до истечения норматива. */
function Timer({ request, now }: { request: RequestCard; now: number }) {
  if (request.deadlineAt === null) {
    return null
  }

  const left = remaining(request.deadlineAt, request.submittedAt, now)

  return (
    <div className={`timer timer--${left.overdue ? 'late' : 'wait'}`}>
      <div className="timer__value">{left.overdue ? `Просрочено на ${left.text}` : left.text}</div>
      <div className="timer__caption">
        {left.overdue ? 'Ответа нет — это основание для жалобы в инспекцию' : 'до истечения норматива'}
      </div>
      <div className="timer__bar">
        <div className="timer__bar-fill" style={{ width: `${Math.round(left.progress * 100)}%` }} />
      </div>
    </div>
  )
}

/** Текст обращения: его нужно скопировать и отправить, поэтому рядом кнопка копирования. */
function GeneratedText({ text }: { text: string }) {
  const [copied, setCopied] = useState(false)

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(text)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    } catch {
      // Буфер обмена может быть недоступен — текст всё равно виден и выделяется.
    }
  }

  return (
    <div className="doc">
      <div className="doc__head">
        <span className="doc__title">Текст обращения</span>
        <button type="button" className="doc__copy" onClick={() => void copy()}>
          {copied ? 'Скопировано' : 'Копировать'}
        </button>
      </div>
      <div className="doc__text">{text}</div>
    </div>
  )
}

/**
 * Карточки обращений с живым отсчётом.
 *
 * Ради этого экрана мини-приложение и бралось в объём: в чате строка «осталось 9 дн.»
 * устаревает сразу после отправки, а здесь остаток пересчитывается каждую секунду.
 *
 * Вёрстка карточки своя, а не из CellList: список ячеек рассчитан на однородные строки,
 * а здесь разнородные блоки — плашка состояния, таймер, факты, документ, — и вложение
 * их в ячейку ломало выравнивание.
 *
 * Действия — отметить ответ, собрать жалобу, удалить — остались в боте: там они уже
 * работают, и раздваивать их между двумя поверхностями значит раздваивать и ошибки.
 */
export function Requests() {
  const [items, setItems] = useState<RequestCard[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    const controller = new AbortController()

    loadRequests(controller.signal)
      .then((found) => {
        setItems(found)
        setError(null)
      })
      .catch((e: unknown) => {
        if (!controller.signal.aborted) setError(messageOf(e))
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })

    return () => controller.abort()
  }, [])

  // Тик раз в секунду. Останавливается вместе с экраном: таймер, продолжающий
  // работать в фоне, жёг бы батарею телефона впустую.
  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(id)
  }, [])

  if (loading) {
    return (
      <Panel className="app">
        <CellList mode="island">
          <CellSimple title="Загружаем обращения…" before={<Spinner />} />
        </CellList>
      </Panel>
    )
  }

  return (
    <Panel className="app">
      <header className="app__header">
        <Typography.Title>Мои обращения</Typography.Title>
        <Typography.Body className="app__lead">
          Срок считается с момента, когда вы подтвердили отправку
        </Typography.Body>
      </header>

      {error && (
        <CellList mode="island">
          <CellSimple title="Не получилось" subtitle={error} />
        </CellList>
      )}

      {!error && (!items || items.length === 0) && (
        <CellList mode="island">
          <CellSimple
            title="Обращений пока нет"
            subtitle="Сообщите о проблеме в чате с ботом — обращение появится здесь"
          />
        </CellList>
      )}

      {items?.map((r) => {
        const status = describeStatus(r.status)
        const tracked = r.status === 'Submitted' || r.status === 'Breached'

        return (
          <article className="card" key={r.id}>
            <div className="card__top">
              <span className="card__number">№{r.number}</span>
              <span className={`pill pill--${status.tone}`}>
                {status.icon} {status.label}
              </span>
            </div>

            <h3 className="card__subject">{r.subject}</h3>
            <p className="card__address">{r.address}</p>

            {tracked && <Timer request={r} now={now} />}

            <div className="facts">
              {r.zone && <Fact name="Отвечает" value={r.zone} />}
              <Fact name="Подано" value={shortDate(r.submittedAt)} />
              {r.deadlineDescription && <Fact name="Норматив" value={r.deadlineDescription} />}
              {r.deadlineLegalBasis && <Fact name="Основание" value={r.deadlineLegalBasis} />}
            </div>

            {r.generatedText && <GeneratedText text={r.generatedText} />}
          </article>
        )
      })}

      {items && items.length > 0 && (
        <p className="footnote">
          Отметить ответ, собрать жалобу в инспекцию или удалить обращение можно в чате с ботом.
        </p>
      )}

      {typeof window.WebApp?.close === 'function' && (
        <Button size="large" stretched onClick={() => window.WebApp?.close?.()}>
          Вернуться в чат
        </Button>
      )}
    </Panel>
  )
}
