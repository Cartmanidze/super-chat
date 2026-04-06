import {
  getTelegramLoginFormState,
  getTelegramLoginStepMeta,
  type TelegramLoginStep,
} from "../lib/telegram-login-form-state";
import { TelegramLoginStepper } from "./telegram-login-stepper";

type TelegramLoginCardProps = {
  step: TelegramLoginStep;
  value: string;
  submittedStep: TelegramLoginStep | null;
  isSubmitting: boolean;
  errorMessage: string | null;
  onValueChange: (value: string) => void;
  onSubmit: () => void;
};

function getAutoComplete(step: TelegramLoginStep) {
  switch (step) {
    case "phone":
      return "tel";
    case "code":
      return "one-time-code";
    case "password":
      return "current-password";
  }
}

function getInputMode(step: TelegramLoginStep) {
  switch (step) {
    case "phone":
      return "tel";
    case "code":
      return "numeric";
    case "password":
      return "text";
  }
}

export function TelegramLoginCard({
  step,
  value,
  submittedStep,
  isSubmitting,
  errorMessage,
  onValueChange,
  onSubmit,
}: TelegramLoginCardProps) {
  const meta = getTelegramLoginStepMeta(step);
  const formState = getTelegramLoginFormState({
    serverStep: step,
    submittedStep,
    isSubmitting,
  });
  const progress = `${((meta.index + 1) / 3) * 100}%`;
  const canSubmit = value.trim().length > 0 && formState.showSubmitButton && !formState.isLocked;

  return (
    <form
      className="panel-card login-step-card"
      aria-busy={formState.isLocked}
      onSubmit={(event) => {
        event.preventDefault();
        if (canSubmit) {
          onSubmit();
        }
      }}
    >
      <div className="telegram-login-stage" key={step}>
        <div className="telegram-login-head">
          <div>
            <h3>Вход в Telegram</h3>
            <p>{meta.accent}</p>
          </div>
          <span className="status-badge">{meta.description}</span>
        </div>

        <div className="telegram-login-progress" aria-hidden="true">
          <span style={{ width: progress }} />
        </div>

        <TelegramLoginStepper currentStep={step} />

        <label className="field telegram-login-field">
          <span>{meta.fieldLabel}</span>
          <input
            className="search-input telegram-login-input"
            type={meta.inputType}
            value={value}
            onChange={(event) => onValueChange(event.target.value)}
            placeholder={meta.placeholder}
            autoComplete={getAutoComplete(step)}
            inputMode={getInputMode(step)}
            disabled={formState.isLocked}
          />
        </label>

        <p className="telegram-login-hint">{meta.hint}</p>

        <div className="telegram-login-actions">
          {formState.showSubmitButton ? (
            <button className="primary-button telegram-login-submit" type="submit" disabled={!canSubmit}>
              {isSubmitting ? (
                <>
                  <span className="button-spinner" aria-hidden="true" />
                  Отправляем...
                </>
              ) : (
                meta.submitLabel
              )}
            </button>
          ) : null}

          {formState.showWaitingNote ? (
            <p className="telegram-login-waiting" role="status" aria-live="polite">
              Данные отправлены. Ждём следующий шаг авторизации.
            </p>
          ) : null}
        </div>

        {errorMessage ? <p className="form-error">{errorMessage}</p> : null}
      </div>
    </form>
  );
}
