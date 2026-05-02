// Инициализация i18next для мобильного приложения.
//
// Стратегия выбора языка:
//   1. Сначала пробуем прочитать сохранённый выбор пользователя из SecureStore
//      (ключ LANGUAGE_KEY). Этот выбор делается на экране профиля.
//   2. Если выбор не сохранён — спрашиваем системный язык через expo-localization.
//      Если первый системный язык начинается с "ru" — берём ru, иначе en.
//   3. Любой неизвестный код языка валится в fallback (ru).
//
// Сама смена языка идёт через `changeAppLanguage(...)` ниже: меняем язык в i18n
// и пишем выбор в SecureStore, чтобы при следующем запуске сразу применялось.
//
// Pluralization: i18next из коробки знает правила для ru/en. Ключи вида
// `*_one`, `*_few`, `*_many`, `*_other` подбираются автоматически по числу.
import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import * as Localization from "expo-localization";
import * as SecureStore from "expo-secure-store";

import en from "./locales/en.json";
import ru from "./locales/ru.json";

export const SUPPORTED_LANGUAGES = ["ru", "en"] as const;
export type SupportedLanguage = (typeof SUPPORTED_LANGUAGES)[number];

const LANGUAGE_KEY = "superchat-language";
const FALLBACK_LANGUAGE: SupportedLanguage = "ru";

function detectDeviceLanguage(): SupportedLanguage {
  try {
    const locales = Localization.getLocales();
    const first = locales[0]?.languageCode ?? "";
    if (first.toLowerCase().startsWith("ru")) return "ru";
    if (first.toLowerCase().startsWith("en")) return "en";
  } catch {
    // ignore — упадём в fallback
  }
  return FALLBACK_LANGUAGE;
}

function isSupported(value: string | null | undefined): value is SupportedLanguage {
  return value === "ru" || value === "en";
}

// Стартуем синхронно с детекцией системного языка, чтобы UI не моргал
// английским перед чтением сохранённого выбора.
const initialLanguage = detectDeviceLanguage();

void i18n
  .use(initReactI18next)
  .init({
    resources: {
      ru: { translation: ru },
      en: { translation: en },
    },
    lng: initialLanguage,
    fallbackLng: FALLBACK_LANGUAGE,
    supportedLngs: [...SUPPORTED_LANGUAGES],
    interpolation: { escapeValue: false },
    returnNull: false,
    compatibilityJSON: "v4",
  });

// Async-чтение сохранённого выбора — если есть, применим уже после инициализации.
// SecureStore не имеет sync-API, поэтому первоначальный язык — системный, а потом
// при необходимости меняем без перезапуска (i18n.changeLanguage триггерит ререндер).
void (async () => {
  try {
    const stored = await SecureStore.getItemAsync(LANGUAGE_KEY);
    if (isSupported(stored) && stored !== i18n.language) {
      await i18n.changeLanguage(stored);
    }
  } catch {
    // ignore
  }
})();

export async function changeAppLanguage(language: SupportedLanguage): Promise<void> {
  await i18n.changeLanguage(language);
  try {
    await SecureStore.setItemAsync(LANGUAGE_KEY, language);
  } catch {
    // если SecureStore недоступен (например, web) — выбор сохранится только в памяти
  }
}

export function currentLanguage(): SupportedLanguage {
  return isSupported(i18n.language) ? i18n.language : FALLBACK_LANGUAGE;
}

// Локаль для Intl.DateTimeFormat / toLocaleString. На ru-RU работает русская
// неделя ("пн", "вт"), на en-GB — 24-часовой формат без "am/pm" (нам так нужно).
export function intlLocale(): string {
  return currentLanguage() === "ru" ? "ru-RU" : "en-GB";
}

export default i18n;
