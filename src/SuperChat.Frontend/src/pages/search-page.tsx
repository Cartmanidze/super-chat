import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useSearch } from "@tanstack/react-router";
import { useSessionStore } from "../features/auth/stores/session-store";
import type { SearchResult } from "../features/search/gateways/search-gateway";
import { useSearchQuery } from "../features/search/hooks/use-search-query";
import { PageSection } from "../shared/ui/page-section";

function formatDate(value: string) {
  return new Date(value).toLocaleString("ru-RU", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export function SearchPage() {
  const navigate = useNavigate();
  const search = useSearch({ from: "/search" });
  const token = useSessionStore((state) => state.accessToken);
  const [query, setQuery] = useState(search.q);
  const [selectedIndex, setSelectedIndex] = useState(0);
  const searchQuery = useSearchQuery(token, search.q);

  useEffect(() => {
    setQuery(search.q);
  }, [search.q]);

  useEffect(() => {
    setSelectedIndex(0);
  }, [searchQuery.data]);

  const selected = useMemo<SearchResult | null>(() => {
    if (!searchQuery.data || searchQuery.data.length === 0) {
      return null;
    }

    return searchQuery.data[selectedIndex] ?? searchQuery.data[0];
  }, [searchQuery.data, selectedIndex]);

  return (
    <PageSection
      eyebrow="Поиск"
      title="Где это обсуждали?"
      description="Ищите по людям, темам и договорённостям."
    >
      {!token ? (
        <article className="panel-card">
          <h3>Нужен вход</h3>
          <p>После входа здесь станет доступен поиск по вашим разговорам и договорённостям.</p>
          <div className="panel-actions">
            <Link to="/auth" className="primary-button">
              Открыть вход
            </Link>
          </div>
        </article>
      ) : (
        <>
          <form
            className="panel-card search-toolbar-card"
            onSubmit={(event) => {
              event.preventDefault();
              void navigate({
                to: "/search",
                search: { q: query.trim() },
              });
            }}
          >
            <div className="search-form-row">
              <input
                className="search-input"
                type="text"
                value={query}
                onChange={(event) => setQuery(event.target.value)}
                placeholder="Например: договор, цена, Марина, Friday..."
              />
              <button className="primary-button" type="submit">
                Искать
              </button>
            </div>
          </form>

          {search.q.trim().length === 0 ? (
            <article className="panel-card">
              <h3>Введите запрос</h3>
              <p>Можно искать по имени, теме разговора или ключевой фразе.</p>
            </article>
          ) : null}

          {searchQuery.isLoading ? (
            <article className="panel-card">
              <h3>Ищем</h3>
              <p>Подождите немного.</p>
            </article>
          ) : null}

          {searchQuery.isError ? (
            <article className="panel-card">
              <h3>Не удалось выполнить поиск</h3>
              <p className="form-error">{String(searchQuery.error.message)}</p>
            </article>
          ) : null}

          {searchQuery.isSuccess && searchQuery.data.length === 0 ? (
            <article className="panel-card">
              <h3>Ничего не найдено</h3>
              <p>Попробуйте короче: по имени человека, по теме или по словам из сообщения.</p>
            </article>
          ) : null}

          {searchQuery.isSuccess && searchQuery.data.length > 0 ? (
            <div className="search-layout">
              <section className="search-list">
                {searchQuery.data.map((result, index) => (
                  <button
                    key={`${result.title}-${result.observedAt}-${index}`}
                    type="button"
                    className={`search-result-card${index === selectedIndex ? " is-selected" : ""}`}
                    onClick={() => setSelectedIndex(index)}
                  >
                    <div className="search-result-head">
                      <strong>{result.title}</strong>
                      <span>{formatDate(result.observedAt)}</span>
                    </div>
                    <p>{result.summary}</p>
                    {result.resolutionNote ? <p className="search-resolution-note">{result.resolutionNote}</p> : null}
                    <div className="meeting-meta">
                      <span>{result.kind}</span>
                      <span>{result.sourceRoom}</span>
                    </div>
                  </button>
                ))}
              </section>

              <aside className="panel-card search-detail-card">
                {selected ? (
                  <>
                    <div className="eyebrow">Подробности</div>
                    <h3>{selected.title}</h3>
                    <p>{selected.summary}</p>
                    <div className="info-list">
                      <p><strong>Тип:</strong> {selected.kind}</p>
                      <p><strong>Источник:</strong> {selected.sourceRoom}</p>
                      <p><strong>Время:</strong> {formatDate(selected.observedAt)}</p>
                      {selected.resolutionNote ? (
                        <p><strong>Итог:</strong> {selected.resolutionNote}</p>
                      ) : null}
                    </div>
                  </>
                ) : (
                  <p>Выберите результат слева.</p>
                )}
              </aside>
            </div>
          ) : null}
        </>
      )}
    </PageSection>
  );
}
