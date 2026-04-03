import { Link } from "@tanstack/react-router";

export function NotFoundPage() {
  return (
    <section className="page-section">
      <div className="section-heading">
        <span className="eyebrow">404</span>
        <h2>Такой страницы нет</h2>
        <p>Похоже, ссылка устарела или в адресе есть ошибка.</p>
      </div>

      <div className="shortcut-grid">
        <Link className="shortcut-card" to="/">
          <span className="shortcut-title">Вернуться на главную</span>
          <span className="shortcut-copy">Открыть главную страницу.</span>
        </Link>

        <Link className="shortcut-card" to="/today">
          <span className="shortcut-title">Открыть встречи</span>
          <span className="shortcut-copy">Перейти к ближайшим встречам и действиям.</span>
        </Link>
      </div>
    </section>
  );
}
