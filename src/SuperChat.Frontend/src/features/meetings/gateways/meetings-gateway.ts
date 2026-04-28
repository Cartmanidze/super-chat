import { api } from "../../../shared/services/api";
import { withBearer } from "../../../shared/services/auth-header";

export type MeetingCard = {
  modelType: "meeting";
  id: string | null;
  title: string;
  summary: string;
  observedAt: string;
  dueAt: string | null;
  chatTitle: string;
  confidence: number;
  status: string | null;
  meetingProvider: string | null;
  meetingJoinUrl: string | null;
};

export const meetingsGateway = {
  list(token: string) {
    return api.get<MeetingCard[]>("/work-items/meetings", {
      headers: withBearer(token),
    });
  },
  confirm(token: string, id: string) {
    return api.post<void>(`/work-items/meetings/${id}/confirm`, undefined, {
      headers: withBearer(token),
    });
  },
  unconfirm(token: string, id: string) {
    return api.post<void>(`/work-items/meetings/${id}/unconfirm`, undefined, {
      headers: withBearer(token),
    });
  },
  complete(token: string, id: string) {
    return api.post<void>(`/work-items/meetings/${id}/complete`, undefined, {
      headers: withBearer(token),
    });
  },
  dismiss(token: string, id: string) {
    return api.post<void>(`/work-items/meetings/${id}/dismiss`, undefined, {
      headers: withBearer(token),
    });
  },
};
