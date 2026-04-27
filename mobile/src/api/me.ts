import { api, withBearer } from "./client";

export type MeResponse = {
  id: string;
  email: string;
  telegramState: string;
  lastSyncedAt: string | null;
  requiresTelegramAction: boolean;
};

export const meGateway = {
  async get(token: string): Promise<MeResponse> {
    const response = await api.get("/me", { headers: withBearer(token) });
    return response.data;
  },
};
