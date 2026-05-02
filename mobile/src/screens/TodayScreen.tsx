import { useEffect, useMemo, useState } from "react";
import { ActivityIndicator, Alert, Linking, Pressable, Text, View } from "react-native";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useNavigation } from "@react-navigation/native";
import { useTranslation } from "react-i18next";
import { meetingsGateway, type MeetingCard } from "../api/meetings";
import { useSessionStore } from "../store/session";
import { Screen } from "../ui/Screen";
import { Header } from "../ui/Header";
import { Card } from "../ui/Card";
import { Pill } from "../ui/Pill";
import { Avatar } from "../ui/Avatar";
import { Button } from "../ui/Button";
import { Eyebrow } from "../ui/Eyebrow";
import { colors, radii, typography } from "../theme/tokens";
import { TODAY_TIME_ZONE, dayBoundsInTimeZone, formatClock, relativeTimeTo } from "../lib/time";
import { intlLocale } from "../i18n";

function avatarInitials(value: string | null | undefined): string {
  if (!value) return "·";
  const cleaned = value.replace(/[#@]/g, "").trim();
  const parts = cleaned.split(/\s+/).filter(Boolean);
  if (parts.length === 0) return "·";
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[1][0]).toUpperCase();
}

function profileInitials(email: string | null | undefined, fallback: string): string {
  if (!email) return fallback;
  const local = email.split("@")[0] ?? email;
  return local.slice(0, 2).toUpperCase();
}

function avatarVariant(value: string | null | undefined): "g1" | "g2" | "g3" | "g4" {
  if (!value) return "g3";
  const code = [...value].reduce((sum, ch) => sum + ch.charCodeAt(0), 0);
  return (["g1", "g2", "g3", "g4"] as const)[code % 4];
}

function pickNext(cards: MeetingCard[], now: Date): MeetingCard | null {
  let best: MeetingCard | null = null;
  let bestTs = Infinity;
  for (const c of cards) {
    const at = c.dueAt ?? c.observedAt;
    if (!at) continue;
    const t = new Date(at).getTime();
    if (Number.isNaN(t)) continue;
    if (t < now.getTime() - 15 * 60_000) continue;
    if (t < bestTs) {
      bestTs = t;
      best = c;
    }
  }
  return best;
}

export function TodayScreen() {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const navigation = useNavigation();
  const token = useSessionStore((s) => s.accessToken);
  const email = useSessionStore((s) => s.email);
  const meetings = useQuery({
    queryKey: ["meetings"],
    queryFn: () => meetingsGateway.list(token!),
    enabled: Boolean(token),
  });

  const invalidateMeetings = () =>
    queryClient.invalidateQueries({ queryKey: ["meetings"] });

  // Optimistic-апдейты: кэш ["meetings"] меняем в момент клика, чтобы UI
  // не ждал двух round-trip-ов к серверу. На ошибке откатываем кэш и
  // показываем Alert. На любом исходе фоновый refetch для консистентности.
  const confirmMeeting = useMutation({
    mutationFn: (id: string) => meetingsGateway.confirm(token!, id),
    onMutate: async (id) => {
      await queryClient.cancelQueries({ queryKey: ["meetings"] });
      const previous = queryClient.getQueryData<MeetingCard[]>(["meetings"]);
      queryClient.setQueryData<MeetingCard[]>(["meetings"], (old = []) =>
        old.map((m) => (m.id === id ? { ...m, status: "Confirmed" } : m)),
      );
      return { previous };
    },
    onError: (e: Error, _id, ctx) => {
      if (ctx?.previous !== undefined) {
        queryClient.setQueryData(["meetings"], ctx.previous);
      }
      Alert.alert(t("today.confirmErrorTitle"), e.message);
    },
    onSettled: invalidateMeetings,
  });

  const dismissMeeting = useMutation({
    mutationFn: (id: string) => meetingsGateway.dismiss(token!, id),
    onMutate: async (id) => {
      await queryClient.cancelQueries({ queryKey: ["meetings"] });
      const previous = queryClient.getQueryData<MeetingCard[]>(["meetings"]);
      queryClient.setQueryData<MeetingCard[]>(["meetings"], (old = []) =>
        old.filter((m) => m.id !== id),
      );
      return { previous };
    },
    onError: (e: Error, _id, ctx) => {
      if (ctx?.previous !== undefined) {
        queryClient.setQueryData(["meetings"], ctx.previous);
      }
      Alert.alert(t("today.dismissErrorTitle"), e.message);
    },
    onSettled: invalidateMeetings,
  });

  const openMeetingUrl = async (url: string) => {
    try {
      const supported = await Linking.canOpenURL(url);
      if (!supported) {
        Alert.alert(t("today.openLinkErrorTitle"), url);
        return;
      }
      await Linking.openURL(url);
    } catch (e) {
      Alert.alert(t("today.openLinkErrorTitle"), String(e));
    }
  };

  const [now, setNow] = useState(() => new Date());
  useEffect(() => {
    const t = setInterval(() => setNow(new Date()), 60_000);
    return () => clearInterval(t);
  }, []);

  const cards = meetings.data ?? [];
  const next = useMemo(() => pickNext(cards, now), [cards, now]);
  const pending = useMemo(() => cards.filter((c) => c.status === "PendingConfirmation"), [cards]);

  const todayCount = useMemo(() => {
    const { start, end } = dayBoundsInTimeZone(now, TODAY_TIME_ZONE);
    return cards.filter((c) => {
      const at = c.dueAt ?? c.observedAt;
      if (!at) return false;
      const ts = new Date(at).getTime();
      return ts >= start && ts < end;
    }).length;
  }, [cards, now]);

  const headlineRel = next ? relativeTimeTo(next.dueAt ?? next.observedAt, now) : null;
  // i18n.language нужен в зависимости только чтобы пересчитать formatWeekDay
  // при переключении языка прямо в раннере приложения.
  const weekday = useMemo(() => formatWeekDay(now, i18n.language), [now, i18n.language]);

  return (
    <Screen>
      <Header
        subtitle={t("today.weekdayClock", { weekday, clock: formatClock(now) })}
        title={
          <View>
            <Text
              style={{
                ...typography.display,
                fontFamily: "Manrope_800ExtraBold",
                fontSize: 24,
                lineHeight: 27,
                color: colors.bone,
                letterSpacing: -0.6,
              }}
            >
              {todayCount > 0
                ? t("today.todayCount", { count: todayCount })
                : t("today.freeDay")}
            </Text>
            {headlineRel ? (
              <Text
                style={{
                  ...typography.display,
                  fontFamily: "Manrope_800ExtraBold",
                  fontSize: 24,
                  lineHeight: 27,
                  color: colors.bolt400,
                  letterSpacing: -0.6,
                }}
              >
                {t("today.nextMeetingPrefix", { label: headlineRel.label })}
              </Text>
            ) : null}
          </View>
        }
        right={
          <Pressable
            onPress={() => navigation.navigate("Profile" as never)}
            hitSlop={8}
            accessibilityRole="button"
            accessibilityLabel={t("today.openProfileA11y")}
          >
            <Avatar who={profileInitials(email, t("profile.guest").slice(0, 1).toUpperCase())} size={32} variant="g2" />
          </Pressable>
        }
      />

      <View style={{ paddingHorizontal: 16, gap: 14 }}>
        {next ? (
          <NextMeetingHero
            card={next}
            now={now}
            onOpenUrl={openMeetingUrl}
            onConfirm={(id) => confirmMeeting.mutate(id)}
            onDismiss={(id) => dismissMeeting.mutate(id)}
            isConfirming={confirmMeeting.isPending}
            isDismissing={dismissMeeting.isPending}
          />
        ) : null}

        {pending.length > 0 ? (
          <>
            <SectionHead title={t("today.pendingSection")} count={pending.length} />
            <View style={{ gap: 6 }}>
              {pending.map((c) => (
                <PendingRow
                  key={c.id ?? c.title}
                  card={c}
                  onConfirm={(id) => confirmMeeting.mutate(id)}
                  onDismiss={(id) => dismissMeeting.mutate(id)}
                  isBusy={confirmMeeting.isPending || dismissMeeting.isPending}
                />
              ))}
            </View>
          </>
        ) : null}

        <SectionHead title={t("today.scheduleSection")} count={todayCount > 0 ? todayCount : null} />
        <View style={{ paddingHorizontal: 4 }}>
          {cards.length === 0 && !meetings.isLoading ? (
            <Card>
              <Text style={{ ...typography.heading, fontFamily: "Manrope_700Bold", fontSize: 16, color: colors.bone }}>
                {t("today.emptyTitle")}
              </Text>
              <Text style={{ ...typography.body, fontSize: 13, color: colors.ash400, marginTop: 6 }}>
                {t("today.emptySubtitle")}
              </Text>
            </Card>
          ) : null}
          {cards.map((c) => (
            <TimelineRow key={c.id ?? c.title} card={c} now={now} />
          ))}
        </View>
      </View>
    </Screen>
  );
}

type NextMeetingHeroProps = {
  card: MeetingCard;
  now: Date;
  onOpenUrl: (url: string) => void;
  onConfirm: (id: string) => void;
  onDismiss: (id: string) => void;
  isConfirming: boolean;
  isDismissing: boolean;
};

function NextMeetingHero({
  card,
  now,
  onOpenUrl,
  onConfirm,
  onDismiss,
  isConfirming,
  isDismissing,
}: NextMeetingHeroProps) {
  const { t } = useTranslation();
  const rel = relativeTimeTo(card.dueAt ?? card.observedAt, now);
  const at = card.dueAt ?? card.observedAt;
  const isPending = card.status === "PendingConfirmation";
  const hasActions = Boolean(card.id) || Boolean(card.meetingJoinUrl);
  return (
    <Card accent>
      <View style={{ flexDirection: "row", justifyContent: "space-between", marginBottom: 10 }}>
        <Pill kind="kind">⚡ {rel.phase === "live" ? t("today.now") : rel.label}</Pill>
        <Pill kind="gold">{Math.round(card.confidence * 100)}%</Pill>
      </View>
      <Text
        numberOfLines={2}
        ellipsizeMode="tail"
        style={{
          ...typography.heading,
          fontFamily: "Manrope_700Bold",
          fontSize: 19,
          color: colors.bone,
          letterSpacing: -0.4,
          marginBottom: 6,
        }}
      >
        {card.title}
      </Text>
      <Text
        numberOfLines={2}
        ellipsizeMode="tail"
        style={{ ...typography.body, fontSize: 12, color: colors.ash400, lineHeight: 18, marginBottom: 12 }}
      >
        <Text style={{ color: colors.bolt400 }}>{card.chatTitle}</Text>
        {card.summary ? ` · ${card.summary}` : ""}
      </Text>
      <View style={{ flexDirection: "row", alignItems: "baseline", gap: 10, marginBottom: 14 }}>
        <Text
          style={{
            ...typography.display,
            fontFamily: "Manrope_800ExtraBold",
            fontSize: 34,
            lineHeight: 34,
            color: colors.bone,
            letterSpacing: -1,
          }}
        >
          {formatClock(at)}
        </Text>
        <Text style={{ ...typography.mono, fontSize: 10, color: colors.bolt400, letterSpacing: 1.4, textTransform: "uppercase" }}>
          {card.meetingProvider ?? t("today.providerFallback")}
        </Text>
      </View>
      {hasActions ? (
        // Раскладка из двух уровней. Верхний — основная ссылка на встречу
        // (full-width), нижний — confirm + dismiss. Раньше всё лежало в одной
        // строке, и три кнопки на 360 px не помещались: текст "Подтвердить"
        // обрезался. Теперь основной CTA сам по себе, а пара действий ниже.
        <View style={{ gap: 8 }}>
          {card.meetingJoinUrl ? (
            <Button
              variant="primary"
              full
              style={{ minHeight: 42 }}
              onPress={() => onOpenUrl(card.meetingJoinUrl!)}
            >
              {t("today.openLinkCta")}
            </Button>
          ) : null}
          {card.id && isPending ? (
            <View style={{ flexDirection: "row", gap: 8 }}>
              <Button
                variant="ghost"
                style={{ flex: 1, minHeight: 42 }}
                disabled={isConfirming}
                onPress={() => onConfirm(card.id!)}
              >
                {isConfirming ? <ActivityIndicator size="small" color={colors.bone} /> : t("today.confirmCta")}
              </Button>
              <Button
                variant="ghost"
                style={{ flex: 1, minHeight: 42 }}
                disabled={isDismissing}
                onPress={() => onDismiss(card.id!)}
              >
                {isDismissing ? <ActivityIndicator size="small" color={colors.bone} /> : t("today.dismissCta")}
              </Button>
            </View>
          ) : null}
        </View>
      ) : null}
    </Card>
  );
}

type PendingRowProps = {
  card: MeetingCard;
  onConfirm: (id: string) => void;
  onDismiss: (id: string) => void;
  isBusy: boolean;
};

function PendingRow({ card, onConfirm, onDismiss, isBusy }: PendingRowProps) {
  const { t } = useTranslation();
  const canAct = card.id != null && !isBusy;
  return (
    <View
      style={{
        padding: 12,
        borderRadius: radii.md,
        backgroundColor: colors.ink850,
        borderWidth: 1,
        borderColor: "rgba(230,181,74,0.2)",
        flexDirection: "row",
        gap: 10,
        alignItems: "center",
      }}
    >
      <View style={{ flex: 1 }}>
        <View style={{ flexDirection: "row", alignItems: "center", gap: 6, marginBottom: 4 }}>
          <Pill kind="pending">? {Math.round(card.confidence * 100)}%</Pill>
          {card.dueAt ? (
            <Text style={{ ...typography.mono, fontSize: 10, color: colors.ash500, letterSpacing: 0.8 }}>
              {formatClock(card.dueAt)}
            </Text>
          ) : null}
        </View>
        <Text
          numberOfLines={2}
          ellipsizeMode="tail"
          style={{ ...typography.heading, fontFamily: "Manrope_700Bold", fontSize: 14, color: colors.bone, letterSpacing: -0.3 }}
        >
          {card.title}
        </Text>
        <Text
          numberOfLines={1}
          ellipsizeMode="tail"
          style={{ ...typography.body, fontSize: 11, color: colors.ash400, marginTop: 2 }}
        >
          {t("today.fromChatPrefix")}<Text style={{ color: colors.bolt400 }}>{card.chatTitle}</Text>
        </Text>
      </View>
      {card.id != null ? (
        <View style={{ flexDirection: "row", gap: 6 }}>
          <Pressable
            disabled={!canAct}
            onPress={() => onConfirm(card.id!)}
            accessibilityLabel={t("today.confirmCta")}
            style={{
              width: 34,
              height: 34,
              borderRadius: 10,
              borderWidth: 1,
              borderColor: colors.successBorder,
              backgroundColor: colors.successBg,
              alignItems: "center",
              justifyContent: "center",
              opacity: canAct ? 1 : 0.5,
            }}
          >
            <Text style={{ color: colors.success, fontSize: 16 }}>✓</Text>
          </Pressable>
          <Pressable
            disabled={!canAct}
            onPress={() => onDismiss(card.id!)}
            accessibilityLabel={t("today.dismissCta")}
            style={{
              width: 34,
              height: 34,
              borderRadius: 10,
              borderWidth: 1,
              borderColor: colors.borderLine,
              backgroundColor: colors.surfaceLow,
              alignItems: "center",
              justifyContent: "center",
              opacity: canAct ? 1 : 0.5,
            }}
          >
            <Text style={{ color: colors.ash400, fontSize: 16 }}>✕</Text>
          </Pressable>
        </View>
      ) : null}
    </View>
  );
}

function TimelineRow({ card, now }: { card: MeetingCard; now: Date }) {
  const { t } = useTranslation();
  const at = card.dueAt ?? card.observedAt;
  const rel = relativeTimeTo(at, now);
  const isPast = rel.phase === "past";
  const isLive = rel.phase === "live";
  return (
    <View style={{ flexDirection: "row", gap: 10, paddingVertical: 12, opacity: isPast ? 0.55 : 1 }}>
      <View
        style={{
          width: 48,
          paddingTop: 4,
          borderRightWidth: 1,
          borderRightColor: colors.borderSoft,
          paddingRight: 10,
        }}
      >
        <Text
          style={{
            ...typography.mono,
            fontSize: 10,
            color: isLive ? colors.bolt400 : colors.ash500,
            letterSpacing: 0.8,
            textTransform: "uppercase",
          }}
        >
          {formatClock(at)}
        </Text>
      </View>
      <View style={{ flex: 1, gap: 4 }}>
        <Text
          numberOfLines={2}
          ellipsizeMode="tail"
          style={{
            ...typography.heading,
            fontFamily: "Manrope_700Bold",
            fontSize: 15,
            color: colors.bone,
            letterSpacing: -0.3,
          }}
        >
          {card.title}
        </Text>
        {card.summary ? (
          <Text
            numberOfLines={2}
            ellipsizeMode="tail"
            style={{ ...typography.body, fontSize: 12, color: colors.ash400, lineHeight: 18 }}
          >
            {card.summary}
          </Text>
        ) : null}
        <View style={{ flexDirection: "row", alignItems: "center", gap: 8, marginTop: 4 }}>
          <Avatar who={avatarInitials(card.chatTitle)} size={20} variant={avatarVariant(card.chatTitle)} />
          <Text
            numberOfLines={1}
            ellipsizeMode="tail"
            style={{ ...typography.mono, fontSize: 10, color: colors.bolt400, letterSpacing: 0.8, flexShrink: 1 }}
          >
            {card.chatTitle}
          </Text>
          <Text style={{ ...typography.mono, fontSize: 10, color: colors.ash500, letterSpacing: 0.8 }}>
            · {isPast ? t("today.past") : isLive ? t("today.live") : rel.label}
          </Text>
        </View>
      </View>
    </View>
  );
}

function SectionHead({ title, count }: { title: string; count: number | null }) {
  return (
    <View style={{ flexDirection: "row", justifyContent: "space-between", alignItems: "baseline", paddingHorizontal: 4, paddingTop: 4 }}>
      <Text
        style={{
          ...typography.heading,
          fontFamily: "Manrope_800ExtraBold",
          fontSize: 16,
          color: colors.bone,
          letterSpacing: -0.4,
        }}
      >
        {title}
      </Text>
      {count !== null && count > 0 ? <Eyebrow color={colors.ash500}>{String(count)}</Eyebrow> : null}
    </View>
  );
}

function formatWeekDay(d: Date, _language: string): string {
  const wd = d.toLocaleDateString(intlLocale(), { weekday: "short" });
  return wd.charAt(0).toUpperCase() + wd.slice(1);
}
