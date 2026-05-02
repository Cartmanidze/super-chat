import { useEffect, useMemo, useRef, useState } from "react";
import { Alert, Pressable, Text, TextInput, View } from "react-native";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
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

type StepMeta = {
  index: number;
  titleKey: string;
  hintKey: string;
  placeholderKey: string;
  ctaKey: string;
  keyboard: "phone-pad" | "number-pad" | "default";
};

const STEPS: Record<Step, StepMeta> = {
  phone: {
    index: 0,
    titleKey: "telegramLogin.phoneTitle",
    hintKey: "telegramLogin.phoneHint",
    placeholderKey: "telegramLogin.phonePlaceholder",
    ctaKey: "telegramLogin.phoneCta",
    keyboard: "phone-pad",
  },
  code: {
    index: 1,
    titleKey: "telegramLogin.codeTitle",
    hintKey: "telegramLogin.codeHint",
    placeholderKey: "telegramLogin.codePlaceholder",
    ctaKey: "telegramLogin.codeCta",
    keyboard: "number-pad",
  },
  password: {
    index: 2,
    titleKey: "telegramLogin.passwordTitle",
    hintKey: "telegramLogin.passwordHint",
    placeholderKey: "telegramLogin.passwordPlaceholder",
    ctaKey: "telegramLogin.passwordCta",
    keyboard: "default",
  },
};

type TelegramLoginScreenProps = {
  onClose: () => void;
};

export function TelegramLoginScreen({ onClose }: TelegramLoginScreenProps) {
  const { t } = useTranslation();
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
    onError: (e: Error) => Alert.alert(t("telegramLogin.errorStartTitle"), e.message),
  });

  const submitInput = useMutation({
    mutationFn: () => telegramGateway.submitLoginInput(token!, value.trim()),
    onSuccess: (next) => {
      queryClient.setQueryData(["telegram-connection"], next);
      setValue("");
    },
    onError: (e: Error) => Alert.alert(t("telegramLogin.errorGenericTitle"), e.message),
  });

  const disconnect = useMutation({
    mutationFn: () => telegramGateway.disconnect(token!),
    // Optimistic: сразу переводим UI в Disconnected, не ждём round-trip.
    // На ошибке откатываемся к снимку состояния и показываем Alert.
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: ["telegram-connection"] });
      const previous = queryClient.getQueryData<TelegramConnection>([
        "telegram-connection",
      ]);
      queryClient.setQueryData<TelegramConnection>(
        ["telegram-connection"],
        (old) =>
          old
            ? { ...old, state: "Disconnected", chatLoginStep: null, requiresAction: false }
            : { state: "Disconnected", matrixUserId: null, chatLoginStep: null, lastSyncedAt: null, requiresAction: false },
      );
      return { previous };
    },
    onSuccess: (next) => queryClient.setQueryData(["telegram-connection"], next),
    onError: (e: Error, _vars, ctx) => {
      if (ctx?.previous !== undefined) {
        queryClient.setQueryData(["telegram-connection"], ctx.previous);
      }
      Alert.alert(t("telegramLogin.errorDisconnectTitle"), e.message);
    },
  });

  useEffect(() => {
    if (step) setTimeout(() => inputRef.current?.focus(), 100);
  }, [step]);

  const meta = step ? STEPS[step] : null;
  const progress = useMemo(() => (meta ? `${meta.index + 1}/3` : null), [meta]);

  return (
    <Screen>
      <Header
        subtitle={t("telegramLogin.subtitle")}
        onBack={onClose}
        title={t("telegramLogin.title")}
        right={data ? <ConnectionPill connection={data} /> : undefined}
      />
      <View style={{ paddingHorizontal: 16, gap: 14 }}>
        {!step ? (
          <Card>
            <Eyebrow color={colors.ash500}>{t("telegramLogin.stateLabel")}</Eyebrow>
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
                ? t("telegramLogin.stateConnected")
                : requiresAction
                  ? t("telegramLogin.stateNeedsAction")
                  : t("telegramLogin.stateNotConnected")}
            </Text>
            <Text style={{ ...typography.body, fontSize: 13, color: colors.ash400, marginTop: 6 }}>
              {data?.state === "Connected"
                ? t("telegramLogin.descriptionConnected")
                : t("telegramLogin.descriptionNotConnected")}
            </Text>
            {data?.state !== "Connected" ? (
              <View style={{ flexDirection: "row", gap: 10, marginTop: 14 }}>
                <Button variant="primary" full onPress={() => startConnect.mutate()}>
                  {startConnect.isPending ? t("telegramLogin.connecting") : t("telegramLogin.connectButton")}
                </Button>
              </View>
            ) : null}
          </Card>
        ) : (
          <Card accent>
            <View style={{ flexDirection: "row", justifyContent: "space-between", marginBottom: 8 }}>
              <Pill kind="kind">{t("telegramLogin.stepProgress", { progress })}</Pill>
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
              {t(meta!.titleKey)}
            </Text>
            <Text style={{ ...typography.body, fontSize: 13, color: colors.ash400, marginTop: 6 }}>
              {t(meta!.hintKey)}
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
                placeholder={t(meta!.placeholderKey)}
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
              {submitInput.isPending ? t("telegramLogin.sending") : t(meta!.ctaKey)}
            </Button>
          </Card>
        )}

        {data?.state === "Connected" ? (
          <Pressable onPress={() => disconnect.mutate()} style={{ alignSelf: "center", paddingVertical: 12 }}>
            <Text style={{ ...typography.bodyMd, fontSize: 13, color: colors.bolt400 }}>
              {t("telegramLogin.disconnectButton")}
            </Text>
          </Pressable>
        ) : null}
      </View>
    </Screen>
  );
}

function ConnectionPill({ connection }: { connection: TelegramConnection }) {
  const { t } = useTranslation();
  if (connection.state === "Connected") return <Pill kind="confirmed">{t("telegramLogin.pillOn")}</Pill>;
  if (connection.requiresAction) return <Pill kind="pending">{t("telegramLogin.pillNeedsAction")}</Pill>;
  return <Pill kind="past">{t("telegramLogin.pillOff")}</Pill>;
}

function isStep(value: string | null | undefined): value is Step {
  return value === "phone" || value === "code" || value === "password";
}
