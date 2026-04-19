import { avatarInitials, avatarTint } from "../lib/avatar";

type AvatarProps = {
  name: string | null | undefined;
  size?: "md" | "sm";
  tintOverride?: "g1" | "g2" | "g3" | "g4" | "g5";
};

export function Avatar({ name, size = "md", tintOverride }: AvatarProps) {
  const initials = avatarInitials(name, name ? "?" : "—");
  const className = name ? "avatar" : "avatar is-ghost";
  const baseFont = size === "sm" ? 11 : 13;
  return (
    <div
      className={className}
      aria-label={name ?? "Гость"}
      style={{ fontSize: baseFont }}
      data-tint={tintOverride ?? (name ? avatarTint(name) : undefined)}
    >
      {initials}
    </div>
  );
}
