import type { MeetingCard } from "../gateways/meetings-gateway";
import { relativeTimeTo } from "../../../shared/lib/relative-time";

export type TimelineBucket = "yesterday" | "today" | "tomorrow";

export type TimelineEntry = {
  card: MeetingCard;
  at: Date;
  phase: "past" | "live" | "upcoming";
};

export function bucketDate(bucket: TimelineBucket, reference: Date = new Date()): Date {
  const base = new Date(reference);
  base.setHours(0, 0, 0, 0);
  if (bucket === "yesterday") {
    base.setDate(base.getDate() - 1);
  } else if (bucket === "tomorrow") {
    base.setDate(base.getDate() + 1);
  }
  return base;
}

export function isSameDate(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
}

export function filterForBucket(
  cards: ReadonlyArray<MeetingCard>,
  bucket: TimelineBucket,
  reference: Date = new Date(),
): TimelineEntry[] {
  const target = bucketDate(bucket, reference);
  const entries: TimelineEntry[] = [];

  for (const card of cards) {
    const raw = card.dueAt ?? card.observedAt;
    if (!raw) continue;
    const at = new Date(raw);
    if (Number.isNaN(at.getTime())) continue;
    if (!isSameDate(at, target)) continue;

    const relative = relativeTimeTo(at, reference);
    const phase: TimelineEntry["phase"] =
      relative.phase === "past" ? "past" : relative.phase === "live" ? "live" : "upcoming";

    entries.push({ card, at, phase });
  }

  entries.sort((a, b) => a.at.getTime() - b.at.getTime());
  return entries;
}

export function pickNextMeeting(
  cards: ReadonlyArray<MeetingCard>,
  reference: Date = new Date(),
): TimelineEntry | null {
  let best: TimelineEntry | null = null;
  for (const card of cards) {
    const raw = card.dueAt ?? card.observedAt;
    if (!raw) continue;
    const at = new Date(raw);
    if (Number.isNaN(at.getTime())) continue;
    const diff = at.getTime() - reference.getTime();
    if (diff < -15 * 60_000) continue;
    if (!best || at.getTime() < best.at.getTime()) {
      const relative = relativeTimeTo(at, reference);
      const phase: TimelineEntry["phase"] =
        relative.phase === "past" ? "past" : relative.phase === "live" ? "live" : "upcoming";
      best = { card, at, phase };
    }
  }
  return best;
}
