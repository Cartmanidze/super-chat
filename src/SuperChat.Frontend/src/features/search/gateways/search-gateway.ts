import { api } from "../../../shared/services/api";
import { withBearer } from "../../../shared/services/auth-header";

export type SearchResult = {
  title: string;
  summary: string;
  kind: string;
  sourceRoom: string;
  observedAt: string;
  resolutionNote: string | null;
  resolutionConfidence: number | null;
};

export const searchGateway = {
  search(token: string, query: string) {
    const encoded = encodeURIComponent(query);

    return api.get<SearchResult[]>(`/search?q=${encoded}`, {
      headers: withBearer(token),
    });
  },
};
