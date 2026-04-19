import { avatarInitials, avatarTint } from "../lib/avatar";

type ParticipantStackProps = {
  names: ReadonlyArray<string | null | undefined>;
  max?: number;
  size?: "sm" | "md";
};

export function ParticipantStack({ names, max = 3, size = "md" }: ParticipantStackProps) {
  const clean = names.filter((x): x is string => typeof x === "string" && x.trim().length > 0);
  const visible = clean.slice(0, max);
  const overflow = clean.length - visible.length;

  if (visible.length === 0) {
    return null;
  }

  return (
    <div className={size === "sm" ? "tl-parts" : "participants"}>
      {visible.map((name, index) => (
        <div key={`${name}-${index}`} className={`pface ${avatarTint(name)}`}>
          {avatarInitials(name, "·")}
        </div>
      ))}
      {overflow > 0 ? <div className="pface more">+{overflow}</div> : null}
    </div>
  );
}
