import { Link, useNavigate, useSearch } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { useSessionStore } from "../features/auth/stores/session-store";
import type { SearchResult } from "../features/search/gateways/search-gateway";
import { useSearchQuery } from "../features/search/hooks/use-search-query";
import { formatDateShort } from "../shared/lib/relative-time";
import { PageSection } from "../shared/ui/page-section";

type SearchPageContentProps = {
  token: string;
  searchQueryValue: string;
};

function SearchPageContent({ token, searchQueryValue }: SearchPageContentProps) {
  const navigate = useNavigate();
  const [query, setQuery] = useState(searchQueryValue);
  const [selectedIndex, setSelectedIndex] = useState(0);
  const searchQuery = useSearchQuery(token, searchQueryValue);

  const selected = useMemo<SearchResult | null>(() => {
    if (!searchQuery.data || searchQuery.data.length === 0) {
      return null;
    }
    return searchQuery.data[selectedIndex] ?? searchQuery.data[0];
  }, [searchQuery.data, selectedIndex]);

  return (
    <>
      <form
        className="panel-card search-toolbar"
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
            placeholder="Например: договор, цена, Марина, Friday…"
          />
          <button className="btn is-primary" type="submit">
            Искать
          </button>
        </div>
      </form>

      {searchQueryValue.trim().length === 0 ? (
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
          <p>Попробуйте короче: по имени, по теме или по словам из сообщения.</p>
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
                  <span>{formatDateShort(result.observedAt)}</span>
                </div>
                <span>{result.sourceRoom}</span>
                <p>{result.summary}</p>
                {result.resolutionNote ? (
                  <p className="search-resolution-note">{result.resolutionNote}</p>
                ) : null}
                <div className="tl-meta">
                  <span className="src">{result.kind}</span>
                  <span className="dot" />
                  <span>{result.sourceRoom}</span>
                </div>
              </button>
            ))}
          </section>

          <aside className="panel-card search-detail-card">
            {selected ? (
              <>
                <span className="eyebrow">Контекст</span>
                <h3>{selected.title}</h3>
                <div className="search-detail-chips">
                  <span className="pill is-kind">{selected.kind}</span>
                  <span className="pill is-neutral">{selected.sourceRoom}</span>
                  <span className="pill is-muted">{formatDateShort(selected.observedAt)}</span>
                </div>
                <p>{selected.summary}</p>
                {selected.resolutionNote ? (
                  <p className="search-resolution-note">
                    <strong>Итог:</strong> {selected.resolutionNote}
                  </p>
                ) : null}
              </>
            ) : (
              <p>Выберите результат слева.</p>
            )}
          </aside>
        </div>
      ) : null}
    </>
  );
}

export function SearchPage() {
  const token = useSessionStore((state) => state.accessToken);
  const search = useSearch({ from: "/search" });

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
            <Link to="/auth" className="btn is-primary">
              Открыть вход
            </Link>
          </div>
        </article>
      ) : (
        <SearchPageContent key={search.q} token={token} searchQueryValue={search.q} />
      )}
    </PageSection>
  );
}
