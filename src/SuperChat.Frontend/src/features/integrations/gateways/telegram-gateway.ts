import { api } from "../../../shared/services/api";
import { withBearer } from "../../../shared/services/auth-header";

export type TelegramConnection = {
  state: string;
  matrixUserId: string | null;
  webLoginUrl: string | null;
  chatLoginStep: string | null;
  lastSyncedAt: string | null;
  requiresAction: boolean;
};

export const telegramGateway = {
  get(token: string) {
    return api.get<TelegramConnection>("/integrations/telegram", {
      headers: withBearer(token),
    });
  },
  connect(token: string) {
    return api.post<TelegramConnection>("/integrations/telegram/connect", undefined, {
      headers: withBearer(token),
    });
  },
  reconnect(token: string) {
    return api.post<TelegramConnection>("/integrations/telegram/reconnect", undefined, {
      headers: withBearer(token),
    });
  },
  submitLoginInput(token: string, input: string) {
    return api.post<TelegramConnection>("/integrations/telegram/login-input", { input }, {
      headers: withBearer(token),
    });
  },
  disconnect(token: string) {
    return api.delete<TelegramConnection>("/integrations/telegram", {
      headers: withBearer(token),
    });
  },
};
