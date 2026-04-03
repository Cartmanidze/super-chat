import { useQuery } from "@tanstack/react-query";
import { telegramGateway } from "../gateways/telegram-gateway";

export function useTelegramConnectionQuery(token: string | null) {
  return useQuery({
    queryKey: ["telegram-connection", token],
    queryFn: () => telegramGateway.get(token!),
    enabled: Boolean(token),
  });
}
