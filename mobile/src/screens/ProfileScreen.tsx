import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Text, View } from "react-native";
import { useSessionStore } from "../store/session";
import { meGateway } from "../api/me";
import { authGateway } from "../api/auth";
import { Screen } from "../ui/Screen";
import { Header } from "../ui/Header";
import { Card } from "../ui/Card";
import { Avatar } from "../ui/Avatar";
import { Button } from "../ui/Button";
import { Eyebrow } from "../ui/Eyebrow";
import { Pill } from "../ui/Pill";
import { colors, typography } from "../theme/tokens";

function initials(email: string | null): string {
  if (!email) return "Я";
  return email.slice(0, 2).toUpperCase();
}

export function ProfileScreen() {
  const queryClient = useQueryClient();
  const token = useSessionStore((s) => s.accessToken);
  const email = useSessionStore((s) => s.email);
  const clearSession = useSessionStore((s) => s.clearSession);
  const me = useQuery({
    queryKey: ["me"],
    queryFn: () => meGateway.get(token!),
    enabled: Boolean(token),
  });

  const logout = useMutation({
    mutationFn: async () => {
      if (token) await authGateway.logout(token);
    },
    // Optimistic: сразу чистим локальную сессию и кэш, чтобы пользователь
    // мгновенно ушёл на onboarding. Сетевой запрос на /auth/logout
    // (revoke токена) уезжает в фоне; ошибка игнорируется — токен в любом
    // случае больше не используется на этом устройстве.
    onMutate: async () => {
      await clearSession();
      queryClient.clear();
    },
  });

  return (
    <Screen>
      <Header
        subtitle="Аккаунт"
        title={
          <Text
            style={{
              ...typography.display,
              fontFamily: "Manrope_800ExtraBold",
              fontSize: 24,
              color: colors.bone,
              letterSpacing: -0.6,
            }}
          >
            Профиль
          </Text>
        }
      />
      <View style={{ paddingHorizontal: 16, gap: 14 }}>
        <Card>
          <View style={{ flexDirection: "row", alignItems: "center", gap: 14 }}>
            <Avatar who={initials(email)} size={56} variant="g2" />
            <View style={{ flex: 1, minWidth: 0, gap: 8 }}>
              <Text
                numberOfLines={1}
                ellipsizeMode="tail"
                style={{
                  ...typography.heading,
                  fontFamily: "Manrope_700Bold",
                  fontSize: 15,
                  color: colors.bone,
                  letterSpacing: -0.3,
                }}
              >
                {email ?? "Гость"}
              </Text>
              <View style={{ flexDirection: "row", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
                <Text style={{ ...typography.body, fontSize: 12, color: colors.ash400 }}>Пилот</Text>
                {me.data ? (
                  <Pill kind={me.data.requiresTelegramAction ? "pending" : "confirmed"}>
                    {me.data.requiresTelegramAction ? "Нужен вход" : "Готово"}
                  </Pill>
                ) : null}
              </View>
            </View>
          </View>
        </Card>

        <Card>
          <Eyebrow color={colors.ash500}>Подключение</Eyebrow>
          <Text
            style={{
              ...typography.heading,
              fontFamily: "Manrope_700Bold",
              fontSize: 16,
              color: colors.bone,
              marginTop: 8,
              letterSpacing: -0.3,
            }}
          >
            Telegram
          </Text>
          <Text style={{ ...typography.body, fontSize: 12, color: colors.ash400, marginTop: 4 }}>
            {me.data?.telegramState ?? "Загружаем…"}
          </Text>
        </Card>

        <Button variant="danger" full onPress={() => logout.mutate()}>
          {logout.isPending ? "Выходим…" : "Выйти"}
        </Button>
      </View>
    </Screen>
  );
}
