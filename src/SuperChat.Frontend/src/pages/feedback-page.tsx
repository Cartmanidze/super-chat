import { useMutation } from "@tanstack/react-query";
import { useEffect, useState } from "react";
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
  const [area, setArea] = useState(search.area);
  const [useful, setUseful] = useState(search.useful);
  const [note, setNote] = useState(search.note);

  useEffect(() => {
    setArea(search.area);
    setUseful(search.useful);
    setNote(search.note);
  }, [search.area, search.useful, search.note]);

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
                const nextArea = event.target.value;
                setArea(nextArea);
                void navigate({
                  to: "/feedback",
                  search: { area: nextArea, useful, note },
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
                const nextUseful = event.target.value === "true";
                setUseful(nextUseful);
                void navigate({
                  to: "/feedback",
                  search: { area, useful: nextUseful, note },
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
                const nextNote = event.target.value;
                setNote(nextNote);
                void navigate({
                  to: "/feedback",
                  search: { area, useful, note: nextNote },
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
            <p className="form-note">Спасибо, отзыв сохранён.</p>
          ) : null}
          {feedbackMutation.isError ? (
            <p className="form-error">{String(feedbackMutation.error.message)}</p>
          ) : null}
        </form>
      )}
    </PageSection>
  );
}
