import { useMutation } from "@tanstack/react-query";
import { Link, Outlet, useNavigate, useRouterState } from "@tanstack/react-router";
import { authGateway } from "../../features/auth/gateways/auth-gateway";
import { useSessionStore } from "../../features/auth/stores/session-store";

const links = [
  { to: "/", label: "Главная" },
  { to: "/today", label: "Встречи" },
  { to: "/search", label: "Поиск" },
  { to: "/settings/connections", label: "Подключения" },
  { to: "/feedback", label: "Отзыв" },
];

export function AppShell() {
  const navigate = useNavigate();
  const location = useRouterState({ select: (state) => state.location.pathname });
  const token = useSessionStore((state) => state.accessToken);
  const email = useSessionStore((state) => state.email);
  const clearSession = useSessionStore((state) => state.clearSession);

  const logoutMutation = useMutation({
    mutationFn: async () => {
      if (!token) {
        return;
      }

      await authGateway.logout(token);
    },
    onSettled: async () => {
      clearSession();
      await navigate({ to: "/auth" });
    },
  });

  const baseLinks = token ? links : [{ to: "/auth", label: "Вход" }];
  const navLinks = location.startsWith("/admin")
    ? [...baseLinks, { to: "/admin", label: "Админ" }]
    : baseLinks;

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand-block">
          <span className="brand-kicker">SuperChat</span>
          <h1>Ваши договорённости</h1>
          <p>{token ? "Встречи, поиск и подключения в одном месте." : "Войдите, чтобы увидеть свои встречи и разговоры."}</p>
        </div>

        <div className="session-panel">
          <div>
            <span className="eyebrow">Профиль</span>
            <p className="session-email">{email ?? "Гость"}</p>
          </div>
          {token ? (
            <button
              className="ghost-button shell-button"
              type="button"
              onClick={() => logoutMutation.mutate()}
              disabled={logoutMutation.isPending}
            >
              {logoutMutation.isPending ? "Выходим..." : "Выйти"}
            </button>
          ) : null}
        </div>

        <nav className="nav-grid" aria-label="Основная навигация">
          {navLinks.map((link) => (
            <Link
              key={link.to}
              to={link.to}
              className={`nav-link${location === link.to ? " is-active" : ""}`}
            >
              {link.label}
            </Link>
          ))}
        </nav>
      </aside>

      <main className="content-panel">
        <Outlet />
      </main>
    </div>
  );
}
