import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { useState } from "react";
import { useSessionStore } from "../features/auth/stores/session-store";
import { TelegramLoginCard } from "../features/integrations/components/telegram-login-card";
import { telegramGateway } from "../features/integrations/gateways/telegram-gateway";
import { useTelegramConnectionQuery } from "../features/integrations/hooks/use-telegram-connection-query";
import {
  isTelegramLoginStep,
  type TelegramLoginStep,
} from "../features/integrations/lib/telegram-login-form-state";
import { formatTelegramState } from "../shared/lib/telegram-state";
import { PageSection } from "../shared/ui/page-section";

function formatSyncedAt(value: string | null) {
  if (!value) {
    return "ещё не было";
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "ещё не было";
  }
  const diffMin = Math.max(1, Math.round((Date.now() - date.getTime()) / 60_000));
  if (diffMin < 60) {
    return `${diffMin} мин назад`;
  }
  const diffHrs = Math.round(diffMin / 60);
  if (diffHrs < 24) {
    return `${diffHrs} ч назад`;
  }
  return date.toLocaleDateString("ru-RU", { day: "2-digit", month: "2-digit" });
}

type FutureIntegration = {
  key: string;
  icon: string;
  brandClass: string;
  title: string;
  subtitle: string;
  note: string;
  state: "pending" | "soon";
};

const FUTURE_INTEGRATIONS: FutureIntegration[] = [
  {
    key: "gmail",
    icon: "gm",
    brandClass: "gm",
    title: "Gmail",
    subtitle: "Приглашения и встречи из почты",
    note: "Super Chat прочитает только приглашения и `.ics` — никаких писем целиком.",
    state: "soon",
  },
  {
    key: "calendar",
    icon: "gc",
    brandClass: "gc",
    title: "Google Calendar",
    subtitle: "Синхронизация в обе стороны",
    note: "Поставим найденные встречи в календарь и обновим их, если перенесут в чате.",
    state: "pending",
  },
  {
    key: "slack",
    icon: "sl",
    brandClass: "sl",
    title: "Slack",
    subtitle: "DM и каналы рабочих пространств",
    note: "Напишем, когда будет готово. Пока — в приватном бета-листе.",
    state: "soon",
  },
  {
    key: "outlook",
    icon: "ol",
    brandClass: "ol",
    title: "Outlook",
    subtitle: "Почта и календарь Microsoft 365",
    note: "OAuth-поток готов, ждём верификацию у Microsoft.",
    state: "soon",
  },
];

export function ConnectionsPage() {
  const queryClient = useQueryClient();
  const token = useSessionStore((state) => state.accessToken);
  const [loginInput, setLoginInput] = useState("");
  const [submittedStep, setSubmittedStep] = useState<TelegramLoginStep | null>(null);
  const telegramQuery = useTelegramConnectionQuery(token);
  const rawChatLoginStep = telegramQuery.data?.chatLoginStep ?? null;
  const chatLoginStep = isTelegramLoginStep(rawChatLoginStep) ? rawChatLoginStep : null;
  const activeSubmittedStep = submittedStep === chatLoginStep ? submittedStep : null;
  const visibleLoginInput = submittedStep && submittedStep !== chatLoginStep ? "" : loginInput;

  const invalidateAll = async () => {
    await queryClient.invalidateQueries({ queryKey: ["telegram-connection"] });
  };

  const resetLoginForm = () => {
    setLoginInput("");
    setSubmittedStep(null);
  };

  const connectMutation = useMutation({
    mutationFn: () => telegramGateway.connect(token!),
    onSuccess: async () => {
      resetLoginForm();
      await invalidateAll();
    },
  });

  const reconnectMutation = useMutation({
    mutationFn: () => telegramGateway.reconnect(token!),
    onSuccess: async () => {
      resetLoginForm();
      await invalidateAll();
    },
  });

  const disconnectMutation = useMutation({
    mutationFn: () => telegramGateway.disconnect(token!),
    onSuccess: async () => {
      resetLoginForm();
      await invalidateAll();
    },
  });

  const loginInputMutation = useMutation({
    mutationFn: ({ input }: { input: string; step: TelegramLoginStep }) =>
      telegramGateway.submitLoginInput(token!, input),
    onMutate: ({ step }) => {
      setSubmittedStep(step);
    },
    onSuccess: async () => {
      await invalidateAll();
    },
    onError: () => {
      setSubmittedStep(null);
    },
  });

  const telegramState = telegramQuery.data?.state ?? null;
  const telegramActive = telegramState === "Connected";
  const activeCount = telegramActive ? 1 : 0;
  const totalSources = 1 + FUTURE_INTEGRATIONS.length;

  return (
    <PageSection>
      <div className="section-head">
        <h2>
          Подключения <em>· {activeCount} из {totalSources} активны</em>
        </h2>
      </div>
      <p className="form-note" style={{ marginTop: -12 }}>
        Подключайте источники сообщений. Super Chat прочитает только важное.
      </p>

      {!token ? (
        <article className="panel-card">
          <h3>Нужен вход</h3>
          <p>После входа здесь откроются настройки подключения и шаги для Telegram.</p>
          <div className="panel-actions">
            <Link to="/auth" className="btn is-primary">
              Открыть вход
            </Link>
          </div>
        </article>
      ) : null}

      {token && telegramQuery.isLoading ? (
        <article className="panel-card">
          <h3>Загружаем настройки</h3>
          <p>Подождите немного.</p>
        </article>
      ) : null}

      {token && telegramQuery.isError ? (
        <article className="panel-card">
          <h3>Не удалось загрузить подключение</h3>
          <p className="form-error">{String(telegramQuery.error.message)}</p>
        </article>
      ) : null}

      {token && telegramQuery.isSuccess ? (
        <section className="int-grid">
          <article className={telegramActive ? "int-card is-on" : "int-card"}>
            <div className="int-head">
              <div className="int-ic tg" aria-hidden="true">
                <svg width="22" height="22" viewBox="0 0 24 24" fill="none">
                  <path
                    fill="#fff"
                    d="M9.78 15.56 9.6 20a.76.76 0 0 0 1.22.5l2.6-2.17 3.4 2.5c.63.34 1.08.17 1.23-.58l2.23-10.45c.22-1.02-.37-1.42-.98-1.2L4.62 13.27c-.99.4-.98.96-.18 1.2l3.93 1.22 9.13-5.76c.43-.26.82-.12.5.17L9.78 15.56Z"
                  />
                </svg>
              </div>
              <div className="int-info">
                <h5>
                  Telegram
                  <span className={telegramActive ? "int-pill is-live" : "int-pill is-soon"}>
                    {telegramActive ? "Активно" : formatTelegramState(telegramState ?? "Disconnected")}
                  </span>
                </h5>
                <p>{telegramActive ? "Читает ваши чаты через защищённую сессию." : "Нажмите «Подключить», чтобы начать."}</p>
              </div>
              {telegramActive ? (
                <button
                  type="button"
                  className="int-switch is-on"
                  onClick={() => disconnectMutation.mutate()}
                  disabled={disconnectMutation.isPending}
                  aria-label="Отключить Telegram"
                >
                  <i />
                </button>
              ) : (
                <button
                  type="button"
                  className="btn is-primary int-btn"
                  onClick={() => connectMutation.mutate()}
                  disabled={connectMutation.isPending}
                >
                  {connectMutation.isPending ? "Открываем…" : "Подключить"}
                </button>
              )}
            </div>

            {telegramActive ? (
              <div className="int-stats">
                <div>
                  <b>{telegramQuery.data.requiresAction ? "—" : "Ок"}</b>
                  <em>Состояние</em>
                </div>
                <div>
                  <b>{formatSyncedAt(telegramQuery.data.lastSyncedAt)}</b>
                  <em>Обновлено</em>
                </div>
                <div>
                  <b>24/7</b>
                  <em>Наблюдение</em>
                </div>
              </div>
            ) : null}

            <div className="int-foot">
              <button
                type="button"
                className="btn is-ghost is-sm"
                onClick={() => reconnectMutation.mutate()}
                disabled={reconnectMutation.isPending}
              >
                Переподключить
              </button>
              {telegramActive ? (
                <button
                  type="button"
                  className="btn is-ghost is-sm"
                  onClick={() => disconnectMutation.mutate()}
                  disabled={disconnectMutation.isPending}
                >
                  Отключить
                </button>
              ) : null}
            </div>

            {connectMutation.isError ? (
              <p className="form-error">{String(connectMutation.error.message)}</p>
            ) : null}
            {reconnectMutation.isError ? (
              <p className="form-error">{String(reconnectMutation.error.message)}</p>
            ) : null}
            {disconnectMutation.isError ? (
              <p className="form-error">{String(disconnectMutation.error.message)}</p>
            ) : null}
          </article>

          {FUTURE_INTEGRATIONS.map((item) => (
            <article key={item.key} className="int-card">
              <div className="int-head">
                <div className={`int-ic ${item.brandClass}`} aria-hidden="true">
                  <IntegrationIcon kind={item.icon} />
                </div>
                <div className="int-info">
                  <h5>
                    {item.title}
                    <span className={item.state === "pending" ? "int-pill is-pending" : "int-pill is-soon"}>
                      {item.state === "pending" ? "Готово к подключению" : "Скоро"}
                    </span>
                  </h5>
                  <p>{item.subtitle}</p>
                </div>
                <button type="button" className="btn is-ghost int-btn" disabled>
                  {item.state === "pending" ? "Подключить" : "В очередь"}
                </button>
              </div>
              <p className="int-note">{item.note}</p>
            </article>
          ))}

          <article className="int-card is-muted">
            <div className="int-head">
              <div className="int-ic pl" aria-hidden="true">
                <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#737373" strokeWidth="2" strokeLinecap="round">
                  <path d="M12 5v14M5 12h14" />
                </svg>
              </div>
              <div className="int-info">
                <h5 style={{ color: "var(--text-warm)" }}>Предложить источник</h5>
                <p>Discord? WhatsApp? VK? Расскажите, что подключить следующим.</p>
              </div>
            </div>
          </article>
        </section>
      ) : null}

      {token && telegramQuery.isSuccess && chatLoginStep ? (
        <TelegramLoginCard
          step={chatLoginStep}
          value={visibleLoginInput}
          submittedStep={activeSubmittedStep}
          isSubmitting={loginInputMutation.isPending}
          errorMessage={loginInputMutation.isError ? String(loginInputMutation.error.message) : null}
          onValueChange={setLoginInput}
          onSubmit={() => {
            loginInputMutation.mutate({
              input: visibleLoginInput.trim(),
              step: chatLoginStep,
            });
          }}
        />
      ) : null}
    </PageSection>
  );
}

function IntegrationIcon({ kind }: { kind: string }) {
  switch (kind) {
    case "gm":
      return (
        <svg width="22" height="22" viewBox="0 0 24 24">
          <path fill="#fff" d="M20 4H4a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V6a2 2 0 0 0-2-2Zm-.4 2L12 11 4.4 6h15.2ZM4 18V7.25l7.4 4.85a1 1 0 0 0 1.2 0L20 7.25V18H4Z" />
        </svg>
      );
    case "gc":
      return (
        <svg width="22" height="22" viewBox="0 0 24 24">
          <rect x="3" y="4" width="18" height="17" rx="2" fill="#fff" />
          <rect x="3" y="4" width="18" height="4" fill="#e5383b" />
          <rect x="7" y="11" width="4" height="4" fill="#e5383b" />
        </svg>
      );
    case "sl":
      return (
        <svg width="22" height="22" viewBox="0 0 24 24">
          <path
            fill="#fff"
            d="M6 15a2 2 0 1 1-2-2h2v2Zm1 0a2 2 0 0 1 4 0v5a2 2 0 0 1-4 0v-5Zm2-9a2 2 0 1 1 2-2v2H9Zm0 1a2 2 0 1 1 0 4H4a2 2 0 1 1 0-4h5Zm9 2a2 2 0 1 1 2 2h-2V9Zm-1 0a2 2 0 0 1-4 0V4a2 2 0 1 1 4 0v5Zm-2 9a2 2 0 1 1-2 2v-2h2Zm0-1a2 2 0 1 1 0-4h5a2 2 0 1 1 0 4h-5Z"
          />
        </svg>
      );
    case "ol":
      return (
        <svg width="22" height="22" viewBox="0 0 24 24">
          <rect x="3" y="4" width="12" height="16" rx="1" fill="#fff" />
          <rect x="14" y="9" width="7" height="7" rx="1" fill="#a3a3a3" />
        </svg>
      );
    default:
      return null;
  }
}
