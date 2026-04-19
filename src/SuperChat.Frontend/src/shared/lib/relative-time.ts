const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

export type RelativePhase = "past" | "live" | "soon" | "today" | "future";

export type RelativeTime = {
  phase: RelativePhase;
  label: string;
};

export function relativeTimeTo(target: Date | string | null, now: Date = new Date()): RelativeTime {
  if (!target) {
    return { phase: "future", label: "время не указано" };
  }

  const targetDate = typeof target === "string" ? new Date(target) : target;
  if (Number.isNaN(targetDate.getTime())) {
    return { phase: "future", label: "время не указано" };
  }

  const diffMs = targetDate.getTime() - now.getTime();
  const absMs = Math.abs(diffMs);

  if (diffMs < -15 * MINUTE) {
    if (absMs < HOUR) {
      return { phase: "past", label: "прошла" };
    }
    if (absMs < DAY) {
      const hours = Math.round(absMs / HOUR);
      return { phase: "past", label: `${hours} ${pluralHours(hours)} назад` };
    }
    const days = Math.round(absMs / DAY);
    return { phase: "past", label: `${days} ${pluralDays(days)} назад` };
  }

  if (Math.abs(diffMs) <= 15 * MINUTE) {
    return { phase: "live", label: "сейчас" };
  }

  if (diffMs < HOUR) {
    const minutes = Math.max(1, Math.round(diffMs / MINUTE));
    return { phase: "soon", label: `через ${minutes} ${pluralMinutes(minutes)}` };
  }

  if (diffMs < DAY) {
    const hours = Math.round(diffMs / HOUR);
    return { phase: "today", label: `через ${hours} ${pluralHours(hours)}` };
  }

  const days = Math.round(diffMs / DAY);
  return { phase: "future", label: `через ${days} ${pluralDays(days)}` };
}

export function formatClockTime(value: Date | string | null, timeZone?: string): string {
  if (!value) {
    return "--:--";
  }
  const date = typeof value === "string" ? new Date(value) : value;
  if (Number.isNaN(date.getTime())) {
    return "--:--";
  }
  return date.toLocaleTimeString("ru-RU", {
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
    timeZone,
  });
}

export function formatDateLong(value: Date | string | null, timeZone?: string): string {
  if (!value) {
    return "";
  }
  const date = typeof value === "string" ? new Date(value) : value;
  if (Number.isNaN(date.getTime())) {
    return "";
  }
  const weekday = date.toLocaleDateString("ru-RU", { weekday: "long", timeZone });
  const day = date.toLocaleDateString("ru-RU", { day: "numeric", month: "long", timeZone });
  return `${capitalize(weekday)}, ${day}`;
}

export function formatDateShort(value: Date | string | null, timeZone?: string): string {
  if (!value) {
    return "";
  }
  const date = typeof value === "string" ? new Date(value) : value;
  if (Number.isNaN(date.getTime())) {
    return "";
  }
  return date.toLocaleDateString("ru-RU", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    timeZone,
  });
}

export function sameDay(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
}

function pluralMinutes(n: number): string {
  const mod10 = n % 10;
  const mod100 = n % 100;
  if (mod10 === 1 && mod100 !== 11) return "минуту";
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20)) return "минуты";
  return "минут";
}

function pluralHours(n: number): string {
  const mod10 = n % 10;
  const mod100 = n % 100;
  if (mod10 === 1 && mod100 !== 11) return "час";
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20)) return "часа";
  return "часов";
}

function pluralDays(n: number): string {
  const mod10 = n % 10;
  const mod100 = n % 100;
  if (mod10 === 1 && mod100 !== 11) return "день";
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20)) return "дня";
  return "дней";
}

function capitalize(value: string): string {
  if (value.length === 0) return value;
  return value.charAt(0).toUpperCase() + value.slice(1);
}
