import { useEffect, useMemo, useRef, useState } from "react";
import { Alert, Pressable, Text, TextInput, View } from "react-native";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { telegramGateway, type TelegramConnection } from "../api/telegram";
import { useSessionStore } from "../store/session";
import { Screen } from "../ui/Screen";
import { Header } from "../ui/Header";
import { Button } from "../ui/Button";
import { Card } from "../ui/Card";
import { Eyebrow } from "../ui/Eyebrow";
import { Pill } from "../ui/Pill";
import { colors, radii, typography } from "../theme/tokens";

type Step = "phone" | "code" | "password";

const META: Record<
  Step,
  { index: number; title: string; hint: string; placeholder: string; cta: string; keyboard: "phone-pad" | "number-pad" | "default" }
> = {
  phone: {
    index: 0,
    title: "Номер телефона",
    hint: "В формате +7 999 123-45-67. SMS придёт от Telegram.",
    placeholder: "+7 999 000-00-00",
    cta: "Получить код",
    keyboard: "phone-pad",
  },
  code: {
    index: 1,
    title: "Код из Telegram",
    hint: "Telegram пришлёт его на ваш аккаунт. Введите 5 цифр.",
    placeholder: "12345",
    cta: "Подтвердить",
    keyboard: "number-pad",
  },
  password: {
    index: 2,
    title: "Облачный пароль",
    hint: "Включена двухфакторка — введите облачный пароль.",
    placeholder: "Облачный пароль",
    cta: "Войти",
    keyboard: "default",
  },
};

type TelegramLoginScreenProps = {
  onClose: () => void;
};

export function TelegramLoginScreen({ onClose }: TelegramLoginScreenProps) {
  const token = useSessionStore((s) => s.accessToken);
  const queryClient = useQueryClient();
  const [value, setValue] = useState("");
  const inputRef = useRef<TextInput>(null);

  const status = useQuery({
    queryKey: ["telegram-connection"],
    queryFn: () => telegramGateway.get(token!),
    enabled: Boolean(token),
    refetchInterval: 4000,
  });

  const data = status.data;
  const step = isStep(data?.chatLoginStep) ? data!.chatLoginStep : null;
  const requiresAction = data?.requiresAction;

  const startConnect = useMutation({
    mutationFn: () => telegramGateway.connect(token!),
    onSuccess: (next) => queryClient.setQueryData(["telegram-connection"], next),
    onError: (e: Error) => Alert.alert("Не удалось начать вход", e.message),
  });

  const submitInput = useMutation({
    mutationFn: () => telegramGateway.submitLoginInput(token!, value.trim()),
    onSuccess: (next) => {
      queryClient.setQueryData(["telegram-connection"], next);
      setValue("");
    },
    onError: (e: Error) => Alert.alert("Ошибка", e.message),
  });

  const disconnect = useMutation({
    mutationFn: () => telegramGateway.disconnect(token!),
    onSuccess: (next) => queryClient.setQueryData(["telegram-connection"], next),
    onError: (e: Error) => Alert.alert("Не удалось отключить", e.message),
  });

  useEffect(() => {
    if (step) setTimeout(() => inputRef.current?.focus(), 100);
  }, [step]);

  const meta = step ? META[step] : null;
  const progress = useMemo(() => (meta ? `${meta.index + 1}/3` : null), [meta]);

  return (
    <Screen>
      <Header
        subtitle="Telegram"
        onBack={onClose}
        title="Подключение"
        right={data ? <ConnectionPill connection={data} /> : undefined}
      />
      <View style={{ paddingHorizontal: 16, gap: 14 }}>
        {!step ? (
          <Card>
            <Eyebrow color={colors.ash500}>Состояние</Eyebrow>
            <Text
              style={{
                ...typography.heading,
                fontFamily: "Manrope_700Bold",
                fontSize: 18,
                color: colors.bone,
                marginTop: 8,
                letterSpacing: -0.4,
              }}
            >
              {data?.state === "Connected"
                ? "Telegram подключён"
                : requiresAction
                  ? "Нужно действие"
                  : "Не подключено"}
            </Text>
            <Text style={{ ...typography.body, fontSize: 13, color: colors.ash400, marginTop: 6 }}>
              {data?.state === "Connected"
                ? "Super Chat читает чаты через защищённую сессию. Можно отключить ниже."
                : "Super Chat читает чаты через защищённую сессию. Вход — по номеру телефона и одноразовому коду из Telegram."}
            </Text>
            {data?.state !== "Connected" ? (
              <View style={{ flexDirection: "row", gap: 10, marginTop: 14 }}>
                <Button variant="primary" full onPress={() => startConnect.mutate()}>
                  {startConnect.isPending ? "Открываем…" : "Подключить"}
                </Button>
              </View>
            ) : null}
          </Card>
        ) : (
          <Card accent>
            <View style={{ flexDirection: "row", justifyContent: "space-between", marginBottom: 8 }}>
              <Pill kind="kind">Шаг {progress}</Pill>
            </View>
            <Text
              style={{
                ...typography.heading,
                fontFamily: "Manrope_700Bold",
                fontSize: 18,
                color: colors.bone,
                letterSpacing: -0.4,
              }}
            >
              {meta!.title}
            </Text>
            <Text style={{ ...typography.body, fontSize: 13, color: colors.ash400, marginTop: 6 }}>
              {meta!.hint}
            </Text>

            <View
              style={{
                marginTop: 14,
                height: 56,
                borderRadius: radii.lg,
                borderWidth: 1,
                borderColor: "rgba(229,56,59,0.35)",
                backgroundColor: colors.ink900,
                paddingHorizontal: 14,
                justifyContent: "center",
              }}
            >
              <TextInput
                ref={inputRef}
                value={value}
                onChangeText={setValue}
                placeholder={meta!.placeholder}
                placeholderTextColor={colors.ash500}
                keyboardType={meta!.keyboard}
                secureTextEntry={step === "password"}
                autoCapitalize="none"
                autoCorrect={false}
                editable={!submitInput.isPending}
                style={{ ...typography.bodySemi, fontSize: 16, color: colors.bone }}
              />
            </View>

            <Button
              variant="primary"
              full
              disabled={value.trim().length === 0 || submitInput.isPending}
              onPress={() => submitInput.mutate()}
              style={{ marginTop: 14 }}
            >
              {submitInput.isPending ? "Отправляем…" : meta!.cta}
            </Button>
          </Card>
        )}

        {data?.state === "Connected" ? (
          <Pressable onPress={() => disconnect.mutate()} style={{ alignSelf: "center", paddingVertical: 12 }}>
            <Text style={{ ...typography.bodyMd, fontSize: 13, color: colors.bolt400 }}>Отключить Telegram</Text>
          </Pressable>
        ) : null}
      </View>
    </Screen>
  );
}

function ConnectionPill({ connection }: { connection: TelegramConnection }) {
  if (connection.state === "Connected") return <Pill kind="confirmed">● ON</Pill>;
  if (connection.requiresAction) return <Pill kind="pending">Действие</Pill>;
  return <Pill kind="past">OFF</Pill>;
}

function isStep(value: string | null | undefined): value is Step {
  return value === "phone" || value === "code" || value === "password";
}
