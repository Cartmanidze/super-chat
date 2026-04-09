import { api } from "../../../shared/services/api";
import { withBearer } from "../../../shared/services/auth-header";

export type SendCodeResponse = {
  message: string;
};

export type SessionTokenResponse = {
  accessToken: string;
  tokenType: string;
  expiresAt: string;
  user: {
    id: string;
    email: string;
  };
};

export const authGateway = {
  sendCode(email: string) {
    return api.post<SendCodeResponse>("/auth/send-code", { email });
  },
  verifyCode(email: string, code: string, timeZoneId?: string) {
    return api.post<SessionTokenResponse>("/auth/verify-code", { email, code, timeZoneId });
  },
  logout(token: string) {
    return api.post<void>("/auth/logout", undefined, {
      headers: withBearer(token),
    });
  },
};
