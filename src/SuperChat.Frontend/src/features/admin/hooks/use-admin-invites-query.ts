import { useQuery } from "@tanstack/react-query";
import { adminGateway } from "../gateways/admin-gateway";

export function useAdminInvitesQuery(token: string | null, password: string) {
  return useQuery({
    queryKey: ["admin-invites", token, password],
    queryFn: () => adminGateway.getInvites(token!, password),
    enabled: Boolean(token) && password.trim().length > 0,
  });
}
