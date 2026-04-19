export type AvatarTint = "g1" | "g2" | "g3" | "g4" | "g5";

const TINTS: AvatarTint[] = ["g1", "g2", "g3", "g4", "g5"];

export function avatarInitials(name: string | null | undefined, fallback = "?"): string {
  if (!name) {
    return fallback;
  }

  const cleaned = name
    .replace(/[#@]/g, "")
    .replace(/[_\-.]+/g, " ")
    .trim();

  if (cleaned.length === 0) {
    return fallback;
  }

  const parts = cleaned.split(/\s+/).filter(Boolean);
  if (parts.length === 0) {
    return fallback;
  }

  if (parts.length === 1) {
    const word = parts[0];
    return word.slice(0, 2).toUpperCase();
  }

  const first = parts[0].charAt(0);
  const second = parts[1].charAt(0);
  return (first + second).toUpperCase();
}

export function avatarTint(name: string | null | undefined): AvatarTint {
  if (!name) {
    return "g3";
  }

  let hash = 0;
  for (let i = 0; i < name.length; i += 1) {
    hash = (hash * 31 + name.charCodeAt(i)) | 0;
  }

  const index = Math.abs(hash) % TINTS.length;
  return TINTS[index];
}
