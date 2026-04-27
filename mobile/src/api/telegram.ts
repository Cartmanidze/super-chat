import { api, withBearer } from "./client";

export type TelegramConnection = {
  state: string;
  matrixUserId: string | null;
  chatLoginStep: "phone" | "code" | "password" | string | null;
  lastSyncedAt: string | null;
  requiresAction: boolean;
};

export const telegramGateway = {
  async get(token: string): Promise<TelegramConnection> {
    const response = await api.get<TelegramConnection>("/integrations/telegram", {
      headers: withBearer(token),
    });
    return response.data;
  },
  async connect(token: string): Promise<TelegramConnection> {
    const response = await api.post<TelegramConnection>("/integrations/telegram/connect", undefined, {
      headers: withBearer(token),
    });
    return response.data;
  },
  async reconnect(token: string): Promise<TelegramConnection> {
    const response = await api.post<TelegramConnection>("/integrations/telegram/reconnect", undefined, {
      headers: withBearer(token),
    });
    return response.data;
  },
  async submitLoginInput(token: string, input: string): Promise<TelegramConnection> {
    const response = await api.post<TelegramConnection>(
      "/integrations/telegram/login-input",
      { input },
      { headers: withBearer(token) },
    );
    return response.data;
  },
  async disconnect(token: string): Promise<TelegramConnection> {
    const response = await api.delete<TelegramConnection>("/integrations/telegram", {
      headers: withBearer(token),
    });
    return response.data;
  },
};
