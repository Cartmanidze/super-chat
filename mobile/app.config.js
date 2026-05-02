// Динамический конфиг Expo. Поверх статического app.json.
//
// Для чего нужен: запустить dev-сервер `npx expo start` без логина в Expo
// (без EXPO_TOKEN). По умолчанию app.json содержит `owner: "glebon84"` и
// `extra.eas.projectId` — эти поля заставляют Expo CLI запрашивать токен
// у проекта с таким владельцем, и без авторизации Expo Go показывает
// "Something went wrong".
//
// Решение: если задана переменная окружения EXPO_LOCAL_DEV=1, отдаём
// конфиг без owner/updates/eas — проект превращается в анонимный, всё
// загружается без логина. Для production-сборок (EAS Build, eas submit)
// переменную не задаём — owner и projectId возвращаются на место.
module.exports = ({ config }) => {
  if (process.env.EXPO_LOCAL_DEV !== "1") {
    return config;
  }

  const cleaned = { ...config };
  delete cleaned.owner;
  delete cleaned.updates;
  if (cleaned.extra && cleaned.extra.eas) {
    cleaned.extra = { ...cleaned.extra };
    delete cleaned.extra.eas;
  }
  return cleaned;
};
