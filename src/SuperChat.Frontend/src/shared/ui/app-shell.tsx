import { useMutation } from "@tanstack/react-query";
import { Link, Outlet, useNavigate, useRouterState } from "@tanstack/react-router";
import { authGateway } from "../../features/auth/gateways/auth-gateway";
import { useSessionStore } from "../../features/auth/stores/session-store";
import { avatarInitials } from "../lib/avatar";
import { BrandMark } from "./brand-mark";
import { DateChip } from "./date-chip";

const PRIMARY_LINKS = [
  { to: "/", label: "Главная" },
  { to: "/today", label: "Встречи" },
  { to: "/search", label: "Поиск" },
  { to: "/settings/connections", label: "Подключения" },
  { to: "/feedback", label: "Отзыв" },
] as const;

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

  const isAuthed = Boolean(token);
  const showAdmin = location.startsWith("/admin");
  const navLinks = isAuthed
    ? showAdmin
      ? [...PRIMARY_LINKS, { to: "/admin", label: "Админ" } as const]
      : PRIMARY_LINKS
    : [{ to: "/auth", label: "Вход" } as const];

  const avatarInitialsValue = isAuthed ? avatarInitials(email, "Я") : "Гость";

  return (
    <div className="app-shell">
      <header className="topbar">
        <Link to="/" className="topbar-brand" aria-label="Super Chat — на главную">
          <BrandMark size={18} />
          <div>
            <div className="brand-word">
              Super<em>Chat</em>
            </div>
            <div className="brand-sub">Встречи · Вовремя и в курсе</div>
          </div>
        </Link>

        <nav className="topbar-nav" aria-label="Основная навигация">
          {navLinks.map((link) => {
            const active =
              link.to === "/"
                ? location === "/"
                : location === link.to || location.startsWith(`${link.to}/`);
            return (
              <Link key={link.to} to={link.to} className={active ? "is-active" : undefined}>
                {link.label}
              </Link>
            );
          })}
        </nav>

        <div className="topbar-right">
          <DateChip />
          {isAuthed ? (
            <>
              <div className="avatar" aria-label={email ?? "Профиль"} title={email ?? undefined}>
                {avatarInitialsValue}
              </div>
              <button
                type="button"
                className="topbar-logout"
                onClick={() => logoutMutation.mutate()}
                disabled={logoutMutation.isPending}
              >
                {logoutMutation.isPending ? "Выходим..." : "Выйти"}
              </button>
            </>
          ) : (
            <div className="avatar is-ghost" aria-label="Гость">
              ·
            </div>
          )}
        </div>
      </header>

      <main className="app-shell-main">
        <Outlet />
      </main>
    </div>
  );
}
