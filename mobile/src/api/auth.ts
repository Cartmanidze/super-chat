import { api, withBearer } from "./client";

export type AuthSession = {
  accessToken: string;
  tokenType: string;
  expiresAt: string;
  user: {
    id: string;
    email: string;
  };
};

export const authGateway = {
  async sendCode(email: string): Promise<{ message: string }> {
    const response = await api.post("/auth/send-code", { email });
    return response.data;
  },
  async verifyCode(email: string, code: string, timeZoneId?: string): Promise<AuthSession> {
    const response = await api.post("/auth/verify-code", { email, code, timeZoneId });
    return response.data;
  },
  async logout(token: string): Promise<void> {
    await api.post("/auth/logout", undefined, { headers: withBearer(token) });
  },
};
