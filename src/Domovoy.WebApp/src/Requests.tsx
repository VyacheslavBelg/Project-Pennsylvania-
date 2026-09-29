import { useEffect, useState } from 'react'
import { Button, CellList, CellSimple, Panel, Spinner, Typography } from '@maxhub/max-ui'
import { ApiError, loadRequests, type RequestCard } from './api'
import { remaining } from './Countdown'
import { Emblem, type Tone } from './Emblem'

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
  /** Цвет плашки состояния. */
  pill: 'wait' | 'late' | 'done'
  /** Цвет значка и эмодзи слева от заголовка. */
  tone: Tone
  icon: string
}

/** Состояние обращения словами — те же формулировки, что показывает бот. */
function describeStatus(status: string): StatusLook {
  switch (status) {
    case 'Submitted':
      return { label: 'Ждём ответа', pill: 'wait', tone: 'blue', icon: '⏳' }
    case 'Answered':
      return { label: 'Ответ получен', pill: 'done', tone: 'green', icon: '✅' }
    case 'Breached':
      return { label: 'Срок нарушен', pill: 'late', tone: 'red', icon: '❗' }
    case 'Escalated':
      return { label: 'Жалоба подготовлена', pill: 'late', tone: 'red', icon: '📨' }
    case 'Closed':
      return { label: 'Закрыто', pill: 'done', tone: 'green', icon: '✅' }
    default:
      return { label: 'Черновик', pill: 'wait', tone: 'blue', icon: '✏️' }
  }
}

/** Живой отсчёт до истечения норматива. */
function Deadline({ request, now }: { request: RequestCard; now: number }) {
  if (request.deadlineAt === null) {
    return null
  }

  const left = remaining(request.deadlineAt, request.submittedAt, now)
  const tracked = request.status === 'Submitted' || request.status === 'Breached'

  if (!tracked) {
    return (
      <Typography.Body className="card__deadline">
        Норматив: {request.deadlineDescription}
      </Typography.Body>
    )
  }

  return (
    <div className={`card__timer card__timer--${left.overdue ? 'late' : 'wait'}`}>
      <Typography.Title className="card__clock">
        {left.overdue ? `Просрочено на ${left.text}` : left.text}
      </Typography.Title>
      <Typography.Body className="card__caption">
        {left.overdue ? 'Ответа нет — можно жаловаться в инспекцию' : 'до истечения норматива'}
      </Typography.Body>
      <div className="card__bar">
        <div className="card__bar-fill" style={{ width: `${Math.round(left.progress * 100)}%` }} />
      </div>
    </div>
  )
}

/**
 * Карточки обращений с живым отсчётом.
 *
 * Ради этого экрана мини-приложение и бралось в объём: в чате строка «осталось 9 дн.»
 * устаревает сразу после отправки, а здесь остаток пересчитывается каждую секунду.
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
        <Typography.Body>Сроки считаются от момента, когда вы подтвердили отправку</Typography.Body>
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

        return (
          <CellList key={r.id} header={`№${r.number} · ${r.subject}`} mode="island">
            <CellSimple
              before={<Emblem tone={status.tone}>{status.icon}</Emblem>}
              overline={r.address}
              title={<span className={`pill pill--${status.pill}`}>{status.label}</span>}
              subtitle={`Подано ${shortDate(r.submittedAt)}${r.zone ? ` · отвечает ${r.zone.toLowerCase()}` : ''}`}
              separator
            />

            <div className="card__body">
              <Deadline request={r} now={now} />

              {r.deadlineDescription && (
                <Typography.Body className="card__basis">
                  Норматив: {r.deadlineDescription}
                  {r.deadlineLegalBasis ? ` · основание: ${r.deadlineLegalBasis}` : ''}
                </Typography.Body>
              )}

              {r.generatedText && (
                <details className="card__text">
                  <summary>Текст обращения</summary>
                  <pre>{r.generatedText}</pre>
                </details>
              )}
            </div>
          </CellList>
        )
      })}

      <Typography.Body className="app__hint">
        Отметить ответ, собрать жалобу в инспекцию или удалить обращение можно в чате с ботом.
      </Typography.Body>

      {typeof window.WebApp?.close === 'function' && (
        <Button size="large" stretched onClick={() => window.WebApp?.close?.()}>
          Вернуться в чат
        </Button>
      )}
    </Panel>
  )
}
