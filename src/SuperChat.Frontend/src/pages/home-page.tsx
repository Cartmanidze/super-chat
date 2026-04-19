import { Link } from "@tanstack/react-router";
import { useEffect, useMemo, useState } from "react";
import { useSessionStore } from "../features/auth/stores/session-store";
import { useMeQuery } from "../features/me/hooks/use-me-query";
import { useMeetingsQuery } from "../features/meetings/hooks/use-meetings-query";
import { filterForBucket, pickNextMeeting, type TimelineBucket } from "../features/meetings/lib/timeline";
import type { MeetingCard } from "../features/meetings/gateways/meetings-gateway";
import { avatarInitials, avatarTint } from "../shared/lib/avatar";
import { formatClockTime, relativeTimeTo } from "../shared/lib/relative-time";
import { ParticipantStack } from "../shared/ui/participant-stack";
import { PageSection } from "../shared/ui/page-section";

const BUCKETS: { id: TimelineBucket; label: string }[] = [
  { id: "yesterday", label: "Вчера" },
  { id: "today", label: "Сегодня" },
  { id: "tomorrow", label: "Завтра" },
];

function formatConfidence(value: number) {
  return `${Math.round(Math.max(0, Math.min(1, value)) * 100)}%`;
}

function participantsFromCard(card: MeetingCard): string[] {
  const names = new Set<string>();
  if (card.sourceRoom) {
    names.add(card.sourceRoom);
  }
  return Array.from(names);
}

export function HomePage() {
  const token = useSessionStore((state) => state.accessToken);
  const meQuery = useMeQuery(token);
  const meetingsQuery = useMeetingsQuery(token);

  const [now, setNow] = useState(() => new Date());
  useEffect(() => {
    const timer = setInterval(() => setNow(new Date()), 60_000);
    return () => clearInterval(timer);
  }, []);

  const [bucket, setBucket] = useState<TimelineBucket>("today");

  const meetings = useMemo(() => meetingsQuery.data ?? [], [meetingsQuery.data]);
  const todayEntries = useMemo(() => filterForBucket(meetings, "today", now), [meetings, now]);
  const bucketEntries = useMemo(() => filterForBucket(meetings, bucket, now), [meetings, bucket, now]);
  const nextMeeting = useMemo(() => pickNextMeeting(meetings, now), [meetings, now]);

  if (!token) {
    return (
      <PageSection>
        <section className="hero">
          <div>
            <div className="hero-eye">Super Chat</div>
            <h1>
              Не теряйте встречи.
              <br />
              <span>Все договорённости — на одной странице.</span>
            </h1>
            <p>
              Super Chat читает ваши чаты и почту, находит договорённости и напоминает вовремя. Войдите, чтобы
              начать.
            </p>
            <div className="hero-ctas">
              <Link to="/auth" className="btn is-primary">
                Открыть вход
              </Link>
            </div>
          </div>
          <aside className="next-card">
            <div className="next-card-row">
              <span className="pill is-kind">Как это работает</span>
              <span className="pill is-gold">Шаги</span>
            </div>
            <h3>Три шага до первых встреч</h3>
            <p className="meta">Код по почте → подключение Telegram → расписание дня.</p>
            <ul className="auth-list" style={{ margin: 0 }}>
              <li>
                <span className="auth-num">01</span>
                <div>
                  <b>Войти по коду</b>
                  <em>Код придёт на почту. Действует 10 минут.</em>
                </div>
              </li>
              <li>
                <span className="auth-num">02</span>
                <div>
                  <b>Подключить Telegram</b>
                  <em>Нужен доступ к чатам, чтобы находить договорённости.</em>
                </div>
              </li>
              <li>
                <span className="auth-num">03</span>
                <div>
                  <b>Получить расписание</b>
                  <em>Super Chat сам соберёт ближайшие встречи и задачи.</em>
                </div>
              </li>
            </ul>
          </aside>
        </section>
      </PageSection>
    );
  }

  if (meQuery.isSuccess && meQuery.data.requiresTelegramAction) {
    return (
      <PageSection>
        <section className="hero">
          <div>
            <div className="hero-eye">Первый шаг</div>
            <h1>
              Нужно подключить<br />
              <span>Telegram.</span>
            </h1>
            <p>
              После подключения здесь появится расписание дня из ваших чатов — ближайшие встречи, задачи и
              ожидания.
            </p>
            <div className="hero-ctas">
              <Link to="/settings/connections" className="btn is-primary">
                Открыть подключения
              </Link>
            </div>
          </div>
        </section>
      </PageSection>
    );
  }

  const upcomingCount = todayEntries.filter((entry) => entry.phase !== "past").length;
  const headline = upcomingCount === 0
    ? "Сегодня встреч больше нет."
    : `${upcomingCount} ${pluralMeetings(upcomingCount)}.`;

  const nextRelative = nextMeeting ? relativeTimeTo(nextMeeting.at, now) : null;
  const nextAccent = nextMeeting && nextRelative
    ? nextRelative.phase === "live"
      ? "Начинается прямо сейчас."
      : `Ближайшая — ${nextRelative.label}.`
    : null;

  return (
    <PageSection>
      <section className="hero">
        <div>
          <div className="hero-eye">Сегодня</div>
          <h1>
            {headline}
            {nextAccent ? (
              <>
                <br />
                <span>{nextAccent}</span>
              </>
            ) : null}
          </h1>
          <p>
            Super Chat собрал расписание дня из Telegram. Ничего не потерялось — всё на одной странице.
          </p>
          <div className="hero-ctas">
            <Link to="/today" className="btn is-primary">
              Открыть встречи
            </Link>
            <Link to="/search" search={{ q: "" }} className="btn is-ghost">
              Поиск по чатам
            </Link>
          </div>
        </div>

        {nextMeeting ? <NextMeetingCard entry={nextMeeting} now={now} /> : <EmptyNextCard />}
      </section>

      <div className="section-head" style={{ marginTop: 24 }}>
        <h2>
          Расписание дня <em>· {bucketEntries.length} {pluralMeetings(bucketEntries.length)}</em>
        </h2>
        <div className="day-nav">
          {BUCKETS.map((item) => (
            <button
              key={item.id}
              type="button"
              className={bucket === item.id ? "is-active" : undefined}
              onClick={() => setBucket(item.id)}
            >
              {item.label}
            </button>
          ))}
        </div>
      </div>

      <section className="schedule">
        {meetingsQuery.isLoading ? (
          <p className="form-note">Загружаем встречи…</p>
        ) : meetingsQuery.isError ? (
          <p className="form-error">Не удалось загрузить встречи: {String(meetingsQuery.error.message)}</p>
        ) : bucketEntries.length === 0 ? (
          <p className="form-note">
            {bucket === "yesterday"
              ? "Вчера встреч не было."
              : bucket === "tomorrow"
                ? "На завтра встреч ещё не назначено."
                : "На сегодня встреч больше нет."}
          </p>
        ) : (
          <div className="tl-grid">
            {bucketEntries.map((entry, index) => (
              <TimelineRow key={`${entry.card.id ?? entry.card.title}-${index}`} entry={entry} now={now} />
            ))}
          </div>
        )}
      </section>
    </PageSection>
  );
}

function NextMeetingCard({ entry, now }: { entry: ReturnType<typeof pickNextMeeting> & {}; now: Date }) {
  if (!entry) return null;
  const relative = relativeTimeTo(entry.at, now);
  const participants = participantsFromCard(entry.card);
  const joinUrl = entry.card.meetingJoinUrl;

  return (
    <aside className="next-card">
      <div className="next-card-row">
        <span className="pill is-kind">
          {relative.phase === "live" ? "⚡ Сейчас" : relative.phase === "soon" ? `⚡ ${relative.label}` : relative.label}
        </span>
        <span className="pill is-gold">{formatConfidence(entry.card.confidence)} уверенность</span>
      </div>
      <h3>{entry.card.title}</h3>
      <p className="meta">
        {entry.card.sourceRoom} · {entry.card.summary}
      </p>
      <div className="when">
        <span className="when-big">{formatClockTime(entry.at)}</span>
        <span className="when-lbl">
          {entry.card.meetingProvider ?? "Встреча"}
        </span>
      </div>
      <ParticipantStack names={participants} max={4} />
      <div className="next-card-actions">
        {joinUrl ? (
          <a className="btn is-primary is-sm" href={joinUrl} target="_blank" rel="noreferrer">
            Открыть ссылку
          </a>
        ) : null}
        <Link to="/today" className="btn is-ghost is-sm">
          Все встречи
        </Link>
      </div>
    </aside>
  );
}

function EmptyNextCard() {
  return (
    <aside className="next-card">
      <div className="next-card-row">
        <span className="pill is-muted">Тишина</span>
      </div>
      <h3>Ближайших встреч нет</h3>
      <p className="meta">Когда Super Chat найдёт новую договорённость, она появится здесь первой.</p>
      <div className="next-card-actions">
        <Link to="/today" className="btn is-ghost is-sm">
          Все встречи
        </Link>
      </div>
    </aside>
  );
}

function TimelineRow({ entry, now }: { entry: ReturnType<typeof filterForBucket>[number]; now: Date }) {
  const relative = relativeTimeTo(entry.at, now);
  const timeClass = entry.phase === "live" ? "tl-time is-now" : "tl-time";
  const cardClass = entry.phase === "live" ? "tl-card is-live" : entry.phase === "past" ? "tl-card is-past" : "tl-card";
  const durationLabel = entry.phase === "past"
    ? "прошла"
    : entry.phase === "live"
      ? "идёт"
      : relative.label;
  const boltContent = entry.phase === "past" ? "✓" : "⚡";
  const names = participantsFromCard(entry.card);

  return (
    <div className="tl-row">
      <div className={timeClass}>{formatClockTime(entry.at)}</div>
      <div className="tl-body">
        <div className={cardClass}>
          <div className="tl-bolt">{boltContent}</div>
          <div className="tl-content">
            <h4>{entry.card.title}</h4>
            <p>{entry.card.summary}</p>
            <div className="tl-meta">
              <span className="src">{entry.card.sourceRoom}</span>
              {entry.card.meetingProvider ? (
                <>
                  <span className="dot" />
                  <span>{entry.card.meetingProvider}</span>
                </>
              ) : null}
            </div>
          </div>
          <div className="tl-right">
            {names.length > 0 ? (
              <div className="tl-parts">
                {names.slice(0, 3).map((name, index) => (
                  <div key={`${name}-${index}`} className={`pface ${avatarTint(name)}`}>
                    {avatarInitials(name, "·")}
                  </div>
                ))}
              </div>
            ) : null}
            <span className="tl-dur">{durationLabel}</span>
          </div>
        </div>
      </div>
    </div>
  );
}

function pluralMeetings(n: number): string {
  const mod10 = n % 10;
  const mod100 = n % 100;
  if (mod10 === 1 && mod100 !== 11) return "встреча";
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20)) return "встречи";
  return "встреч";
}
