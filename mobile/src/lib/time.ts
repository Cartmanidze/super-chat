import i18n, { intlLocale } from "../i18n";

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

export const TODAY_TIME_ZONE = "Europe/Moscow";

export function dayBoundsInTimeZone(
  now: Date,
  timeZone: string,
): { start: number; end: number } {
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hour12: false,
  }).formatToParts(now);
  const map: Record<string, string> = {};
  for (const p of parts) {
    if (p.type !== "literal") map[p.type] = p.value;
  }
  const localMidnightAsUtc = Date.UTC(
    Number(map.year),
    Number(map.month) - 1,
    Number(map.day),
  );
  const wallNowAsUtc = Date.UTC(
    Number(map.year),
    Number(map.month) - 1,
    Number(map.day),
    Number(map.hour) === 24 ? 0 : Number(map.hour),
    Number(map.minute),
    Number(map.second),
  );
  const tzOffsetMs = wallNowAsUtc - now.getTime();
  const start = localMidnightAsUtc - tzOffsetMs;
  return { start, end: start + DAY };
}

export type RelativePhase = "past" | "live" | "soon" | "today" | "future";

export type RelativeTime = {
  phase: RelativePhase;
  label: string;
};

// Бьём разницу во времени на фазы и формируем читаемую подпись через i18next.
// Плюрализация (минута / минуты / минут) живёт в *_one/_few/_many ключах локалей —
// i18next сам выберет нужную форму по правилам ru/en для текущего значения count.
export function relativeTimeTo(target: Date | string | null, now: Date = new Date()): RelativeTime {
  const t = i18n.t.bind(i18n);
  if (!target) return { phase: "future", label: t("time.unknown") };
  const date = typeof target === "string" ? new Date(target) : target;
  if (Number.isNaN(date.getTime())) return { phase: "future", label: t("time.unknown") };

  const diff = date.getTime() - now.getTime();
  if (diff < -15 * MINUTE) {
    if (Math.abs(diff) < HOUR) return { phase: "past", label: t("time.passed") };
    if (Math.abs(diff) < DAY) {
      const hours = Math.round(Math.abs(diff) / HOUR);
      return { phase: "past", label: t("time.agoHours", { count: hours }) };
    }
    const days = Math.round(Math.abs(diff) / DAY);
    return { phase: "past", label: t("time.agoDays", { count: days }) };
  }
  if (Math.abs(diff) <= 15 * MINUTE) return { phase: "live", label: t("time.now") };
  if (diff < HOUR) {
    const minutes = Math.max(1, Math.round(diff / MINUTE));
    return { phase: "soon", label: t("time.inMinutes", { count: minutes }) };
  }
  if (diff < DAY) {
    const hours = Math.round(diff / HOUR);
    return { phase: "today", label: t("time.inHours", { count: hours }) };
  }
  const days = Math.round(diff / DAY);
  return { phase: "future", label: t("time.inDays", { count: days }) };
}

export function formatClock(value: Date | string | null): string {
  if (!value) return "--:--";
  const d = typeof value === "string" ? new Date(value) : value;
  if (Number.isNaN(d.getTime())) return "--:--";
  return d.toLocaleTimeString(intlLocale(), { hour: "2-digit", minute: "2-digit", hour12: false });
}

export function formatWeekDayShort(value: Date | string | null): string {
  if (!value) return "";
  const d = typeof value === "string" ? new Date(value) : value;
  if (Number.isNaN(d.getTime())) return "";
  return d.toLocaleDateString(intlLocale(), { weekday: "short" });
}
