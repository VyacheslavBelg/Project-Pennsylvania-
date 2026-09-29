import { Component, type ErrorInfo, type ReactNode } from 'react'
import { Button, CellList, CellSimple, Panel, Typography } from '@maxhub/max-ui'

interface Props {
  children: ReactNode
}

interface State {
  message: string | null
}

/**
 * Последний рубеж на случай ошибки в разметке.
 *
 * Без него любое необработанное исключение размонтирует дерево React, и пользователь
 * видит пустой белый экран без единого слова о том, что случилось. Так и вышло, когда
 * бэкенд сменил формат ответа поиска, а форма продолжила ждать прежний.
 *
 * Требование критерия — интерфейс сообщает о загрузке, результате и ошибке — относится
 * и к таким сбоям тоже.
 */
export class ErrorBoundary extends Component<Props, State> {
  state: State = { message: null }

  static getDerivedStateFromError(error: unknown): State {
    return { message: error instanceof Error ? error.message : 'Неизвестная ошибка' }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Сбой интерфейса', error, info.componentStack)
  }

  render() {
    if (this.state.message === null) {
      return this.props.children
    }

    return (
      <Panel className="app">
        <header className="app__header">
          <Typography.Title>Что-то сломалось</Typography.Title>
          <Typography.Body>
            Приложение не смогло показать этот экран. Основной сценарий доступен в чате с ботом.
          </Typography.Body>
        </header>

        <CellList mode="island">
          <CellSimple title="Подробности" subtitle={this.state.message} />
        </CellList>

        <Button size="large" stretched onClick={() => window.location.reload()}>
          Перезагрузить
        </Button>
      </Panel>
    )
  }
}
