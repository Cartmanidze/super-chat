import { useQuery } from "@tanstack/react-query";
import { telegramGateway } from "../gateways/telegram-gateway";
import { getTelegramConnectionRefetchInterval } from "./telegram-connection-polling";

export function useTelegramConnectionQuery(token: string | null) {
  return useQuery({
    queryKey: ["telegram-connection", token],
    queryFn: () => telegramGateway.get(token!),
    enabled: Boolean(token),
    refetchInterval: (query) => getTelegramConnectionRefetchInterval(query.state.data),
  });
}
