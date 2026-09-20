// Объявления для MAX Bridge. Официальных типов нет: библиотека подключается скриптом
// с CDN и кладёт объект в window. Описано по документации dev.max.ru/docs/webapps/bridge.

export interface MaxInitDataUser {
  id: number
  first_name?: string
  last_name?: string
  username?: string
}

export interface MaxInitDataUnsafe {
  user?: MaxInitDataUser
  start_param?: string
}

export interface MaxWebApp {
  /** Стартовые параметры строкой — только их можно проверять на бэкенде. */
  initData?: string
  /** Те же параметры разобранными. Подпись не проверена, доверять нельзя. */
  initDataUnsafe?: MaxInitDataUnsafe
  platform?: string | Promise<string>
  version?: string | Promise<string>
  deviceName?: string | Promise<string>
}

declare global {
  interface Window {
    WebApp?: MaxWebApp
  }
}

export {}
