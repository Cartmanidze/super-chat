const telegramStateLabels: Record<string, string> = {
  NotStarted: "не подключён",
  AwaitingLogin: "нужен вход",
  AwaitingCode: "нужен код",
  AwaitingPassword: "нужен пароль",
  Connected: "подключён",
  Failed: "нужна проверка",
};

export function formatTelegramState(state: string) {
  return telegramStateLabels[state] ?? state;
}
