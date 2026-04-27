import { api, withBearer } from "./client";

export type SearchResult = {
  kind: string;
  title: string;
  summary: string;
  observedAt: string;
  sourceRoom: string;
  resolutionNote: string | null;
};

export const searchGateway = {
  async query(token: string, q: string): Promise<SearchResult[]> {
    const response = await api.get<SearchResult[]>("/search", {
      headers: withBearer(token),
      params: { q },
    });
    return response.data;
  },
};
