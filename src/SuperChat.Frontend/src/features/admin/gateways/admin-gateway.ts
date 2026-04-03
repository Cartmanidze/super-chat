import { api } from "../../../shared/services/api";
import { withBearer } from "../../../shared/services/auth-header";

type AdminUnlockResponse = {
  unlocked: boolean;
};

export type AdminInvite = {
  email: string;
  invitedBy: string;
  invitedAt: string;
  isActive: boolean;
};

export type AdminInviteMutationResult = {
  succeeded: boolean;
  message: string;
};

function withAdminHeaders(token: string, password: string) {
  return {
    ...withBearer(token),
    "X-SuperChat-Admin-Password": password,
  };
}

export const adminGateway = {
  unlock(token: string, password: string) {
    return api.post<AdminUnlockResponse>("/admin/unlock", { password }, {
      headers: withBearer(token),
    });
  },
  getInvites(token: string, password: string) {
    return api.get<AdminInvite[]>("/admin/invites", {
      headers: withAdminHeaders(token, password),
    });
  },
  addInvite(token: string, password: string, email: string) {
    return api.post<AdminInviteMutationResult>("/admin/invites", { email }, {
      headers: withAdminHeaders(token, password),
    });
  },
};
