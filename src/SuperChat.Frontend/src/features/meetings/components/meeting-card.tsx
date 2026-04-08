import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { MeetingCard as MeetingCardModel } from "../gateways/meetings-gateway";
import { meetingsGateway } from "../gateways/meetings-gateway";

type MeetingCardProps = {
  token: string;
  card: MeetingCardModel;
};

function formatDate(value: string | null) {
  if (!value) {
    return "Время не указано";
  }

  return new Date(value).toLocaleString("ru-RU", {
    day: "2-digit",
    month: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function formatConfidence(value: number) {
  return `${Math.round(Math.max(0, Math.min(1, value)) * 100)}%`;
}

function formatStatus(value: string | null) {
  switch (value) {
    case "PendingConfirmation":
      return "Ждет подтверждения";
    case "Confirmed":
      return "Подтверждена";
    case "Rescheduled":
      return "Перенесена";
    case "Cancelled":
      return "Отменена";
    default:
      return value ?? "Неизвестно";
  }
}

export function MeetingCard({ token, card }: MeetingCardProps) {
  const queryClient = useQueryClient();
  const canConfirm = card.status === "PendingConfirmation";
  const canUnconfirm = card.status === "Confirmed";

  const completeMutation = useMutation({
    mutationFn: () => meetingsGateway.complete(token, card.id!),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["meetings"] });
    },
  });

  const dismissMutation = useMutation({
    mutationFn: () => meetingsGateway.dismiss(token, card.id!),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["meetings"] });
    },
  });

  const confirmMutation = useMutation({
    mutationFn: () => meetingsGateway.confirm(token, card.id!),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["meetings"] });
    },
  });

  const unconfirmMutation = useMutation({
    mutationFn: () => meetingsGateway.unconfirm(token, card.id!),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["meetings"] });
    },
  });

  const isActionPending =
    completeMutation.isPending ||
    dismissMutation.isPending ||
    confirmMutation.isPending ||
    unconfirmMutation.isPending;

  return (
    <article className="meeting-card">
      <div className="meeting-card-head">
        <span className="kind-pill">Meeting</span>
        <span className="confidence-pill">{formatConfidence(card.confidence)}</span>
      </div>

      <h3>{card.title}</h3>
      <p>{card.summary}</p>

      <div className="meeting-meta">
        <span>{card.sourceRoom}</span>
        <span>{formatDate(card.dueAt ?? card.observedAt)}</span>
      </div>

      <div className="meeting-meta">
        <span>Статус: {formatStatus(card.status)}</span>
        <span>Провайдер: {card.meetingProvider ?? "n/a"}</span>
      </div>

      <div className="meeting-actions">
        {card.meetingJoinUrl ? (
          <a className="ghost-button" href={card.meetingJoinUrl} target="_blank" rel="noreferrer">
            Открыть ссылку
          </a>
        ) : null}
        {card.id ? (
          <>
            {canConfirm ? (
              <button
                className="ghost-button"
                type="button"
                onClick={() => confirmMutation.mutate()}
                disabled={isActionPending}
              >
                Подтвердить
              </button>
            ) : null}
            {canUnconfirm ? (
              <button
                className="ghost-button"
                type="button"
                onClick={() => unconfirmMutation.mutate()}
                disabled={isActionPending}
              >
                Снять подтверждение
              </button>
            ) : null}
            <button
              className="ghost-button"
              type="button"
              onClick={() => completeMutation.mutate()}
              disabled={isActionPending}
            >
              Завершить
            </button>
            <button
              className="ghost-button"
              type="button"
              onClick={() => dismissMutation.mutate()}
              disabled={isActionPending}
            >
              Скрыть
            </button>
          </>
        ) : null}
      </div>

      {completeMutation.isError ? <p className="form-error">{String(completeMutation.error.message)}</p> : null}
      {dismissMutation.isError ? <p className="form-error">{String(dismissMutation.error.message)}</p> : null}
      {confirmMutation.isError ? <p className="form-error">{String(confirmMutation.error.message)}</p> : null}
      {unconfirmMutation.isError ? <p className="form-error">{String(unconfirmMutation.error.message)}</p> : null}
    </article>
  );
}
