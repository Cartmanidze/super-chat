import type { TelegramConnection } from "../gateways/telegram-gateway";

export const TELEGRAM_CONNECTION_REFETCH_INTERVAL_MS = 3000;

export function getTelegramConnectionRefetchInterval(
  connection: Pick<TelegramConnection, "state" | "requiresAction"> | undefined,
) {
  return connection?.state === "Pending" && connection.requiresAction
    ? TELEGRAM_CONNECTION_REFETCH_INTERVAL_MS
    : false;
}
