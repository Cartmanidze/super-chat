import { api } from "../../../shared/services/api";
import { withBearer } from "../../../shared/services/auth-header";

export type FeedbackResponse = {
  status: string;
};

export const feedbackGateway = {
  submit(token: string, area: string, useful: boolean, note: string) {
    return api.post<FeedbackResponse>("/feedback", { area, useful, note: note || null }, {
      headers: withBearer(token),
    });
  },
};
