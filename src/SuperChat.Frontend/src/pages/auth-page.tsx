import { useMutation } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import { useEffect, useMemo, useRef, useState } from "react";
import { authGateway } from "../features/auth/gateways/auth-gateway";
import { useSessionStore } from "../features/auth/stores/session-store";
import { ApiError } from "../shared/services/http-api";

const OTP_LENGTH = 6;
const RESEND_SECONDS = 42;

function getBrowserTimeZone(): string | undefined {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone;
  } catch {
    return undefined;
  }
}

function describeSendCodeError(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 403) return "Этот email не приглашён в пилот.";
    if (error.status === 429) return "Слишком много запросов кода. Подожди минуту.";
    if (error.detail) return error.detail;
  }
  return "Не удалось отправить код. Попробуй ещё раз.";
}

function describeVerifyCodeError(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 400) return "Код неверный или истёк. Запроси новый.";
    if (error.status === 429) return "Слишком много попыток. Подожди и попробуй снова.";
    if (error.detail) return error.detail;
  }
  return "Не удалось войти. Попробуй ещё раз.";
}

function padCells(code: string): string[] {
  const cells = code.split("").slice(0, OTP_LENGTH);
  while (cells.length < OTP_LENGTH) {
    cells.push("");
  }
  return cells;
}

function formatCountdown(seconds: number): string {
  const m = Math.floor(seconds / 60);
  const s = seconds % 60;
  return `${m}:${s.toString().padStart(2, "0")}`;
}

export function AuthPage() {
  const navigate = useNavigate();
  const token = useSessionStore((state) => state.accessToken);
  const setSession = useSessionStore((state) => state.setSession);
  const [email, setEmail] = useState("");
  const [code, setCode] = useState("");
  const [codeRequested, setCodeRequested] = useState(false);
  const [resendSeconds, setResendSeconds] = useState(RESEND_SECONDS);
  const otpInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (token) {
      void navigate({ to: "/" });
    }
  }, [navigate, token]);

  useEffect(() => {
    if (!codeRequested) return;
    if (resendSeconds <= 0) return;
    const timer = setInterval(() => {
      setResendSeconds((value) => Math.max(0, value - 1));
    }, 1000);
    return () => clearInterval(timer);
  }, [codeRequested, resendSeconds]);

  const sendCodeMutation = useMutation({
    mutationFn: () => authGateway.sendCode(email),
    onSuccess: () => {
      setCodeRequested(true);
      setResendSeconds(RESEND_SECONDS);
      setCode("");
      setTimeout(() => otpInputRef.current?.focus(), 0);
    },
  });

  const verifyCodeMutation = useMutation({
    mutationFn: () => authGateway.verifyCode(email, code, getBrowserTimeZone()),
    onSuccess: async (session) => {
      setSession(session.accessToken, session.user.email);
      await navigate({ to: "/" });
    },
  });

  const otpCells = useMemo(() => padCells(code), [code]);
  const activeIndex = Math.min(code.length, OTP_LENGTH - 1);
  // После успешной верификации блокируем повторное нажатие «Войти», иначе
  // пользователь, не дождавшись редиректа, ещё раз отправляет тот же код,
  // а он уже consumed → 400 «code invalid or expired». Видели такое в проде.
  const canSubmit =
    code.length === OTP_LENGTH &&
    !verifyCodeMutation.isPending &&
    !verifyCodeMutation.isSuccess;

  return (
    <section className="page-section">
      <div className="section-head">
        <h2>
          Вход в Super Chat <em>· один код из почты</em>
        </h2>
      </div>

      <section className="auth">
        <div className="auth-left">
          <div className="auth-bolt" aria-hidden="true">
            <svg width="28" height="28" viewBox="0 0 48 48" fill="none">
              <defs>
                <linearGradient id="auth-bolt-gradient" x1="0" y1="0" x2="1" y2="1">
                  <stop offset="0" stopColor="#ffe49a" />
                  <stop offset="0.5" stopColor="#f3c96b" />
                  <stop offset="1" stopColor="#a67c1e" />
                </linearGradient>
              </defs>
              <path fill="url(#auth-bolt-gradient)" d="M28 4 L14 26 L21 26 L19 44 L34 20 L27 20 L28 4 Z" />
            </svg>
          </div>

          <div className="eyebrow auth-step-eye">{codeRequested ? "Шаг 2 из 2" : "Шаг 1 из 2"}</div>
          {!codeRequested ? (
            <>
              <h3 className="auth-title">
                Введите почту<br />
                <span>для кода.</span>
              </h3>
              <p className="auth-sub">
                Отправим 6-значный код на указанный адрес. Никаких паролей — только доступ к почте.
              </p>

              <form
                onSubmit={(event) => {
                  event.preventDefault();
                  sendCodeMutation.mutate();
                }}
              >
                <label className="field">
                  <span>Email</span>
                  <input
                    type="email"
                    autoComplete="email"
                    value={email}
                    onChange={(event) => setEmail(event.target.value)}
                    placeholder="you@example.com"
                    required
                  />
                </label>

                <div className="auth-row">
                  <button
                    className="btn is-primary"
                    type="submit"
                    disabled={!email || sendCodeMutation.isPending}
                  >
                    {sendCodeMutation.isPending ? "Отправляем…" : "Получить код"}
                    <span className="kbd">↵</span>
                  </button>
                </div>

                {sendCodeMutation.isError ? (
                  <p className="form-error">{describeSendCodeError(sendCodeMutation.error)}</p>
                ) : null}
              </form>
            </>
          ) : (
            <>
              <h3 className="auth-title">
                Введите код<br />
                <span>из письма.</span>
              </h3>
              <p className="auth-sub">
                Мы отправили 6-значный код на <b>{email}</b>. Он действует 10 минут.
              </p>

              <form
                onSubmit={(event) => {
                  event.preventDefault();
                  if (canSubmit) {
                    verifyCodeMutation.mutate();
                  }
                }}
              >
                <div
                  className="otp"
                  onClick={() => otpInputRef.current?.focus()}
                  role="group"
                  aria-label="Код из письма"
                >
                  {otpCells.map((cell, index) => {
                    const isFilled = Boolean(cell);
                    const isActive = index === activeIndex && code.length < OTP_LENGTH;
                    const className = `otp-cell${isFilled ? " is-filled" : ""}${isActive ? " is-active" : ""}`;
                    return (
                      <div key={index} className={className}>
                        {cell || "·"}
                      </div>
                    );
                  })}
                  <input
                    ref={otpInputRef}
                    className="otp-input"
                    inputMode="numeric"
                    autoComplete="one-time-code"
                    maxLength={OTP_LENGTH}
                    value={code}
                    onChange={(event) => setCode(event.target.value.replace(/\D/g, "").slice(0, OTP_LENGTH))}
                  />
                </div>

                <div className="auth-row">
                  <button className="btn is-primary" type="submit" disabled={!canSubmit}>
                    {verifyCodeMutation.isSuccess
                      ? "Открываем…"
                      : verifyCodeMutation.isPending
                        ? "Проверяем…"
                        : "Войти"}
                    <span className="kbd">↵</span>
                  </button>
                  <span className="resend">
                    {resendSeconds > 0 ? (
                      <>
                        Отправить снова через <b>{formatCountdown(resendSeconds)}</b>
                      </>
                    ) : (
                      <button
                        type="button"
                        onClick={() => sendCodeMutation.mutate()}
                        disabled={sendCodeMutation.isPending}
                      >
                        Отправить снова
                      </button>
                    )}
                  </span>
                </div>

                {verifyCodeMutation.isError ? (
                  <p className="form-error">{describeVerifyCodeError(verifyCodeMutation.error)}</p>
                ) : null}
              </form>

              <div className="auth-foot">
                <span>Нет доступа к почте?</span>
                <button
                  type="button"
                  onClick={() => {
                    setCodeRequested(false);
                    setCode("");
                  }}
                >
                  Ввести другой адрес
                </button>
              </div>
            </>
          )}
        </div>

        <aside className="auth-right">
          <div className="auth-eye">Почему коды</div>
          <h4>
            Никаких паролей.<br />
            Только доступ к почте.
          </h4>
          <ul className="auth-list">
            <li>
              <span className="auth-num">01</span>
              <div>
                <b>Ничего не хранится</b>
                <em>Коды одноразовые, живут 10 минут.</em>
              </div>
            </li>
            <li>
              <span className="auth-num">02</span>
              <div>
                <b>Двухфакторка по умолчанию</b>
                <em>Доступ привязан к вашему ящику.</em>
              </div>
            </li>
            <li>
              <span className="auth-num">03</span>
              <div>
                <b>Без лишних сессий</b>
                <em>Войти нужно раз в 30 дней, потом — продление само.</em>
              </div>
            </li>
          </ul>
        </aside>
      </section>
    </section>
  );
}
