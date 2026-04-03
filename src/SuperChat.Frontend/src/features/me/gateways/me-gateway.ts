import { api } from "../../../shared/services/api";
import { withBearer } from "../../../shared/services/auth-header";

export type MeResponse = {
  id: string;
  email: string;
  matrixUserId: string | null;
  telegramState: string;
  lastSyncedAt: string | null;
  requiresTelegramAction: boolean;
};

export const meGateway = {
  getCurrent(token: string) {
    return api.get<MeResponse>("/me", {
      headers: withBearer(token),
    });
  },
};
