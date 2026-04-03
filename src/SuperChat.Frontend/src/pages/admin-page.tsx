import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useSessionStore } from "../features/auth/stores/session-store";
import { adminGateway } from "../features/admin/gateways/admin-gateway";
import { useAdminInvitesQuery } from "../features/admin/hooks/use-admin-invites-query";
import { useMeQuery } from "../features/me/hooks/use-me-query";
import { PageSection } from "../shared/ui/page-section";

function formatDate(value: string) {
  return new Date(value).toLocaleString("ru-RU", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  });
}

export function AdminPage() {
  const queryClient = useQueryClient();
  const token = useSessionStore((state) => state.accessToken);
  const [password, setPassword] = useState("");
  const [inviteEmail, setInviteEmail] = useState("");
  const [unlockedPassword, setUnlockedPassword] = useState("");
  const meQuery = useMeQuery(token);
  const invitesQuery = useAdminInvitesQuery(token, unlockedPassword);

  const unlockMutation = useMutation({
    mutationFn: () => adminGateway.unlock(token!, password),
    onSuccess: () => {
      setUnlockedPassword(password);
    },
  });

  const addInviteMutation = useMutation({
    mutationFn: () => adminGateway.addInvite(token!, unlockedPassword, inviteEmail),
    onSuccess: async () => {
      setInviteEmail("");
      await queryClient.invalidateQueries({ queryKey: ["admin-invites"] });
    },
  });

  return (
    <PageSection
      eyebrow="Служебный раздел"
      title="Приглашения"
      description="Здесь можно открыть служебный раздел и управлять приглашениями."
    >
      {!token ? (
        <article className="panel-card">
          <h3>Нужен вход</h3>
          <p>Сначала войдите, потом откроется этот раздел.</p>
        </article>
      ) : null}

      {token && meQuery.isSuccess ? (
        <article className="panel-card">
          <h3>Текущий пользователь</h3>
          <p><strong>Email:</strong> {meQuery.data.email}</p>
          <p className="form-note">Доступ проверяется по вашей почте и служебному паролю.</p>
        </article>
      ) : null}

      {token && !unlockedPassword ? (
        <form
          className="panel-card feedback-form"
          onSubmit={(event) => {
            event.preventDefault();
            unlockMutation.mutate();
          }}
        >
          <h3>Открыть раздел</h3>
          <div className="field">
            <span>Служебный пароль</span>
            <input
              className="search-input"
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              placeholder="Введите пароль"
            />
          </div>
          <div className="connection-actions">
            <button className="primary-button" type="submit" disabled={unlockMutation.isPending}>
              {unlockMutation.isPending ? "Проверяем..." : "Открыть"}
            </button>
          </div>
          {unlockMutation.isError ? <p className="form-error">{String(unlockMutation.error.message)}</p> : null}
        </form>
      ) : null}

      {token && unlockedPassword ? (
        <>
          <article className="panel-card feedback-form">
            <h3>Добавить приглашение</h3>
            <div className="search-form-row">
              <input
                className="search-input"
                type="email"
                value={inviteEmail}
                onChange={(event) => setInviteEmail(event.target.value)}
                placeholder="name@example.com"
              />
              <button className="primary-button" type="button" onClick={() => addInviteMutation.mutate()} disabled={addInviteMutation.isPending}>
                Добавить
              </button>
            </div>
            {addInviteMutation.isSuccess ? <p className="form-note">{addInviteMutation.data.message}</p> : null}
            {addInviteMutation.isError ? <p className="form-error">{String(addInviteMutation.error.message)}</p> : null}
          </article>

          {invitesQuery.isLoading ? (
            <article className="panel-card">
              <h3>Загружаем приглашения</h3>
              <p>Подождите немного.</p>
            </article>
          ) : null}

          {invitesQuery.isError ? (
            <article className="panel-card">
              <h3>Не удалось открыть список</h3>
              <p className="form-error">{String(invitesQuery.error.message)}</p>
            </article>
          ) : null}

          {invitesQuery.isSuccess ? (
            <article className="panel-card">
              <h3>Список приглашений</h3>
              {invitesQuery.data.length === 0 ? (
                <p>Список пока пуст.</p>
              ) : (
                <div className="admin-list">
                  {invitesQuery.data.map((invite) => (
                    <div key={`${invite.email}-${invite.invitedAt}`} className="admin-list-row">
                      <div>
                        <strong>{invite.email}</strong>
                        <p className="form-note">Добавил: {invite.invitedBy}</p>
                      </div>
                      <div className="admin-list-meta">
                        <span className={`status-badge${invite.isActive ? "" : " is-muted"}`}>
                          {invite.isActive ? "Активен" : "Выключен"}
                        </span>
                        <span>{formatDate(invite.invitedAt)}</span>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </article>
          ) : null}
        </>
      ) : null}
    </PageSection>
  );
}
