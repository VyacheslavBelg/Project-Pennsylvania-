import { useEffect, useState } from 'react'
import { Button, CellList, CellSimple, Input, Panel, Spinner, Textarea, Typography } from '@maxhub/max-ui'
import { ApiError, loadProfile, saveDescription, saveProfile, type Profile } from './api'

const messageOf = (e: unknown) =>
  e instanceof ApiError ? e.message : 'Что-то пошло не так. Попробуйте ещё раз.'

/** Экран после успешного сохранения: результат уже в чате, форме остаётся закрыться. */
function Done({ title, subtitle }: { title: string; subtitle: string }) {
  return (
    <Panel className="app">
      <header className="app__header">
        <Typography.Title>✅ {title}</Typography.Title>
        <Typography.Body>{subtitle}</Typography.Body>
      </header>
      {typeof window.WebApp?.close === 'function' && (
        <Button size="large" stretched onClick={() => window.WebApp?.close?.()}>
          Вернуться в чат
        </Button>
      )}
    </Panel>
  )
}

/** Предупреждение снаружи MAX: искать и смотреть можно, сохранять — нет. */
function OutsideMaxNotice() {
  return (
    <CellList mode="island">
      <CellSimple
        title="Откройте форму из чата с ботом"
        subtitle="Сохранить данные можно только внутри MAX"
      />
    </CellList>
  )
}

/**
 * Описание проблемы своими словами.
 *
 * Раньше его набирали сообщением в чат, и оно оставалось в переписке: удалять
 * сообщения пользователя платформа боту не даёт. Здесь текст в чат не попадает,
 * а готовое обращение бот показывает следующим экраном.
 */
export function DescribeForm() {
  const [text, setText] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState(false)

  const insideMax = Boolean(window.WebApp?.initData)

  const submit = async () => {
    setSaving(true)
    setError(null)

    try {
      await saveDescription(text.trim())
      setDone(true)
      setTimeout(() => window.WebApp?.close?.(), 1500)
    } catch (e) {
      setError(messageOf(e))
    } finally {
      setSaving(false)
    }
  }

  if (done) {
    return <Done title="Описание сохранено" subtitle="Готовый текст обращения — в чате с ботом" />
  }

  return (
    <Panel className="app">
      <header className="app__header">
        <Typography.Title>Что случилось</Typography.Title>
        <Typography.Body>
          Опишите проблему своими словами — текст попадёт в обращение
        </Typography.Body>
      </header>

      {!insideMax && <OutsideMaxNotice />}

      <Textarea
        placeholder="Например: с потолка в ванной третий день капает вода, на стене мокрое пятно"
        value={text}
        onChange={(e) => setText(e.target.value)}
        rows={6}
        autoFocus
      />

      <Typography.Body className="app__hint">
        Полезно указать, где именно, когда началось и что уже предпринимали.
        Можно отправить и без описания — обращение соберётся по выбранной теме.
      </Typography.Body>

      {error && (
        <CellList mode="island">
          <CellSimple title="Не получилось" subtitle={error} />
        </CellList>
      )}

      <Button size="large" stretched disabled={saving || !insideMax} onClick={() => void submit()}>
        {saving ? <Spinner /> : 'Сохранить и собрать обращение'}
      </Button>
    </Panel>
  )
}

/**
 * ФИО и номер квартиры.
 *
 * Порядок рассмотрения обращений граждан требует фамилию, имя, отчество и адрес
 * для ответа: без них обращение можно оставить без рассмотрения. Поля заполняются
 * разом и не оставляют сообщений в переписке.
 */
export function ProfileForm() {
  const [profile, setProfile] = useState<Profile | null>(null)
  const [fullName, setFullName] = useState('')
  const [apartment, setApartment] = useState('')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState(false)

  const insideMax = Boolean(window.WebApp?.initData)

  useEffect(() => {
    const controller = new AbortController()

    loadProfile(controller.signal)
      .then((p) => {
        setProfile(p)
        setFullName(p.fullName ?? '')
        setApartment(p.apartment ?? '')
      })
      .catch((e: unknown) => {
        if (!controller.signal.aborted) setError(messageOf(e))
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })

    return () => controller.abort()
  }, [])

  const submit = async () => {
    setSaving(true)
    setError(null)

    try {
      await saveProfile(fullName.trim(), apartment.trim())
      setDone(true)
      setTimeout(() => window.WebApp?.close?.(), 1500)
    } catch (e) {
      setError(messageOf(e))
    } finally {
      setSaving(false)
    }
  }

  if (done) {
    return <Done title="Данные сохранены" subtitle="Они подставятся в текст обращения" />
  }

  return (
    <Panel className="app">
      <header className="app__header">
        <Typography.Title>Ваши данные</Typography.Title>
        <Typography.Body>
          Без ФИО и адреса обращение могут оставить без рассмотрения — этого требует
          порядок работы с обращениями граждан
        </Typography.Body>
      </header>

      {!insideMax && <OutsideMaxNotice />}

      {loading ? (
        <CellList mode="island">
          <CellSimple title="Загружаем данные…" before={<Spinner />} />
        </CellList>
      ) : (
        <>
          <Input
            placeholder="Фамилия Имя Отчество"
            value={fullName}
            onChange={(e) => setFullName(e.target.value)}
            hint="Например: Иванов Иван Иванович"
            withClearButton
            autoFocus
          />

          <Input
            placeholder="Номер квартиры"
            value={apartment}
            onChange={(e) => setApartment(e.target.value)}
            hint={profile?.address ?? 'Дом пока не привязан'}
            withClearButton
          />
        </>
      )}

      {error && (
        <CellList mode="island">
          <CellSimple title="Не получилось" subtitle={error} />
        </CellList>
      )}

      <Button
        size="large"
        stretched
        disabled={saving || loading || !insideMax}
        onClick={() => void submit()}
      >
        {saving ? <Spinner /> : 'Сохранить'}
      </Button>
    </Panel>
  )
}
