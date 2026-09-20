import { useEffect, useState } from 'react'
import { Button, CellList, CellSimple, Panel, Spinner, Typography } from '@maxhub/max-ui'
import { apiConfigured, searchBuildings, type Building } from './api'
import './App.css'

interface BridgeInfo {
  platform: string
  version: string
  device: string
  insideMax: boolean
}

/**
 * Каркас мини-приложения. Карточка обращения со статусом и таймером норматива
 * появится в Фазе 4 — здесь оболочка и проверка, что Bridge и MAX UI работают
 * внутри мессенджера.
 */
export default function App() {
  const [bridge, setBridge] = useState<BridgeInfo | null>(null)
  const [buildings, setBuildings] = useState<Building[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    const webApp = window.WebApp

    if (!webApp) {
      setBridge({ platform: 'вне MAX', version: '—', device: '—', insideMax: false })
      return
    }

    // Методы Bridge могут возвращать как значение, так и Promise.
    const resolve = async (value: unknown): Promise<string> => {
      try {
        return String((await value) ?? '—')
      } catch {
        return '—'
      }
    }

    void (async () => {
      setBridge({
        platform: await resolve(webApp.platform),
        version: await resolve(webApp.version),
        device: await resolve(webApp.deviceName),
        insideMax: true,
      })
    })()
  }, [])

  useEffect(() => {
    if (!apiConfigured) {
      return
    }

    const controller = new AbortController()
    setLoading(true)

    searchBuildings('Казань', controller.signal)
      .then((found) => {
        setBuildings(found)
        setError(null)
      })
      .catch((e: unknown) => {
        if (controller.signal.aborted) return
        setError(e instanceof Error ? e.message : 'Неизвестная ошибка')
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })

    return () => controller.abort()
  }, [])

  // Состояния загрузки и ошибки показываются явно: критерий требует, чтобы интерфейс
  // сообщал о загрузке, результате действия и возникшей ошибке.
  const renderBuildings = () => {
    if (!apiConfigured) {
      return (
        <CellSimple
          title="Сервис ещё не подключён"
          subtitle="Адрес бэкенда не задан на сборке. Данные о доме появятся после публикации сервиса."
        />
      )
    }

    if (loading) {
      return <CellSimple title="Загружаем данные…" before={<Spinner />} />
    }

    if (error) {
      return (
        <CellSimple
          title="Сервис недоступен"
          subtitle={error}
          after={
            <Button size="small" variant="secondary" onClick={() => window.location.reload()}>
              Повторить
            </Button>
          }
        />
      )
    }

    if (!buildings || buildings.length === 0) {
      return <CellSimple title="Дома не найдены" subtitle="Список пуст" />
    }

    return buildings.map((b) => (
      <CellSimple
        key={b.id}
        title={b.address.full}
        subtitle={b.management.name ?? 'Управляющая организация не указана'}
        overline={b.source.isTestData ? 'Демонстрационные данные' : (b.source.name ?? undefined)}
        separator
      />
    ))
  }

  return (
    <Panel className="app">
      <header className="app__header">
        <Typography.Title>Домовой</Typography.Title>
        <Typography.Body>
          Кто отвечает за проблему в доме и в какой срок обязан отреагировать
        </Typography.Body>
      </header>

      <CellList header="Дом" mode="island">
        {renderBuildings()}
      </CellList>

      <CellList header="Окружение" mode="island">
        <CellSimple title={bridge?.platform ?? '…'} subtitle="Платформа запуска" separator />
        <CellSimple title={bridge?.version ?? '…'} subtitle="Версия MAX" separator />
        <CellSimple title={bridge?.device ?? '…'} subtitle="Устройство" />
      </CellList>
    </Panel>
  )
}
