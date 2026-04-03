import { Link } from "@tanstack/react-router";
import { useSessionStore } from "../features/auth/stores/session-store";
import { useMeQuery } from "../features/me/hooks/use-me-query";
import { formatTelegramState } from "../shared/lib/telegram-state";
import { PageSection } from "../shared/ui/page-section";

const shortcuts = [
  {
    title: "Встречи",
    copy: "Ближайшие договорённости и простые действия по ним.",
    to: "/today",
  },
  {
    title: "Поиск",
    copy: "Быстрый поиск по важным разговорам и темам.",
    to: "/search",
  },
  {
    title: "Подключения",
    copy: "Проверка Telegram и управление подключением.",
    to: "/settings/connections",
  },
];

export function HomePage() {
  const token = useSessionStore((state) => state.accessToken);
  const email = useSessionStore((state) => state.email);
  const meQuery = useMeQuery(token);

  return (
    <PageSection
      eyebrow="Главная"
      title="Всё важное в одном месте"
      description="Откройте встречи, найдите нужный разговор, проверьте подключение и оставьте отзыв."
    >
      {!token ? (
        <div className="hero-card">
          <div>
            <h3>Начните со входа</h3>
            <p>После входа здесь появятся ваши встречи, поиск по разговорам и настройки подключения.</p>
          </div>
          <div className="hero-metric">
            <span>Первый шаг</span>
            <strong>Войти по коду</strong>
            <Link to="/auth" className="primary-button">
              Открыть вход
            </Link>
          </div>
        </div>
      ) : null}

      {token ? (
        <div className="hero-card">
          <div>
            <h3>Что здесь есть</h3>
            <ul className="bullet-list">
              <li>Ближайшие встречи и напоминания</li>
              <li>Поиск по важным разговорам</li>
              <li>Подключение Telegram</li>
              <li>Обратная связь</li>
            </ul>
          </div>
          <div className="hero-metric">
            <span>Текущий вход</span>
            <strong>
              {meQuery.isLoading && "Загружаем"}
              {meQuery.isSuccess && meQuery.data.email}
              {meQuery.isError && (email ?? "Вход есть")}
            </strong>
          </div>
        </div>
      ) : null}

      {token ? (
        <div className="card-grid">
          <article className="panel-card">
            <h3>Профиль</h3>
            {meQuery.isLoading ? <p>Загружаем данные профиля...</p> : null}
            {meQuery.isError ? <p className="form-error">{String(meQuery.error.message)}</p> : null}
            {meQuery.isSuccess ? (
              <div className="info-list">
                <p><strong>Email:</strong> {meQuery.data.email}</p>
                <p><strong>Telegram:</strong> {formatTelegramState(meQuery.data.telegramState)}</p>
              </div>
            ) : null}
          </article>

          <article className="panel-card">
            <h3>С чего начать</h3>
            <p>Сначала подключите Telegram. После этого здесь появятся встречи и результаты поиска.</p>
          </article>
        </div>
      ) : null}

      {token ? (
        <div className="shortcut-grid">
          {shortcuts.map((shortcut) => (
            <Link key={shortcut.to} to={shortcut.to} className="shortcut-card">
              <span className="eyebrow">Раздел</span>
              <h3>{shortcut.title}</h3>
              <p>{shortcut.copy}</p>
            </Link>
          ))}
        </div>
      ) : null}
    </PageSection>
  );
}
