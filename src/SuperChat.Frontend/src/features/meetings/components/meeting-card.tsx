import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { avatarInitials, avatarTint } from "../../../shared/lib/avatar";
import { formatClockTime, relativeTimeTo } from "../../../shared/lib/relative-time";
import type { MeetingCard as MeetingCardModel } from "../gateways/meetings-gateway";
import { meetingsGateway } from "../gateways/meetings-gateway";

type MeetingCardProps = {
  token: string;
  card: MeetingCardModel;
};

function formatConfidence(value: number) {
  return `${Math.round(Math.max(0, Math.min(1, value)) * 100)}%`;
}

function statusPill(status: string | null) {
  switch (status) {
    case "Confirmed":
      return { label: "Подтверждена", className: "pill is-success" };
    case "PendingConfirmation":
      return { label: "Ждёт подтверждения", className: "pill is-warn" };
    case "Rescheduled":
      return { label: "Перенесена", className: "pill is-neutral" };
    case "Cancelled":
      return { label: "Отменена", className: "pill is-muted" };
    default:
      return null;
  }
}

function phaseFor(card: MeetingCardModel, now: Date): "past" | "live" | "upcoming" {
  const raw = card.dueAt ?? card.observedAt;
  if (!raw) return "upcoming";
  const at = new Date(raw);
  if (Number.isNaN(at.getTime())) return "upcoming";
  const rel = relativeTimeTo(at, now);
  if (rel.phase === "past") return "past";
  if (rel.phase === "live") return "live";
  return "upcoming";
}

export function MeetingCard({ token, card }: MeetingCardProps) {
  const queryClient = useQueryClient();
  const canConfirm = card.status === "PendingConfirmation";
  const canUnconfirm = card.status === "Confirmed";

  const [now, setNow] = useState(() => new Date());
  useEffect(() => {
    const timer = setInterval(() => setNow(new Date()), 60_000);
    return () => clearInterval(timer);
  }, []);

  const phase = phaseFor(card, now);
  const at = card.dueAt ? new Date(card.dueAt) : card.observedAt ? new Date(card.observedAt) : null;
  const rel = at ? relativeTimeTo(at, now) : null;

  const cardClass = phase === "live"
    ? "meeting-card is-live"
    : phase === "past"
      ? "meeting-card is-past"
      : "meeting-card";

  const kindPillLabel = phase === "live"
    ? "⚡ Сейчас"
    : phase === "past"
      ? "✓ Прошла"
      : rel
        ? rel.label
        : "Встреча";
  const kindPillClass = phase === "live"
    ? "pill is-live"
    : phase === "past"
      ? "pill is-muted"
      : "pill is-neutral";

  const status = statusPill(card.status);

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

  const faceName = card.chatTitle ?? "";

  return (
    <article className={cardClass}>
      <div className="meeting-card-row">
        <span className={kindPillClass}>{kindPillLabel}</span>
        <span className="pill is-gold">{formatConfidence(card.confidence)}</span>
      </div>

      <h3>{card.title}</h3>
      <p className="sub">{card.summary}</p>

      {at ? (
        <div className="when">
          <span className="when-big">{formatClockTime(at)}</span>
          <span className="when-lbl">
            {[card.meetingProvider, rel ? rel.label : null].filter(Boolean).join(" · ")}
          </span>
        </div>
      ) : null}

      {faceName ? (
        <div className="participants">
          <div className={`pface ${avatarTint(faceName)}`}>{avatarInitials(faceName, "·")}</div>
        </div>
      ) : null}

      {status ? (
        <div className="meeting-card-row">
          <span className={status.className}>{status.label}</span>
        </div>
      ) : null}

      <div className="meeting-foot">
        <span className="src">{card.chatTitle}</span>
        <div className="meeting-actions">
          {card.meetingJoinUrl ? (
            <a className="btn is-primary is-sm" href={card.meetingJoinUrl} target="_blank" rel="noreferrer">
              Открыть ссылку
            </a>
          ) : null}
          {card.id ? (
            <>
              {canConfirm ? (
                <button
                  type="button"
                  className="btn is-sm"
                  onClick={() => confirmMutation.mutate()}
                  disabled={isActionPending}
                >
                  Подтвердить
                </button>
              ) : null}
              {canUnconfirm ? (
                <button
                  type="button"
                  className="btn is-sm"
                  onClick={() => unconfirmMutation.mutate()}
                  disabled={isActionPending}
                >
                  Снять
                </button>
              ) : null}
              <button
                type="button"
                className="btn is-sm"
                onClick={() => completeMutation.mutate()}
                disabled={isActionPending}
              >
                Завершить
              </button>
              <button
                type="button"
                className="btn is-sm"
                onClick={() => dismissMutation.mutate()}
                disabled={isActionPending}
              >
                Скрыть
              </button>
            </>
          ) : null}
        </div>
      </div>

      {completeMutation.isError ? <p className="form-error">{String(completeMutation.error.message)}</p> : null}
      {dismissMutation.isError ? <p className="form-error">{String(dismissMutation.error.message)}</p> : null}
      {confirmMutation.isError ? <p className="form-error">{String(confirmMutation.error.message)}</p> : null}
      {unconfirmMutation.isError ? <p className="form-error">{String(unconfirmMutation.error.message)}</p> : null}
    </article>
  );
}
