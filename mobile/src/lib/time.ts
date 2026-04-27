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

export function relativeTimeTo(target: Date | string | null, now: Date = new Date()): RelativeTime {
  if (!target) return { phase: "future", label: "время не указано" };
  const date = typeof target === "string" ? new Date(target) : target;
  if (Number.isNaN(date.getTime())) return { phase: "future", label: "время не указано" };

  const diff = date.getTime() - now.getTime();
  if (diff < -15 * MINUTE) {
    if (Math.abs(diff) < HOUR) return { phase: "past", label: "прошла" };
    if (Math.abs(diff) < DAY) {
      const hours = Math.round(Math.abs(diff) / HOUR);
      return { phase: "past", label: `${hours} ${pluralHours(hours)} назад` };
    }
    const days = Math.round(Math.abs(diff) / DAY);
    return { phase: "past", label: `${days} ${pluralDays(days)} назад` };
  }
  if (Math.abs(diff) <= 15 * MINUTE) return { phase: "live", label: "сейчас" };
  if (diff < HOUR) {
    const minutes = Math.max(1, Math.round(diff / MINUTE));
    return { phase: "soon", label: `через ${minutes} ${pluralMin(minutes)}` };
  }
  if (diff < DAY) {
    const hours = Math.round(diff / HOUR);
    return { phase: "today", label: `через ${hours} ${pluralHours(hours)}` };
  }
  const days = Math.round(diff / DAY);
  return { phase: "future", label: `через ${days} ${pluralDays(days)}` };
}

export function formatClock(value: Date | string | null): string {
  if (!value) return "--:--";
  const d = typeof value === "string" ? new Date(value) : value;
  if (Number.isNaN(d.getTime())) return "--:--";
  return d.toLocaleTimeString("ru-RU", { hour: "2-digit", minute: "2-digit", hour12: false });
}

export function formatWeekDayShort(value: Date | string | null): string {
  if (!value) return "";
  const d = typeof value === "string" ? new Date(value) : value;
  if (Number.isNaN(d.getTime())) return "";
  return d.toLocaleDateString("ru-RU", { weekday: "short" });
}

function pluralMin(n: number): string {
  const m10 = n % 10;
  const m100 = n % 100;
  if (m10 === 1 && m100 !== 11) return "минуту";
  if (m10 >= 2 && m10 <= 4 && (m100 < 10 || m100 >= 20)) return "минуты";
  return "минут";
}

function pluralHours(n: number): string {
  const m10 = n % 10;
  const m100 = n % 100;
  if (m10 === 1 && m100 !== 11) return "час";
  if (m10 >= 2 && m10 <= 4 && (m100 < 10 || m100 >= 20)) return "часа";
  return "часов";
}

function pluralDays(n: number): string {
  const m10 = n % 10;
  const m100 = n % 100;
  if (m10 === 1 && m100 !== 11) return "день";
  if (m10 >= 2 && m10 <= 4 && (m100 < 10 || m100 >= 20)) return "дня";
  return "дней";
}
