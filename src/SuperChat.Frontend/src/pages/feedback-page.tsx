import { useMutation } from "@tanstack/react-query";
import { useNavigate, useSearch } from "@tanstack/react-router";
import { useSessionStore } from "../features/auth/stores/session-store";
import { feedbackGateway } from "../features/feedback/gateways/feedback-gateway";
import { PageSection } from "../shared/ui/page-section";

const areaOptions = [
  { value: "today", label: "Встречи" },
  { value: "search", label: "Поиск" },
  { value: "connections", label: "Подключения" },
];

export function FeedbackPage() {
  const navigate = useNavigate();
  const search = useSearch({ from: "/feedback" });
  const token = useSessionStore((state) => state.accessToken);
  const area = search.area;
  const useful = search.useful;
  const note = search.note;

  const feedbackMutation = useMutation({
    mutationFn: () => feedbackGateway.submit(token!, area, useful, note),
  });

  return (
    <PageSection
      eyebrow="Отзыв"
      title="Обратная связь"
      description="Расскажите, что было полезно, а что стоит поправить."
    >
      {!token ? (
        <article className="panel-card">
          <h3>Нужен вход</h3>
          <p>Сначала войдите, потом можно будет отправить отзыв.</p>
        </article>
      ) : (
        <form
          className="panel-card feedback-form"
          onSubmit={(event) => {
            event.preventDefault();
            feedbackMutation.mutate();
          }}
        >
          <div className="field">
            <span>Раздел</span>
            <select
              className="search-input"
              value={area}
              onChange={(event) => {
                void navigate({
                  to: "/feedback",
                  search: { area: event.target.value, useful, note },
                  replace: true,
                });
              }}
            >
              {areaOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </div>

          <div className="field">
            <span>Полезно?</span>
            <select
              className="search-input"
              value={useful ? "true" : "false"}
              onChange={(event) => {
                void navigate({
                  to: "/feedback",
                  search: { area, useful: event.target.value === "true", note },
                  replace: true,
                });
              }}
            >
              <option value="true">Да</option>
              <option value="false">Нет</option>
            </select>
          </div>

          <div className="field">
            <span>Комментарий</span>
            <textarea
              className="feedback-textarea"
              rows={6}
              value={note}
              onChange={(event) => {
                void navigate({
                  to: "/feedback",
                  search: { area, useful, note: event.target.value },
                  replace: true,
                });
              }}
              placeholder="Что было полезно или что сработало плохо?"
            />
          </div>

          <div className="connection-actions">
            <button className="primary-button" type="submit" disabled={feedbackMutation.isPending}>
              {feedbackMutation.isPending ? "Отправляем..." : "Отправить отзыв"}
            </button>
          </div>

          {feedbackMutation.isSuccess ? (
            <p className="form-note">Спасибо, отзыв сохранен.</p>
          ) : null}
          {feedbackMutation.isError ? (
            <p className="form-error">{String(feedbackMutation.error.message)}</p>
          ) : null}
        </form>
      )}
    </PageSection>
  );
}
