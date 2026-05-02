import { useState } from "react";
import { ScrollView, Text, View } from "react-native";
import { LinearGradient } from "expo-linear-gradient";
import { SafeAreaView } from "react-native-safe-area-context";
import { useTranslation } from "react-i18next";
import { BoltIcon } from "../ui/BoltIcon";
import { BoltChip } from "../ui/BoltChip";
import { BrandIcon } from "../ui/BrandIcon";
import { Button } from "../ui/Button";
import { Card } from "../ui/Card";
import { Avatar } from "../ui/Avatar";
import { Pill } from "../ui/Pill";
import { Eyebrow } from "../ui/Eyebrow";
import { Dots } from "../ui/Dots";
import { colors, typography } from "../theme/tokens";

type OnboardingScreenProps = {
  onFinish: () => void;
};

export function OnboardingScreen({ onFinish }: OnboardingScreenProps) {
  const { t } = useTranslation();
  const [step, setStep] = useState(0);
  const next = () => setStep((s) => Math.min(3, s + 1));
  const finish = () => onFinish();

  return (
    <View style={{ flex: 1, backgroundColor: colors.ink950 }}>
      <LinearGradient
        colors={["rgba(229,56,59,0.26)", "transparent"]}
        start={{ x: 1, y: 0 }}
        end={{ x: 0.5, y: 0.5 }}
        style={{ position: "absolute", inset: 0 }}
      />
      <SafeAreaView style={{ flex: 1 }}>
        <ScrollView
          style={{ flex: 1 }}
          contentContainerStyle={{ flexGrow: 1, paddingBottom: 24 }}
          showsVerticalScrollIndicator={false}
        >
          {step === 0 ? <SlideHero /> : null}
          {step === 1 ? <SlideHow /> : null}
          {step === 2 ? <SlideValue /> : null}
          {step === 3 ? <SlideCTA /> : null}
        </ScrollView>
        <View style={{ paddingHorizontal: 22, paddingTop: 12, paddingBottom: 24, gap: 12 }}>
          <LinearGradient
            colors={["transparent", "rgba(7,7,7,0.95)"]}
            start={{ x: 0, y: 0 }}
            end={{ x: 0, y: 0.6 }}
            style={{ position: "absolute", left: 0, right: 0, top: -40, height: 40 }}
          />
          <Dots count={4} active={step} style={{ alignSelf: "center" }} />
          {step < 3 ? (
            <Button variant="primary" full onPress={next}>
              {step === 0 ? t("onboarding.startCta") : t("onboarding.nextCta")}
            </Button>
          ) : (
            <>
              <Button variant="primary" full onPress={finish}>
                {t("onboarding.finishCta")}
              </Button>
              <Text
                style={{
                  textAlign: "center",
                  ...typography.body,
                  fontSize: 10.5,
                  color: colors.ash500,
                  paddingHorizontal: 14,
                  lineHeight: 14,
                }}
              >
                {t("onboarding.terms")}
              </Text>
            </>
          )}
        </View>
      </SafeAreaView>
    </View>
  );
}

function SlideHero() {
  const { t } = useTranslation();
  return (
    <View style={{ flex: 1, paddingHorizontal: 24, paddingTop: 56, gap: 20 }}>
      <View style={{ alignItems: "center", marginVertical: 12 }}>
        <View
          style={{
            width: 156,
            height: 156,
            borderRadius: 48,
            alignItems: "center",
            justifyContent: "center",
            overflow: "hidden",
          }}
        >
          <LinearGradient
            colors={["#e5383b", "#6a0b12"]}
            start={{ x: 0, y: 0 }}
            end={{ x: 0.7, y: 1 }}
            style={{ position: "absolute", inset: 0 }}
          />
          <BoltIcon size={88} variant="gold" />
        </View>
      </View>

      <View style={{ gap: 14 }}>
        <Eyebrow color={colors.gold400} style={{ alignSelf: "center" }}>
          {t("onboarding.slide1Eyebrow")}
        </Eyebrow>
        <Text
          style={{
            ...typography.display,
            fontFamily: "Manrope_800ExtraBold",
            fontSize: 32,
            lineHeight: 34,
            color: colors.bone,
            textAlign: "center",
            letterSpacing: -1,
          }}
        >
          {t("onboarding.slide1TitleLine1")}{"\n"}
          <Text style={{ color: colors.bolt400 }}>{t("onboarding.slide1TitleLine2")}</Text>
        </Text>
        <Text
          style={{
            ...typography.body,
            fontSize: 14,
            lineHeight: 22,
            color: colors.ash400,
            textAlign: "center",
          }}
        >
          {t("onboarding.slide1Subtitle")}
        </Text>

        <View
          style={{
            flexDirection: "row",
            alignSelf: "center",
            gap: 10,
            paddingHorizontal: 14,
            paddingVertical: 10,
            borderRadius: 999,
            borderWidth: 1,
            borderColor: colors.borderSoft,
            backgroundColor: colors.surfaceMid,
            alignItems: "center",
          }}
        >
          <BrandIcon kind="tg" size={28} />
          <BrandIcon kind="sl" size={28} />
          <BrandIcon kind="gm" size={28} />
          <BrandIcon kind="wa" size={28} />
          <Text style={{ ...typography.mono, fontSize: 10, color: colors.ash500, paddingLeft: 4 }}>+3</Text>
        </View>
      </View>
    </View>
  );
}

function SlideHow() {
  const { t } = useTranslation();
  return (
    <View style={{ paddingHorizontal: 22, paddingTop: 60, gap: 12 }}>
      <Eyebrow>{t("onboarding.slide2Eyebrow")}</Eyebrow>
      <Text
        style={{
          ...typography.display,
          fontFamily: "Manrope_800ExtraBold",
          fontSize: 26,
          lineHeight: 30,
          color: colors.bone,
          letterSpacing: -0.8,
        }}
      >
        {t("onboarding.slide2Title")}
      </Text>

      <View
        style={{
          padding: 14,
          borderRadius: 18,
          backgroundColor: "rgba(42,171,238,0.08)",
          borderWidth: 1,
          borderColor: "rgba(42,171,238,0.25)",
          alignSelf: "flex-start",
          maxWidth: "90%",
          marginTop: 6,
        }}
      >
        <View style={{ flexDirection: "row", alignItems: "center", gap: 8, marginBottom: 6 }}>
          <Avatar who={t("onboarding.slide2MessageSenderInitials")} size={22} variant="g2" />
          <Text style={{ ...typography.bodySemi, fontSize: 12, color: colors.info }}>
            {t("onboarding.slide2MessageSender")}
          </Text>
          <Text style={{ ...typography.mono, fontSize: 10, color: colors.ash500 }}>
            {t("onboarding.slide2MessageMeta")}
          </Text>
        </View>
        <Text style={{ ...typography.body, fontSize: 13, color: colors.bone, lineHeight: 19 }}>
          {t("onboarding.slide2MessageText")}
        </Text>
      </View>

      <View style={{ alignItems: "center", gap: 4, marginVertical: 8 }}>
        <View style={{ width: 1, height: 16, backgroundColor: colors.bolt500 }} />
        <View
          style={{
            paddingHorizontal: 12,
            paddingVertical: 6,
            borderRadius: 999,
            borderWidth: 1,
            borderColor: "rgba(229,56,59,0.4)",
            backgroundColor: "rgba(229,56,59,0.12)",
            flexDirection: "row",
            alignItems: "center",
            gap: 6,
          }}
        >
          <BoltIcon size={11} variant="red" />
          <Text style={{ ...typography.mono, fontSize: 10, color: colors.bolt400, letterSpacing: 1.6 }}>
            {t("onboarding.slide2Latency")}
          </Text>
        </View>
        <View style={{ width: 1, height: 16, backgroundColor: colors.bolt500 }} />
      </View>

      <Card accent style={{ padding: 14 }}>
        <View style={{ flexDirection: "row", justifyContent: "space-between", marginBottom: 8 }}>
          <Pill kind="kind">{t("onboarding.slide2CardKind")}</Pill>
          <Pill kind="gold">92%</Pill>
        </View>
        <Text
          style={{
            ...typography.heading,
            fontFamily: "Manrope_700Bold",
            fontSize: 17,
            color: colors.bone,
            marginBottom: 6,
          }}
        >
          {t("onboarding.slide2CardTitle")}
        </Text>
        <View style={{ gap: 4 }}>
          <Row label={t("onboarding.slide2RowWhen")} value={t("onboarding.slide2RowWhenValue")} />
          <Row label={t("onboarding.slide2RowWhere")} value={t("onboarding.slide2RowWhereValue")} />
          <View style={{ flexDirection: "row", gap: 12 }}>
            <Text style={{ ...typography.bodyMd, fontSize: 12, color: colors.ash500, width: 50 }}>
              {t("onboarding.slide2RowWho")}
            </Text>
            <View style={{ flexDirection: "row", alignItems: "center" }}>
              <Avatar who={t("onboarding.slide2WhoInitialsA")} size={18} variant="g2" />
              <Avatar who={t("onboarding.slide2WhoInitialsB")} size={18} variant="g1" style={{ marginLeft: -6 }} />
              <Text style={{ ...typography.body, fontSize: 12, color: colors.bone, marginLeft: 6 }}>+2</Text>
            </View>
          </View>
        </View>
      </Card>

      <Text style={{ ...typography.body, fontSize: 13, color: colors.ash400, marginTop: 6 }}>
        {t("onboarding.slide2Privacy")}
      </Text>
    </View>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <View style={{ flexDirection: "row", gap: 12 }}>
      <Text style={{ ...typography.bodyMd, fontSize: 12, color: colors.ash500, width: 50 }}>{label}</Text>
      <Text style={{ ...typography.body, fontSize: 12, color: colors.bone }}>{value}</Text>
    </View>
  );
}

function SlideValue() {
  const { t } = useTranslation();
  const items = [
    {
      ic: "✓",
      c: colors.success,
      titleKey: "onboarding.slide3Item1Title",
      subtitleKey: "onboarding.slide3Item1Subtitle",
    },
    {
      ic: "⟳",
      c: colors.info,
      titleKey: "onboarding.slide3Item2Title",
      subtitleKey: "onboarding.slide3Item2Subtitle",
    },
    {
      ic: "◎",
      c: colors.gold400,
      titleKey: "onboarding.slide3Item3Title",
      subtitleKey: "onboarding.slide3Item3Subtitle",
    },
  ];
  return (
    <View style={{ paddingHorizontal: 22, paddingTop: 60, gap: 14 }}>
      <Eyebrow>{t("onboarding.slide3Eyebrow")}</Eyebrow>
      <Text
        style={{
          ...typography.display,
          fontFamily: "Manrope_800ExtraBold",
          fontSize: 26,
          lineHeight: 30,
          color: colors.bone,
          letterSpacing: -0.8,
        }}
      >
        {t("onboarding.slide3TitleLine1")}{"\n"}{t("onboarding.slide3TitleLine2")}
      </Text>

      <View style={{ gap: 10, marginTop: 4 }}>
        {items.map((it) => (
          <View
            key={it.ic}
            style={{
              padding: 14,
              flexDirection: "row",
              gap: 12,
              borderRadius: 16,
              borderWidth: 1,
              borderColor: colors.borderSoft,
              backgroundColor: colors.ink850,
            }}
          >
            <View
              style={{
                width: 40,
                height: 40,
                borderRadius: 12,
                backgroundColor: `${it.c}26`,
                borderWidth: 1,
                borderColor: `${it.c}55`,
                alignItems: "center",
                justifyContent: "center",
              }}
            >
              <Text style={{ ...typography.heading, fontSize: 20, color: it.c }}>{it.ic}</Text>
            </View>
            <View style={{ flex: 1 }}>
              <Text
                style={{
                  ...typography.heading,
                  fontFamily: "Manrope_700Bold",
                  fontSize: 14,
                  color: colors.bone,
                  marginBottom: 3,
                }}
              >
                {t(it.titleKey)}
              </Text>
              <Text style={{ ...typography.body, fontSize: 12, color: colors.ash400, lineHeight: 17 }}>
                {t(it.subtitleKey)}
              </Text>
            </View>
          </View>
        ))}
      </View>

      <View
        style={{
          padding: 14,
          borderRadius: 16,
          borderWidth: 1,
          borderColor: "rgba(230,181,74,0.28)",
          backgroundColor: "rgba(230,181,74,0.04)",
          flexDirection: "row",
          justifyContent: "space-around",
          alignItems: "baseline",
          marginTop: 4,
        }}
      >
        <View style={{ alignItems: "center" }}>
          <Text style={{ ...typography.display, fontFamily: "Manrope_800ExtraBold", fontSize: 26, color: colors.gold400 }}>
            87%
          </Text>
          <Text style={{ ...typography.mono, fontSize: 9, color: colors.ash400, marginTop: 6, textAlign: "center" }}>
            {t("onboarding.slide3StatConfidenceLine1")}{"\n"}{t("onboarding.slide3StatConfidenceLine2")}
          </Text>
        </View>
        <View style={{ width: 1, height: 40, backgroundColor: colors.borderLine }} />
        <View style={{ alignItems: "center" }}>
          <Text style={{ ...typography.display, fontFamily: "Manrope_800ExtraBold", fontSize: 26, color: colors.bolt400 }}>
            {t("onboarding.slide3StatPerMessageValue")}
          </Text>
          <Text style={{ ...typography.mono, fontSize: 9, color: colors.ash400, marginTop: 6, textAlign: "center" }}>
            {t("onboarding.slide3StatPerMessageLine1")}{"\n"}{t("onboarding.slide3StatPerMessageLine2")}
          </Text>
        </View>
      </View>
    </View>
  );
}

function SlideCTA() {
  const { t } = useTranslation();
  return (
    <View style={{ paddingHorizontal: 22, paddingTop: 56, gap: 22 }}>
      <View style={{ alignItems: "center", gap: 14 }}>
        <BoltChip size={64} radius={20} />
        <Eyebrow color={colors.gold400}>{t("onboarding.slide4Eyebrow")}</Eyebrow>
      </View>
      <View style={{ alignItems: "center", gap: 10 }}>
        <Text
          style={{
            ...typography.display,
            fontFamily: "Manrope_800ExtraBold",
            fontSize: 30,
            lineHeight: 32,
            color: colors.bone,
            textAlign: "center",
            letterSpacing: -1,
          }}
        >
          {t("onboarding.slide4TitleLine1")}{"\n"}
          <Text style={{ color: colors.bolt400 }}>{t("onboarding.slide4TitleLine2")}</Text>
        </Text>
        <Text
          style={{
            ...typography.body,
            fontSize: 13,
            color: colors.ash400,
            textAlign: "center",
            paddingHorizontal: 14,
            lineHeight: 20,
          }}
        >
          {t("onboarding.slide4Subtitle")}
        </Text>
      </View>
    </View>
  );
}
