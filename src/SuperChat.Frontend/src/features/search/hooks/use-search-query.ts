import { useQuery } from "@tanstack/react-query";
import { searchGateway } from "../gateways/search-gateway";

export function useSearchQuery(token: string | null, query: string) {
  return useQuery({
    queryKey: ["search", token, query],
    queryFn: () => searchGateway.search(token!, query),
    enabled: Boolean(token) && query.trim().length > 0,
  });
}
