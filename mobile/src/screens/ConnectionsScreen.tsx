import { useQuery } from "@tanstack/react-query";
import { Pressable, Text, View } from "react-native";
import { useNavigation } from "@react-navigation/native";
import type { NativeStackNavigationProp } from "@react-navigation/native-stack";
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

type Source = {
  key: string;
  brand: BrandKind;
  title: string;
  subtitle: string;
  state: "live" | "pending" | "soon";
  note?: string;
};

const SOURCES: Source[] = [
  { key: "tg", brand: "tg", title: "Telegram", subtitle: "Чаты и группы", state: "live", note: "Активная сессия. Обновляется в реальном времени." },
  { key: "gm", brand: "gm", title: "Gmail", subtitle: "Приглашения и .ics", state: "soon", note: "Запустим, как только будет готов OAuth-поток." },
  { key: "sl", brand: "sl", title: "Slack", subtitle: "DM и каналы", state: "soon", note: "В приватной бете." },
  { key: "ol", brand: "ol", title: "Outlook", subtitle: "Microsoft 365", state: "soon" },
  { key: "wa", brand: "wa", title: "WhatsApp", subtitle: "Личные и рабочие", state: "soon" },
];

export function ConnectionsScreen() {
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
        subtitle="Источники"
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
            Подключения
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
                      {src.title}
                    </Text>
                    <StatePill state={src.state} />
                  </View>
                  <Text style={{ ...typography.body, fontSize: 12, color: colors.ash400, marginTop: 2 }}>
                    {src.subtitle}
                  </Text>
                </View>
                {isTelegram ? (
                  <Button variant="ghost" style={{ minHeight: 36, paddingHorizontal: 14 }} onPress={openTelegram}>
                    {src.state === "live" ? "Открыть" : "Подключить"}
                  </Button>
                ) : (
                  <Button variant="ghost" style={{ minHeight: 36, paddingHorizontal: 14 }}>
                    В очередь
                  </Button>
                )}
              </View>
              {src.note ? (
                <Text style={{ ...typography.body, fontSize: 12, color: colors.ash400, marginTop: 12 }}>
                  {src.note}
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
            <Eyebrow color={colors.ash500}>Предложить источник</Eyebrow>
            <Text style={{ ...typography.body, fontSize: 12, color: colors.ash400, textAlign: "center", marginTop: 8 }}>
              Discord? VK? Расскажите, что подключить следующим.
            </Text>
          </View>
        </Card>
      </View>
    </Screen>
  );
}

function StatePill({ state }: { state: "live" | "pending" | "soon" }) {
  if (state === "live") return <Pill kind="confirmed">Активно</Pill>;
  if (state === "pending") return <Pill kind="pending">Нужен вход</Pill>;
  return <Pill kind="past">Скоро</Pill>;
}
