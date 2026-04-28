import { useMutation } from "@tanstack/react-query";
import { useNavigate, useSearch } from "@tanstack/react-router";
import { useSessionStore } from "../features/auth/stores/session-store";
import { feedbackGateway } from "../features/feedback/gateways/feedback-gateway";
import { PageSection } from "../shared/ui/page-section";

const areaOptions = [
  { value: "today", label: "Встречи" },
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
      title="Что улучшить?"
      description="Короткий отзыв помогает нам понять, где AI шумит, а где полезен."
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
          <div>
            <span className="eyebrow">Раздел</span>
            <div className="feedback-area-tabs" style={{ marginTop: 10 }}>
              {areaOptions.map((option) => (
                <button
                  key={option.value}
                  type="button"
                  className={area === option.value ? "is-active" : undefined}
                  onClick={() => {
                    void navigate({
                      to: "/feedback",
                      search: { area: option.value, useful, note },
                      replace: true,
                    });
                  }}
                >
                  {option.label}
                </button>
              ))}
            </div>
          </div>

          <div>
            <span className="eyebrow">Полезно?</span>
            <div className="feedback-toggle" style={{ marginTop: 10 }}>
              <button
                type="button"
                className={useful ? "is-active" : undefined}
                onClick={() => {
                  void navigate({
                    to: "/feedback",
                    search: { area, useful: true, note },
                    replace: true,
                  });
                }}
              >
                Да, полезно
              </button>
              <button
                type="button"
                className={!useful ? "is-active" : undefined}
                onClick={() => {
                  void navigate({
                    to: "/feedback",
                    search: { area, useful: false, note },
                    replace: true,
                  });
                }}
              >
                Нет
              </button>
            </div>
          </div>

          <label className="field" style={{ marginBottom: 0 }}>
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
              placeholder="Коротко опишите, что работает и что мешает."
            />
          </label>

          <div className="connection-actions">
            <button className="btn is-primary" type="submit" disabled={feedbackMutation.isPending}>
              {feedbackMutation.isPending ? "Отправляем…" : "Отправить отзыв"}
            </button>
            <button
              type="button"
              className="btn is-ghost"
              onClick={() => {
                void navigate({
                  to: "/feedback",
                  search: { area, useful, note: "" },
                  replace: true,
                });
              }}
            >
              Очистить
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
