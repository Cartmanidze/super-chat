export type TelegramLoginStep = "phone" | "code" | "password";

type TelegramLoginStepMeta = {
  title: string;
  description: string;
  hint: string;
  submitLabel: string;
  fieldLabel: string;
  placeholder: string;
  inputType: "tel" | "text" | "password";
  index: number;
  accent: string;
};

type TelegramLoginFormStateParams = {
  serverStep: TelegramLoginStep | null;
  submittedStep: TelegramLoginStep | null;
  isSubmitting: boolean;
};

type TelegramLoginFormState = {
  isLocked: boolean;
  showSubmitButton: boolean;
  showWaitingNote: boolean;
};

const telegramLoginStepMeta: Record<TelegramLoginStep, TelegramLoginStepMeta> = {
  phone: {
    title: "Номер телефона",
    description: "Шаг 1 из 3",
    hint: "Укажите номер в международном формате, например +79991234567.",
    submitLabel: "Продолжить",
    fieldLabel: "Телефон",
    placeholder: "+79991234567",
    inputType: "tel",
    index: 0,
    accent: "Отправим номер в Telegram bridge и перейдем к следующему шагу.",
  },
  code: {
    title: "SMS-код",
    description: "Шаг 2 из 3",
    hint: "Введите код без пробелов. Если код не пришел, подождите пару секунд и проверьте Telegram.",
    submitLabel: "Подтвердить код",
    fieldLabel: "Код из Telegram",
    placeholder: "12345",
    inputType: "text",
    index: 1,
    accent: "После проверки кода сервис либо подключится, либо попросит пароль 2FA.",
  },
  password: {
    title: "Пароль 2FA",
    description: "Шаг 3 из 3",
    hint: "Введите пароль двухэтапной проверки Telegram. Он не сохраняется в интерфейсе.",
    submitLabel: "Завершить вход",
    fieldLabel: "Пароль",
    placeholder: "Пароль двухэтапной проверки",
    inputType: "password",
    index: 2,
    accent: "После проверки пароля подключение завершится и начнется синхронизация.",
  },
};

export const telegramLoginSteps = Object.keys(telegramLoginStepMeta) as TelegramLoginStep[];

export function isTelegramLoginStep(step: string | null): step is TelegramLoginStep {
  return step === "phone" || step === "code" || step === "password";
}

export function getTelegramLoginStepMeta(step: TelegramLoginStep) {
  return telegramLoginStepMeta[step];
}

export function getTelegramLoginFormState({
  serverStep,
  submittedStep,
  isSubmitting,
}: TelegramLoginFormStateParams): TelegramLoginFormState {
  if (isSubmitting) {
    return {
      isLocked: true,
      showSubmitButton: true,
      showWaitingNote: false,
    };
  }

  if (serverStep !== null && submittedStep === serverStep) {
    return {
      isLocked: true,
      showSubmitButton: false,
      showWaitingNote: true,
    };
  }

  return {
    isLocked: false,
    showSubmitButton: true,
    showWaitingNote: false,
  };
}
