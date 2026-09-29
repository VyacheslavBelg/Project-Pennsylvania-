import { useEffect, useState } from 'react'
import { CellList, CellSimple, Panel, Typography } from '@maxhub/max-ui'
import { BindForm } from './BindForm'
import { DescribeForm, ProfileForm } from './TextForm'
import { Requests } from './Requests'
import './App.css'

interface BridgeInfo {
  platform: string
  version: string
  device: string
  insideMax: boolean
}

type View = 'home' | 'bind' | 'describe' | 'profile' | 'requests'

/** Экраны, которые бот открывает параметром запуска. */
const VIEWS: readonly View[] = ['bind', 'describe', 'profile', 'requests']

/**
 * Экран, который просит бот. Внутри MAX параметр запуска приходит в initData, вне MAX
 * его можно передать в адресе страницы — так форму удобно проверять в браузере.
 */
function startView(): View {
  const params = new URLSearchParams(window.location.search)
  const value = window.WebApp?.initDataUnsafe?.start_param ?? params.get('startapp') ?? params.get('view')
  return VIEWS.includes(value as View) ? (value as View) : 'home'
}

/**
 * Мини-приложение. Выбор дома работает; карточка обращения со статусом и таймером
 * норматива — следующий шаг.
 */
export default function App() {
  const [view, setView] = useState<View>(startView)
  // Вне MAX объекта Bridge нет, и это известно сразу — ждать эффекта незачем.
  const [bridge, setBridge] = useState<BridgeInfo | null>(() =>
    window.WebApp ? null : { platform: 'вне MAX', version: '—', device: '—', insideMax: false },
  )

  useEffect(() => {
    const webApp = window.WebApp

    if (!webApp) {
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

  if (view === 'bind') {
    return <BindForm />
  }

  if (view === 'describe') {
    return <DescribeForm />
  }

  if (view === 'profile') {
    return <ProfileForm />
  }

  if (view === 'requests') {
    return <Requests />
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
        <CellSimple
          title="Выбрать или сменить дом"
          subtitle="Поиск по государственному адресному реестру"
          showChevron
          separator
          onClick={() => setView('bind')}
        />
        <CellSimple
          title="Мои данные"
          subtitle="ФИО и квартира для текста обращения"
          showChevron
          onClick={() => setView('profile')}
        />
      </CellList>

      <CellList header="Обращения" mode="island">
        <CellSimple
          title="Мои обращения"
          subtitle="Статус и время до истечения норматива"
          showChevron
          onClick={() => setView('requests')}
        />
      </CellList>

      <CellList header="Окружение" mode="island">
        <CellSimple title={bridge?.platform ?? '…'} subtitle="Платформа запуска" separator />
        <CellSimple title={bridge?.version ?? '…'} subtitle="Версия MAX" separator />
        <CellSimple title={bridge?.device ?? '…'} subtitle="Устройство" />
      </CellList>
    </Panel>
  )
}
