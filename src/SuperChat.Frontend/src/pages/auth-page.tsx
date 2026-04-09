import { useMutation } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { authGateway } from "../features/auth/gateways/auth-gateway";
import { useSessionStore } from "../features/auth/stores/session-store";
import { PageSection } from "../shared/ui/page-section";

export function AuthPage() {
  const navigate = useNavigate();
  const token = useSessionStore((state) => state.accessToken);
  const setSession = useSessionStore((state) => state.setSession);
  const [email, setEmail] = useState("");
  const [code, setCode] = useState("");
  const [codeRequested, setCodeRequested] = useState(false);

  useEffect(() => {
    if (!token) {
      return;
    }

    void navigate({ to: "/" });
  }, [navigate, token]);

  const sendCodeMutation = useMutation({
    mutationFn: () => authGateway.sendCode(email),
    onSuccess: () => setCodeRequested(true),
  });

  const verifyCodeMutation = useMutation({
    mutationFn: () => authGateway.verifyCode(email, code, getBrowserTimeZone()),
    onSuccess: async (session) => {
      setSession(session.accessToken, session.user.email);
      await navigate({ to: "/" });
    },
  });

  return (
    <PageSection
      eyebrow="Вход"
      title="Вход по коду"
      description="Укажите почту, получите код и подтвердите вход."
    >
      <div className="auth-grid">
        <form
          className="panel-card auth-card"
          onSubmit={(event) => {
            event.preventDefault();
            sendCodeMutation.mutate();
          }}
        >
          <h3>Шаг 1. Получить код</h3>
          <label className="field">
            <span>Email</span>
            <input
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              placeholder="you@example.com"
              required
            />
          </label>
          <button className="primary-button" type="submit" disabled={sendCodeMutation.isPending}>
            {sendCodeMutation.isPending ? "Отправляем..." : "Получить код"}
          </button>
          {sendCodeMutation.isSuccess ? <p className="form-note">Код отправлен. Теперь введите его справа.</p> : null}
          {sendCodeMutation.isError ? <p className="form-error">{String(sendCodeMutation.error.message)}</p> : null}
        </form>

        <form
          className="panel-card auth-card"
          onSubmit={(event) => {
            event.preventDefault();
            verifyCodeMutation.mutate();
          }}
        >
          <h3>Шаг 2. Ввести код</h3>
          <label className="field">
            <span>Код</span>
            <input
              type="text"
              value={code}
              onChange={(event) => setCode(event.target.value)}
              placeholder="123456"
              required
            />
          </label>
          <button
            className="primary-button"
            type="submit"
            disabled={!codeRequested || verifyCodeMutation.isPending}
          >
            {verifyCodeMutation.isPending ? "Проверяем..." : "Войти"}
          </button>
          {!codeRequested ? <p className="form-note">Сначала запросите код слева.</p> : null}
          {verifyCodeMutation.isError ? <p className="form-error">{String(verifyCodeMutation.error.message)}</p> : null}
        </form>
      </div>

      <article className="panel-card helper-card">
        <h3>Как это работает</h3>
        <ul>
          <li>Код приходит на указанную почту.</li>
          <li>После входа откроются встречи, поиск и подключения.</li>
          <li>Если письмо не пришло, проверьте папку со спамом.</li>
        </ul>
      </article>
    </PageSection>
  );
}

function getBrowserTimeZone() {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone;
  } catch {
    return undefined;
  }
}
