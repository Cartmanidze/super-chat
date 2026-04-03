import { Link } from "@tanstack/react-router";
import { PageSection } from "../shared/ui/page-section";
import { useSessionStore } from "../features/auth/stores/session-store";
import { useMeQuery } from "../features/me/hooks/use-me-query";
import { useMeetingsQuery } from "../features/meetings/hooks/use-meetings-query";
import { MeetingCard } from "../features/meetings/components/meeting-card";

export function MeetingsPage() {
  const token = useSessionStore((state) => state.accessToken);
  const meQuery = useMeQuery(token);
  const meetingsQuery = useMeetingsQuery(token);

  return (
    <PageSection
      eyebrow="Встречи"
      title="Ближайшие встречи"
      description="Здесь собраны ближайшие договорённости, о которых стоит помнить."
    >
      {!token ? (
        <article className="panel-card">
          <h3>Нужен вход</h3>
          <p>После входа здесь появятся ваши встречи и короткие действия по ним.</p>
          <div className="panel-actions">
            <Link to="/auth" className="primary-button">
              Открыть вход
            </Link>
          </div>
        </article>
      ) : null}

      {token && meQuery.isLoading ? (
        <article className="panel-card">
          <h3>Проверяем доступ</h3>
          <p>Загружаем ваш профиль и состояние подключения.</p>
        </article>
      ) : null}

      {token && meQuery.isError ? (
        <article className="panel-card">
          <h3>Не удалось загрузить данные</h3>
          <p className="form-error">{String(meQuery.error.message)}</p>
        </article>
      ) : null}

      {token && meQuery.isSuccess && meQuery.data.requiresTelegramAction ? (
        <article className="panel-card">
          <h3>Нужно подключить Telegram</h3>
          <p>После подключения здесь появятся встречи и новые договорённости.</p>
          <div className="panel-actions">
            <Link to="/settings/connections" className="primary-button">
              Открыть подключения
            </Link>
          </div>
        </article>
      ) : null}

      {token && meQuery.isSuccess && !meQuery.data.requiresTelegramAction ? (
        <>
          <div className="card-grid">
            <article className="panel-card">
              <h3>Сводка</h3>
              <p>Встреч: {meetingsQuery.data?.length ?? 0}</p>
            </article>
            <article className="panel-card">
              <h3>Подключение</h3>
              <p>{meQuery.data.telegramState}</p>
            </article>
          </div>

          {meetingsQuery.isLoading ? (
            <article className="panel-card">
              <h3>Загружаем встречи</h3>
              <p>Подождите немного.</p>
            </article>
          ) : null}

          {meetingsQuery.isError ? (
            <article className="panel-card">
              <h3>Не удалось показать встречи</h3>
              <p className="form-error">{String(meetingsQuery.error.message)}</p>
            </article>
          ) : null}

          {meetingsQuery.isSuccess && meetingsQuery.data.length === 0 ? (
            <article className="panel-card">
              <h3>Пока пусто</h3>
              <p>Ближайших встреч сейчас нет.</p>
            </article>
          ) : null}

          {meetingsQuery.isSuccess && meetingsQuery.data.length > 0 ? (
            <div className="meeting-list">
              {meetingsQuery.data.map((card) => (
                <MeetingCard key={`${card.id ?? card.title}-${card.observedAt}`} token={token} card={card} />
              ))}
            </div>
          ) : null}
        </>
      ) : null}
    </PageSection>
  );
}
