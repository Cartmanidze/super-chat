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

function formatDate(value: string | null) {
  if (!value) {
    return "Еще не было синхронизации";
  }

  return new Date(value).toLocaleString("ru-RU", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export function ConnectionsPage() {
  const queryClient = useQueryClient();
  const token = useSessionStore((state) => state.accessToken);
  const [loginInput, setLoginInput] = useState("");
  const [submittedStep, setSubmittedStep] = useState<TelegramLoginStep | null>(null);
  const telegramQuery = useTelegramConnectionQuery(token);
  const rawChatLoginStep = telegramQuery.data?.chatLoginStep ?? null;
  const chatLoginStep = isTelegramLoginStep(rawChatLoginStep) ? rawChatLoginStep : null;
  const activeSubmittedStep = submittedStep === chatLoginStep ? submittedStep : null;
  const visibleLoginInput =
    submittedStep && submittedStep !== chatLoginStep ? "" : loginInput;

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
              <p>
                <strong>Статус:</strong> {formatTelegramState(telegramQuery.data.state)}
              </p>
              <p>
                <strong>Последняя синхронизация:</strong> {formatDate(telegramQuery.data.lastSyncedAt)}
              </p>
              <p>
                <strong>Нужно действие:</strong> {telegramQuery.data.requiresAction ? "да" : "нет"}
              </p>
            </div>
          </article>

          <article className="panel-card">
            <h3>Действия</h3>
            <div className="connection-actions">
              <button
                className="primary-button"
                type="button"
                onClick={() => connectMutation.mutate()}
                disabled={connectMutation.isPending}
              >
                Подключить Telegram
              </button>
              <button
                className="ghost-button"
                type="button"
                onClick={() => reconnectMutation.mutate()}
                disabled={reconnectMutation.isPending}
              >
                Переподключить
              </button>
              <button
                className="ghost-button"
                type="button"
                onClick={() => disconnectMutation.mutate()}
                disabled={disconnectMutation.isPending}
              >
                Отключить
              </button>
            </div>
            {telegramQuery.data.requiresAction && !chatLoginStep ? (
              <p className="form-note">
                Нажмите первую кнопку, чтобы начать подключение внутри сервиса.
              </p>
            ) : null}
            {connectMutation.isError ? <p className="form-error">{String(connectMutation.error.message)}</p> : null}
            {reconnectMutation.isError ? (
              <p className="form-error">{String(reconnectMutation.error.message)}</p>
            ) : null}
            {disconnectMutation.isError ? (
              <p className="form-error">{String(disconnectMutation.error.message)}</p>
            ) : null}
          </article>

          {chatLoginStep ? (
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
        </>
      ) : null}
    </PageSection>
  );
}
