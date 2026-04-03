import { createRootRoute, createRoute, createRouter } from "@tanstack/react-router";
import { HomePage } from "../pages/home-page";
import { MeetingsPage } from "../pages/meetings-page";
import { SearchPage } from "../pages/search-page";
import { ConnectionsPage } from "../pages/connections-page";
import { FeedbackPage } from "../pages/feedback-page";
import { AdminPage } from "../pages/admin-page";
import { AuthPage } from "../pages/auth-page";
import { NotFoundPage } from "../pages/not-found-page";
import { AppShell } from "../shared/ui/app-shell";

const rootRoute = createRootRoute({
  component: AppShell,
  notFoundComponent: NotFoundPage,
});

const indexRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/",
  component: HomePage,
});

const authRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/auth",
  component: AuthPage,
});

const todayRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/today",
  component: MeetingsPage,
});

const searchRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/search",
  validateSearch: (search: Record<string, unknown>) => ({
    q: typeof search.q === "string" ? search.q : "",
  }),
  component: SearchPage,
});

const connectionsRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/settings/connections",
  component: ConnectionsPage,
});

const feedbackRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/feedback",
  validateSearch: (search: Record<string, unknown>) => ({
    area: typeof search.area === "string" ? search.area : "today",
    useful: typeof search.useful === "string" ? search.useful === "true" : true,
    note: typeof search.note === "string" ? search.note : "",
  }),
  component: FeedbackPage,
});

const adminRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/admin",
  component: AdminPage,
});

const routeTree = rootRoute.addChildren([
  indexRoute,
  authRoute,
  todayRoute,
  searchRoute,
  connectionsRoute,
  feedbackRoute,
  adminRoute,
]);

export const router = createRouter({ routeTree });

declare module "@tanstack/react-router" {
  interface Register {
    router: typeof router;
  }
}
