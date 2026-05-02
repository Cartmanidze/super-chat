import { useQuery } from "@tanstack/react-query";
import { Pressable, Text, View } from "react-native";
import { useNavigation } from "@react-navigation/native";
import type { NativeStackNavigationProp } from "@react-navigation/native-stack";
import { useTranslation } from "react-i18next";
import { useSessionStore } from "../store/session";
import { meGateway } from "../api/me";
import { Screen } from "../ui/Screen";
import { Header } from "../ui/Header";
import { Card } from "../ui/Card";
import { BrandIcon, type BrandKind } from "../ui/BrandIcon";
import { Pill } from "../ui/Pill";
import { Button } from "../ui/Button";
import { Eyebrow } from "../ui/Eyebrow";
import type { ConnectStackParamList } from "../navigation/ConnectStack";
import { colors, typography } from "../theme/tokens";

// Базовый список источников. Все названия и подписи берём через i18n,
// здесь храним только идентификатор бренда + ключи переводов.
type Source = {
  key: string;
  brand: BrandKind;
  titleKey: string;
  subtitleKey: string;
  state: "live" | "pending" | "soon";
  noteKey?: string;
};

const SOURCES: Source[] = [
  {
    key: "tg",
    brand: "tg",
    titleKey: "connections.telegramTitle",
    subtitleKey: "connections.telegramSubtitle",
    state: "live",
    noteKey: "connections.telegramNote",
  },
  {
    key: "gm",
    brand: "gm",
    titleKey: "connections.gmailTitle",
    subtitleKey: "connections.gmailSubtitle",
    state: "soon",
    noteKey: "connections.gmailNote",
  },
  {
    key: "sl",
    brand: "sl",
    titleKey: "connections.slackTitle",
    subtitleKey: "connections.slackSubtitle",
    state: "soon",
    noteKey: "connections.slackNote",
  },
  {
    key: "ol",
    brand: "ol",
    titleKey: "connections.outlookTitle",
    subtitleKey: "connections.outlookSubtitle",
    state: "soon",
  },
  {
    key: "wa",
    brand: "wa",
    titleKey: "connections.whatsappTitle",
    subtitleKey: "connections.whatsappSubtitle",
    state: "soon",
  },
];

export function ConnectionsScreen() {
  const { t } = useTranslation();
  const token = useSessionStore((s) => s.accessToken);
  const navigation = useNavigation<NativeStackNavigationProp<ConnectStackParamList>>();
  const me = useQuery({
    queryKey: ["me"],
    queryFn: () => meGateway.get(token!),
    enabled: Boolean(token),
  });

  const sources = SOURCES.map((s) => {
    if (s.key === "tg") {
      const live = me.data && !me.data.requiresTelegramAction;
      return { ...s, state: (live ? "live" : "pending") as "live" | "pending" };
    }
    return s;
  });

  const openTelegram = () => navigation.navigate("TelegramLogin");

  return (
    <Screen>
      <Header
        subtitle={t("connections.subtitle")}
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
            {t("connections.title")}
          </Text>
        }
      />
      <View style={{ paddingHorizontal: 16, gap: 12 }}>
        {sources.map((src) => {
          const isTelegram = src.key === "tg";
          const body = (
            <Card>
              <View style={{ flexDirection: "row", alignItems: "center", gap: 12 }}>
                <BrandIcon kind={src.brand} size={44} />
                <View style={{ flex: 1 }}>
                  <View style={{ flexDirection: "row", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
                    <Text
                      style={{
                        ...typography.heading,
                        fontFamily: "Manrope_700Bold",
                        fontSize: 16,
                        color: colors.bone,
                        letterSpacing: -0.3,
                      }}
                    >
                      {t(src.titleKey)}
                    </Text>
                    <StatePill state={src.state} />
                  </View>
                  <Text style={{ ...typography.body, fontSize: 12, color: colors.ash400, marginTop: 2 }}>
                    {t(src.subtitleKey)}
                  </Text>
                </View>
                {isTelegram ? (
                  <Button variant="ghost" style={{ minHeight: 36, paddingHorizontal: 14 }} onPress={openTelegram}>
                    {src.state === "live" ? t("connections.openCta") : t("connections.connectCta")}
                  </Button>
                ) : (
                  <Button variant="ghost" style={{ minHeight: 36, paddingHorizontal: 14 }}>
                    {t("connections.queueCta")}
                  </Button>
                )}
              </View>
              {src.noteKey ? (
                <Text style={{ ...typography.body, fontSize: 12, color: colors.ash400, marginTop: 12 }}>
                  {t(src.noteKey)}
                </Text>
              ) : null}
            </Card>
          );
          return isTelegram ? (
            <Pressable key={src.key} onPress={openTelegram}>
              {body}
            </Pressable>
          ) : (
            <View key={src.key}>{body}</View>
          );
        })}

        <Card>
          <View style={{ alignItems: "center", paddingVertical: 8 }}>
            <Eyebrow color={colors.ash500}>{t("connections.suggestEyebrow")}</Eyebrow>
            <Text style={{ ...typography.body, fontSize: 12, color: colors.ash400, textAlign: "center", marginTop: 8 }}>
              {t("connections.suggestText")}
            </Text>
          </View>
        </Card>
      </View>
    </Screen>
  );
}

function StatePill({ state }: { state: "live" | "pending" | "soon" }) {
  const { t } = useTranslation();
  if (state === "live") return <Pill kind="confirmed">{t("connections.pillActive")}</Pill>;
  if (state === "pending") return <Pill kind="pending">{t("connections.pillNeedsLogin")}</Pill>;
  return <Pill kind="past">{t("connections.pillSoon")}</Pill>;
}
