import { api, withBearer } from "./client";

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
  async list(token: string): Promise<MeetingCard[]> {
    const response = await api.get<MeetingCard[]>("/work-items/meetings", { headers: withBearer(token) });
    return response.data;
  },
  async confirm(token: string, id: string): Promise<void> {
    await api.post(`/work-items/meetings/${id}/confirm`, undefined, { headers: withBearer(token) });
  },
  async unconfirm(token: string, id: string): Promise<void> {
    await api.post(`/work-items/meetings/${id}/unconfirm`, undefined, { headers: withBearer(token) });
  },
  async complete(token: string, id: string): Promise<void> {
    await api.post(`/work-items/meetings/${id}/complete`, undefined, { headers: withBearer(token) });
  },
  async dismiss(token: string, id: string): Promise<void> {
    await api.post(`/work-items/meetings/${id}/dismiss`, undefined, { headers: withBearer(token) });
  },
};
