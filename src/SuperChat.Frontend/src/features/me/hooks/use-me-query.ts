import { useQuery } from "@tanstack/react-query";
import { meGateway } from "../gateways/me-gateway";

export function useMeQuery(token: string | null) {
  return useQuery({
    queryKey: ["me", token],
    queryFn: () => meGateway.getCurrent(token!),
    enabled: Boolean(token),
  });
}
