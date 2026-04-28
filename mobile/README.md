# Super Chat — Mobile (Expo + React Native)

Кросс-платформенный (iOS + Android) клиент к Super Chat API. Стек: Expo SDK 54, React Native 0.81, TypeScript, React Navigation, TanStack Query, Zustand, expo-secure-store, expo-linear-gradient, react-native-svg.

## Структура

```
src/
  theme/        — токены (цвета, типографика, тени, радиусы), шрифты Manrope/Inter/IBM Plex Mono
  ui/           — primitives: BoltIcon, BoltChip, Pill, Button, Avatar, Card, Eyebrow, Header, Screen, BrandIcon, Dots
  api/          — axios + те же контракты что у веба (auth, me, meetings, telegram)
  store/        — zustand session-store + expo-secure-store (Keychain/Keystore для токена)
  lib/          — хелперы (relative-time, форматы)
  screens/      — Onboarding, Auth, Today, Connections, TelegramLogin, Profile
  navigation/   — Root (Stack: Onboarding → Auth → Tabs), TabBar (BlurView), ConnectStack
```

## Что уже есть

- 4-шаговый онбординг (Hero / How / Value / CTA) + 6-cell OTP-вход.
- Главный экран «Сегодня» — hero next-meeting, ждут подтверждения, timeline дня.
- Источники + Telegram-login (3 шага: phone → code → password) с polling-ом статуса.
- Профиль + выход (с очисткой SecureStore + react-query кэша).
- Liquid-glass tab-bar (BlurView 50, dark, 1px bone-border) с активным state-gradient.
- Тёмная Bolt-палитра целиком из дизайн-системы веба.

## Запуск

```bash
cd mobile
npm install
npm run ios       # macOS + Xcode
npm run android   # Android Studio (любая ОС)
npm start         # Expo Go на физическом устройстве (отсканируй QR)
```

API endpoint по умолчанию — `https://api.super-chat.org/api/v1`. Переопределить (например, для локального API):

```bash
EXPO_PUBLIC_API_BASE_URL=http://192.168.0.10:5050/api/v1 npm start
```

## EAS (build & deploy)

Все три профиля в [eas.json](./eas.json) сейчас смотрят на прод (`api.super-chat.org`). Различие — в дистрибуции:
- `development` — internal dev-client (для отладки на dev-машине).
- `preview` — internal IPA/APK для теста на физических устройствах.
- `production` — store-ready, autoIncrement версий.

```bash
npx eas-cli login
npx eas-cli build --profile preview --platform android
npx eas-cli build --profile production --platform ios
```

`appleId`, `ascAppId`, `appleTeamId` в `submit.production` — заполнить после регистрации в Apple Dev Account.

## Брендинг (TODO)

В `assets/` сейчас лежат дефолтные иконки Expo. Для релиза заменить:
- `icon.png` 1024×1024 — брендовый красный bolt на `#060606`.
- `splash-icon.png` 1242×1242 — золотой bolt на `#060606`.
- `adaptive-icon.png` (Android) — bolt на круге, фон `#060606`.

Источник для иконки — [src/SuperChat.Frontend/public/favicon.svg](../src/SuperChat.Frontend/public/favicon.svg) (тот же красный градиент `#e5383b → #c1121f`).

## Что осталось

- Push-нотификации (`expo-notifications`) для напоминаний о встречах.
- Pull-to-refresh на Today.
- Ошибки и offline-state — пока минимально (Alert).
- Тесты — пока ноль.
- `meetings/{id}/confirm|complete|dismiss` actions из карточек (есть в gateway, не подключены к UI).
- Real-time обновление через `mtCallback` или WebSocket — пока polling react-query.
