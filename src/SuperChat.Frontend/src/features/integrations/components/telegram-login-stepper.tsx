import {
  getTelegramLoginStepMeta,
  telegramLoginSteps,
  type TelegramLoginStep,
} from "../lib/telegram-login-form-state";

type TelegramLoginStepperProps = {
  currentStep: TelegramLoginStep;
};

export function TelegramLoginStepper({ currentStep }: TelegramLoginStepperProps) {
  const currentIndex = getTelegramLoginStepMeta(currentStep).index;

  return (
    <ol className="telegram-login-stepper" aria-label="Шаги входа в Telegram">
      {telegramLoginSteps.map((step) => {
        const meta = getTelegramLoginStepMeta(step);
        const state =
          meta.index < currentIndex ? "done" : meta.index === currentIndex ? "current" : "upcoming";

        return (
          <li className="telegram-login-step" data-state={state} key={step}>
            <span className="telegram-login-step-index">{meta.index + 1}</span>
            <div className="telegram-login-step-copy">
              <strong>{meta.title}</strong>
              <span>{meta.description}</span>
            </div>
          </li>
        );
      })}
    </ol>
  );
}
