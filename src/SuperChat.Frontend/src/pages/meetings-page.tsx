import { Link } from "@tanstack/react-router";
import { useMemo } from "react";
import { useSessionStore } from "../features/auth/stores/session-store";
import { useMeQuery } from "../features/me/hooks/use-me-query";
import { MeetingCard } from "../features/meetings/components/meeting-card";
import type { MeetingCard as MeetingCardModel } from "../features/meetings/gateways/meetings-gateway";
import { useMeetingsQuery } from "../features/meetings/hooks/use-meetings-query";
import { PageSection } from "../shared/ui/page-section";

function autoResolvedCount(cards: ReadonlyArray<MeetingCardModel>): number {
  return cards.filter((card) => card.status === "Confirmed").length;
}

export function MeetingsPage() {
  const token = useSessionStore((state) => state.accessToken);
  const meQuery = useMeQuery(token);
  const meetingsQuery = useMeetingsQuery(token);

  const cards = useMemo(() => meetingsQuery.data ?? [], [meetingsQuery.data]);
  const resolved = autoResolvedCount(cards);

  return (
    <PageSection
      eyebrow="Встречи"
      title="Ближайшие встречи"
      description="Здесь собраны ближайшие договорённости — с контекстом, участниками и быстрыми действиями."
      aside={resolved > 0 ? <span className="pill is-gold">● Auto-resolved: {resolved}</span> : null}
    >
      {!token ? (
        <article className="panel-card">
          <h3>Нужен вход</h3>
          <p>После входа здесь появятся ваши встречи и быстрые действия по ним.</p>
          <div className="panel-actions">
            <Link to="/auth" className="btn is-primary">
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
          <h3>Сначала подключите Telegram</h3>
          <p>После подключения здесь появятся встречи и новые договорённости из ваших чатов.</p>
          <div className="panel-actions">
            <Link to="/settings/connections" className="btn is-primary">
              Открыть подключения
            </Link>
          </div>
        </article>
      ) : null}

      {token && meQuery.isSuccess && !meQuery.data.requiresTelegramAction ? (
        <>
          <div className="summary-grid">
            <article className="panel-card">
              <div className="eyebrow">Сводка</div>
              <h3>Встреч: {cards.length}</h3>
              <p>
                {cards.length === 0
                  ? "Пока ничего не пришло — как только найдём договорённость, появится здесь."
                  : `Из них подтверждено: ${resolved}.`}
              </p>
            </article>
            <article className="panel-card">
              <div className="eyebrow">Подключение</div>
              <h3>Telegram</h3>
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

          {meetingsQuery.isSuccess && cards.length === 0 ? (
            <article className="panel-card">
              <h3>Пока пусто</h3>
              <p>Ближайших встреч сейчас нет.</p>
            </article>
          ) : null}

          {meetingsQuery.isSuccess && cards.length > 0 ? (
            <div className="meeting-list">
              {cards.map((card) => (
                <MeetingCard
                  key={`${card.id ?? card.title}-${card.observedAt}`}
                  token={token}
                  card={card}
                />
              ))}
            </div>
          ) : null}
        </>
      ) : null}
    </PageSection>
  );
}
