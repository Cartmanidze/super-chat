import { useEffect, useState } from "react";
import { formatClockTime, formatDateLong } from "../lib/relative-time";

export function DateChip() {
  const [now, setNow] = useState(() => new Date());

  useEffect(() => {
    const timer = setInterval(() => setNow(new Date()), 30_000);
    return () => clearInterval(timer);
  }, []);

  return (
    <span className="date-chip">
      <b>{formatDateLong(now)}</b>
      <span>·</span>
      <span>{formatClockTime(now)}</span>
    </span>
  );
}
