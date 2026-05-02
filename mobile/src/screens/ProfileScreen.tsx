import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Pressable, Text, View } from "react-native";
import { useTranslation } from "react-i18next";
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
import { colors, radii, typography } from "../theme/tokens";
import { changeAppLanguage, currentLanguage, type SupportedLanguage } from "../i18n";

function initials(email: string | null, fallback: string): string {
  if (!email) return fallback;
  return email.slice(0, 2).toUpperCase();
}

export function ProfileScreen() {
  const { t, i18n } = useTranslation();
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

  // Подписываемся на смену языка через хук useTranslation: он вернёт новый
  // i18n.language после `changeAppLanguage(...)` и компонент перерисуется.
  const active: SupportedLanguage = currentLanguage();
  const handleSwitch = (next: SupportedLanguage) => {
    if (next === active) return;
    void changeAppLanguage(next);
  };

  return (
    <Screen>
      <Header
        subtitle={t("profile.subtitle")}
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
            {t("profile.title")}
          </Text>
        }
      />
      <View style={{ paddingHorizontal: 16, gap: 14 }}>
        <Card>
          <View style={{ flexDirection: "row", alignItems: "center", gap: 14 }}>
            <Avatar
              who={initials(email, t("profile.guest").slice(0, 1).toUpperCase())}
              size={56}
              variant="g2"
            />
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
                {email ?? t("profile.guest")}
              </Text>
              <View style={{ flexDirection: "row", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
                <Text style={{ ...typography.body, fontSize: 12, color: colors.ash400 }}>
                  {t("profile.pilot")}
                </Text>
                {me.data ? (
                  <Pill kind={me.data.requiresTelegramAction ? "pending" : "confirmed"}>
                    {me.data.requiresTelegramAction
                      ? t("profile.pillNeedsLogin")
                      : t("profile.pillReady")}
                  </Pill>
                ) : null}
              </View>
            </View>
          </View>
        </Card>

        <Card>
          <Eyebrow color={colors.ash500}>{t("profile.connectionEyebrow")}</Eyebrow>
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
            {t("profile.telegramTitle")}
          </Text>
          <Text style={{ ...typography.body, fontSize: 12, color: colors.ash400, marginTop: 4 }}>
            {me.data?.telegramState ?? t("profile.loading")}
          </Text>
        </Card>

        <Card>
          <Eyebrow color={colors.ash500}>{t("profile.languageEyebrow")}</Eyebrow>
          {/*
            Сегментированный переключатель языка. Активный сегмент подсвечивается
            красной заливкой; зависимость от i18n.language нужна, чтобы перекраска
            произошла после changeAppLanguage(...).
          */}
          <View
            key={i18n.language}
            style={{
              flexDirection: "row",
              marginTop: 10,
              padding: 4,
              borderRadius: radii.md,
              borderWidth: 1,
              borderColor: colors.borderSoft,
              backgroundColor: colors.surfaceLow,
              gap: 4,
            }}
          >
            <LanguageOption
              active={active === "ru"}
              label={t("profile.languageRussian")}
              onPress={() => handleSwitch("ru")}
            />
            <LanguageOption
              active={active === "en"}
              label={t("profile.languageEnglish")}
              onPress={() => handleSwitch("en")}
            />
          </View>
        </Card>

        <Button variant="danger" full onPress={() => logout.mutate()}>
          {logout.isPending ? t("profile.loggingOut") : t("profile.logoutButton")}
        </Button>
      </View>
    </Screen>
  );
}

function LanguageOption({
  active,
  label,
  onPress,
}: {
  active: boolean;
  label: string;
  onPress: () => void;
}) {
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityState={{ selected: active }}
      style={{
        flex: 1,
        paddingVertical: 10,
        borderRadius: radii.sm,
        alignItems: "center",
        justifyContent: "center",
        backgroundColor: active ? "rgba(229,56,59,0.18)" : "transparent",
        borderWidth: 1,
        borderColor: active ? "rgba(229,56,59,0.45)" : "transparent",
      }}
    >
      <Text
        style={{
          ...typography.bodySemi,
          fontSize: 13,
          color: active ? colors.bone : colors.ash400,
        }}
      >
        {label}
      </Text>
    </Pressable>
  );
}
