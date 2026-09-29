import { useEffect, useState } from 'react'
import { CellList, CellSimple, Panel, Typography } from '@maxhub/max-ui'
import { BindForm } from './BindForm'
import { DescribeForm, ProfileForm } from './TextForm'
import { Requests } from './Requests'
import { Emblem } from './Emblem'
import { loadProfile, type Profile } from './api'
import './App.css'

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

export default function App() {
  const [view, setView] = useState<View>(startView)
  const [profile, setProfile] = useState<Profile | null>(null)

  // Привязанный дом показывается в шапке: он определяет всё остальное, и человек
  // должен видеть, о каком доме речь, не проваливаясь в разделы.
  useEffect(() => {
    if (view !== 'home' || !window.WebApp?.initData) {
      return
    }

    const controller = new AbortController()

    loadProfile(controller.signal)
      .then(setProfile)
      .catch(() => {
        // Дом не обязателен для показа главного экрана: молча обходимся без него.
      })

    return () => controller.abort()
  }, [view])

  if (view === 'bind') return <BindForm />
  if (view === 'describe') return <DescribeForm />
  if (view === 'profile') return <ProfileForm />
  if (view === 'requests') return <Requests />

  return (
    <Panel className="app">
      <header className="hero">
        <Typography.Title className="hero__title">Домовой</Typography.Title>
        <Typography.Body className="hero__subtitle">
          Кто отвечает за проблему в доме и в какой срок обязан отреагировать
        </Typography.Body>
        {profile?.address && (
          <Typography.Body className="hero__address">🏠 {profile.address}</Typography.Body>
        )}
      </header>

      <CellList mode="island">
        <CellSimple
          before={<Emblem tone="amber">⏱</Emblem>}
          title="Мои обращения"
          subtitle="Статус и время до истечения норматива"
          showChevron
          separator
          onClick={() => setView('requests')}
        />
        <CellSimple
          before={<Emblem tone="blue">🏠</Emblem>}
          title="Мой дом"
          subtitle={profile?.address ?? 'Поиск по государственному адресному реестру'}
          showChevron
          separator
          onClick={() => setView('bind')}
        />
        <CellSimple
          before={<Emblem tone="green">👤</Emblem>}
          title="Мои данные"
          subtitle={profile?.fullName ?? 'ФИО и квартира для текста обращения'}
          showChevron
          onClick={() => setView('profile')}
        />
      </CellList>

      <Typography.Body className="footnote">
        Обращение составляет Домовой, отправляет житель: канала в системы управляющих
        организаций у сервиса нет. Отсчёт срока идёт с момента, когда вы подтвердили отправку.
      </Typography.Body>

      <Typography.Body className="footnote">
        Адреса — из государственного адресного реестра (ФИАС). Сведения об управляющих
        организациях демонстрационные. Нормативные сроки приводятся с основанием.
      </Typography.Body>
    </Panel>
  )
}
