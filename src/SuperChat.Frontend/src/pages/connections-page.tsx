import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { useState } from "react";
import { useSessionStore } from "../features/auth/stores/session-store";
import { telegramGateway } from "../features/integrations/gateways/telegram-gateway";
import { useTelegramConnectionQuery } from "../features/integrations/hooks/use-telegram-connection-query";
import { formatTelegramState } from "../shared/lib/telegram-state";
import { PageSection } from "../shared/ui/page-section";

function formatDate(value: string | null) {
  if (!value) {
    return "Ещё не было синхронизации";
  }

  return new Date(value).toLocaleString("ru-RU", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function getChatLoginPrompt(step: string | null) {
  switch (step) {
    case "phone":
      return "Введите номер телефона, который привязан к Telegram.";
    case "code":
      return "Введите код, который пришёл в Telegram.";
    case "password":
      return "Введите пароль двухэтапной проверки Telegram.";
    default:
      return null;
  }
}

export function ConnectionsPage() {
  const queryClient = useQueryClient();
  const token = useSessionStore((state) => state.accessToken);
  const [loginInput, setLoginInput] = useState("");
  const telegramQuery = useTelegramConnectionQuery(token);

  const invalidateAll = async () => {
    await queryClient.invalidateQueries({ queryKey: ["telegram-connection"] });
  };

  const connectMutation = useMutation({
    mutationFn: () => telegramGateway.connect(token!),
    onSuccess: invalidateAll,
  });

  const reconnectMutation = useMutation({
    mutationFn: () => telegramGateway.reconnect(token!),
    onSuccess: invalidateAll,
  });

  const disconnectMutation = useMutation({
    mutationFn: () => telegramGateway.disconnect(token!),
    onSuccess: invalidateAll,
  });

  const loginInputMutation = useMutation({
    mutationFn: () => telegramGateway.submitLoginInput(token!, loginInput),
    onSuccess: async () => {
      setLoginInput("");
      await invalidateAll();
    },
  });

  const chatLoginPrompt = telegramQuery.data ? getChatLoginPrompt(telegramQuery.data.chatLoginStep) : null;

  return (
    <PageSection
      eyebrow="Подключения"
      title="Подключение Telegram"
      description="Здесь можно проверить состояние подключения и при необходимости переподключить его."
    >
      {!token ? (
        <article className="panel-card">
          <h3>Нужен вход</h3>
          <p>После входа здесь откроются настройки подключения и шаги для Telegram.</p>
          <div className="panel-actions">
            <Link to="/auth" className="primary-button">
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
        <>
          <article className="panel-card">
            <h3>Состояние</h3>
            <div className="info-list">
              <p><strong>Статус:</strong> {formatTelegramState(telegramQuery.data.state)}</p>
              <p><strong>Последняя синхронизация:</strong> {formatDate(telegramQuery.data.lastSyncedAt)}</p>
              <p><strong>Нужно действие:</strong> {telegramQuery.data.requiresAction ? "да" : "нет"}</p>
            </div>
          </article>

          <article className="panel-card">
            <h3>Действия</h3>
            <div className="connection-actions">
              <button className="primary-button" type="button" onClick={() => connectMutation.mutate()} disabled={connectMutation.isPending}>
                Подключить Telegram
              </button>
              <button className="ghost-button" type="button" onClick={() => reconnectMutation.mutate()} disabled={reconnectMutation.isPending}>
                Переподключить
              </button>
              <button className="ghost-button" type="button" onClick={() => disconnectMutation.mutate()} disabled={disconnectMutation.isPending}>
                Отключить
              </button>
            </div>
            {telegramQuery.data.requiresAction && !telegramQuery.data.chatLoginStep ? (
              <p className="form-note">Нажмите первую кнопку, чтобы начать подключение внутри сервиса.</p>
            ) : null}
            {connectMutation.isError ? <p className="form-error">{String(connectMutation.error.message)}</p> : null}
            {reconnectMutation.isError ? <p className="form-error">{String(reconnectMutation.error.message)}</p> : null}
            {disconnectMutation.isError ? <p className="form-error">{String(disconnectMutation.error.message)}</p> : null}
          </article>

          {telegramQuery.data.chatLoginStep ? (
            <form
              className="panel-card login-step-card"
              onSubmit={(event) => {
                event.preventDefault();
                loginInputMutation.mutate();
              }}
            >
              <h3>Вход в Telegram</h3>
              <p>{chatLoginPrompt}</p>
              <div className="search-form-row">
                <input
                  className="search-input"
                  type="text"
                  value={loginInput}
                  onChange={(event) => setLoginInput(event.target.value)}
                  placeholder="Введите данные для этого шага"
                />
                <button className="primary-button" type="submit" disabled={loginInputMutation.isPending}>
                  Отправить
                </button>
              </div>
              {loginInputMutation.isError ? <p className="form-error">{String(loginInputMutation.error.message)}</p> : null}
            </form>
          ) : null}
        </>
      ) : null}
    </PageSection>
  );
}
