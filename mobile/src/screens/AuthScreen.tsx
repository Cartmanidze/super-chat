import { useEffect, useMemo, useRef, useState } from "react";
import { Alert, Pressable, Text, TextInput, View } from "react-native";
import { useMutation } from "@tanstack/react-query";
import { LinearGradient } from "expo-linear-gradient";
import { useTranslation } from "react-i18next";
import { authGateway } from "../api/auth";
import { useSessionStore } from "../store/session";
import { Screen } from "../ui/Screen";
import { Header } from "../ui/Header";
import { Button } from "../ui/Button";
import { Eyebrow } from "../ui/Eyebrow";
import { BoltChip } from "../ui/BoltChip";
import { colors, radii, typography } from "../theme/tokens";

const OTP_LENGTH = 6;
const RESEND_SECONDS = 42;

export function AuthScreen() {
  const { t } = useTranslation();
  const setSession = useSessionStore((s) => s.setSession);
  const [email, setEmail] = useState("");
  const [code, setCode] = useState("");
  const [stage, setStage] = useState<"email" | "code">("email");
  const [resend, setResend] = useState(RESEND_SECONDS);
  const codeRef = useRef<TextInput>(null);

  useEffect(() => {
    if (stage !== "code" || resend <= 0) return;
    const t = setInterval(() => setResend((s) => Math.max(0, s - 1)), 1000);
    return () => clearInterval(t);
  }, [stage, resend]);

  const sendCode = useMutation({
    mutationFn: () => authGateway.sendCode(email.trim()),
    onSuccess: () => {
      setStage("code");
      setResend(RESEND_SECONDS);
      setTimeout(() => codeRef.current?.focus(), 100);
    },
    onError: (e: Error) => Alert.alert(t("auth.errorSendCodeTitle"), e.message),
  });

  const verifyCode = useMutation({
    mutationFn: () =>
      authGateway.verifyCode(
        email.trim(),
        code,
        Intl.DateTimeFormat().resolvedOptions().timeZone,
      ),
    onSuccess: async (session) => {
      await setSession(session.accessToken, session.user.email);
    },
    onError: (e: Error) => Alert.alert(t("auth.errorVerifyTitle"), e.message),
  });
  const cells = useMemo(() => {
    const list = code.split("").slice(0, OTP_LENGTH);
    while (list.length < OTP_LENGTH) list.push("");
    return list;
  }, [code]);
  const filled = code.length;
  const canSubmit = filled === OTP_LENGTH && !verifyCode.isPending;

  return (
    <Screen>
      <Header subtitle={stage === "email" ? t("auth.stepEmail") : t("auth.stepCode")} />
      <View style={{ paddingHorizontal: 22, gap: 20 }}>
        <View style={{ alignItems: "center", marginBottom: 10 }}>
          <BoltChip size={56} radius={18} />
        </View>

        {stage === "email" ? (
          <>
            <View style={{ gap: 6 }}>
              <Text
                style={{
                  ...typography.display,
                  fontFamily: "Manrope_800ExtraBold",
                  fontSize: 28,
                  lineHeight: 30,
                  color: colors.bone,
                  letterSpacing: -1,
                }}
              >
                {t("auth.titleEnterEmailLine1")}{"\n"}
                <Text style={{ color: colors.bolt400 }}>{t("auth.titleEnterEmailLine2")}</Text>
              </Text>
              <Text style={{ ...typography.body, fontSize: 13, color: colors.ash400, lineHeight: 20 }}>
                {t("auth.subtitleEnterEmail")}
              </Text>
            </View>

            <View style={{ gap: 10 }}>
              <Eyebrow color={colors.ash500}>{t("auth.yourEmail")}</Eyebrow>
              <View
                style={{
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
                  value={email}
                  onChangeText={setEmail}
                  placeholder={t("auth.emailPlaceholder")}
                  placeholderTextColor={colors.ash500}
                  keyboardType="email-address"
                  autoCapitalize="none"
                  autoCorrect={false}
                  style={{ ...typography.bodySemi, fontSize: 16, color: colors.bone }}
                />
              </View>
            </View>

            <Button
              variant="primary"
              full
              disabled={email.trim().length === 0 || sendCode.isPending}
              onPress={() => sendCode.mutate()}
            >
              {sendCode.isPending ? t("auth.sending") : t("auth.getCodeCta")}
            </Button>
          </>
        ) : (
          <>
            <View style={{ gap: 6 }}>
              <Text
                style={{
                  ...typography.display,
                  fontFamily: "Manrope_800ExtraBold",
                  fontSize: 28,
                  lineHeight: 30,
                  color: colors.bone,
                  letterSpacing: -1,
                }}
              >
                {t("auth.titleEnterCode")}
              </Text>
              <View
                style={{
                  alignSelf: "flex-start",
                  paddingHorizontal: 12,
                  paddingVertical: 8,
                  borderRadius: 10,
                  backgroundColor: "rgba(42,171,238,0.06)",
                  borderWidth: 1,
                  borderColor: "rgba(42,171,238,0.2)",
                }}
              >
                <Text style={{ ...typography.bodyMd, fontSize: 12, color: colors.info }}>{email}</Text>
              </View>
            </View>

            <Pressable onPress={() => codeRef.current?.focus()}>
              <View style={{ flexDirection: "row", gap: 8 }}>
                {cells.map((cell, i) => {
                  const active = i === filled;
                  const has = !!cell;
                  return (
                    <View
                      key={i}
                      style={{
                        flex: 1,
                        aspectRatio: 1 / 1.15,
                        borderRadius: 14,
                        borderWidth: active ? 1.5 : 1,
                        borderColor: has
                          ? "rgba(229,56,59,0.5)"
                          : active
                            ? colors.bolt500
                            : colors.borderLine,
                        backgroundColor: has
                          ? "rgba(229,56,59,0.08)"
                          : active
                            ? "rgba(229,56,59,0.04)"
                            : colors.surfaceLow,
                        alignItems: "center",
                        justifyContent: "center",
                      }}
                    >
                      <Text
                        style={{
                          ...typography.display,
                          fontFamily: "Manrope_800ExtraBold",
                          fontSize: 24,
                          color: colors.bone,
                          letterSpacing: -0.5,
                        }}
                      >
                        {cell}
                      </Text>
                    </View>
                  );
                })}
              </View>
            </Pressable>

            <TextInput
              ref={codeRef}
              value={code}
              onChangeText={(v) => setCode(v.replace(/\D/g, "").slice(0, OTP_LENGTH))}
              keyboardType="number-pad"
              autoComplete="sms-otp"
              maxLength={OTP_LENGTH}
              style={{ position: "absolute", opacity: 0, width: 1, height: 1 }}
            />

            <View
              style={{
                padding: 12,
                borderRadius: 12,
                borderWidth: 1,
                borderColor: colors.borderSoft,
                backgroundColor: colors.surfaceLow,
                flexDirection: "row",
                justifyContent: "space-between",
                alignItems: "center",
                gap: 12,
              }}
            >
              <View style={{ flex: 1 }}>
                <Eyebrow color={colors.ash500}>{t("auth.codeActive")}</Eyebrow>
                <View
                  style={{
                    height: 4,
                    backgroundColor: colors.surfaceMid,
                    borderRadius: 2,
                    marginTop: 6,
                    overflow: "hidden",
                  }}
                >
                  <LinearGradient
                    colors={[colors.success, colors.gold400]}
                    start={{ x: 0, y: 0 }}
                    end={{ x: 1, y: 0 }}
                    style={{ height: "100%", width: `${(resend / RESEND_SECONDS) * 100}%` }}
                  />
                </View>
                <Text style={{ ...typography.mono, fontSize: 10, color: colors.ash400, marginTop: 6 }}>
                  {resend > 0
                    ? t("auth.timeLeft", { seconds: resend.toString().padStart(2, "0") })
                    : t("auth.canResendNow")}
                </Text>
              </View>
              <Pressable
                disabled={resend > 0 || sendCode.isPending}
                onPress={() => sendCode.mutate()}
                style={{
                  paddingHorizontal: 12,
                  paddingVertical: 8,
                  borderRadius: 10,
                  borderWidth: 1,
                  borderColor: colors.borderLine,
                  opacity: resend > 0 ? 0.5 : 1,
                }}
              >
                <Text style={{ ...typography.bodySemi, fontSize: 11, color: colors.ash400 }}>
                  {t("auth.resendButton")}
                </Text>
              </Pressable>
            </View>

            <Button variant="primary" full disabled={!canSubmit} onPress={() => verifyCode.mutate()}>
              {verifyCode.isPending
                ? t("auth.verifying")
                : filled === OTP_LENGTH
                  ? t("auth.verifyCta")
                  : t("auth.enterMore", { count: OTP_LENGTH - filled })}
            </Button>

            <Pressable onPress={() => setStage("email")} style={{ alignSelf: "center", paddingVertical: 8 }}>
              <Text style={{ ...typography.bodyMd, fontSize: 12, color: colors.ash400 }}>
                {t("auth.changeEmail")}
              </Text>
            </Pressable>
          </>
        )}
      </View>
    </Screen>
  );
}
