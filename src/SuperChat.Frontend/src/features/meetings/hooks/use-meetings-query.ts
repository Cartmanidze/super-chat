import { useQuery } from "@tanstack/react-query";
import { meetingsGateway } from "../gateways/meetings-gateway";

export function useMeetingsQuery(token: string | null) {
  return useQuery({
    queryKey: ["meetings", token],
    queryFn: () => meetingsGateway.list(token!),
    enabled: Boolean(token),
  });
}
