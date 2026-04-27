import Svg, { Defs, LinearGradient, Path, Stop } from "react-native-svg";

type BoltIconProps = {
  size?: number;
  variant?: "gold" | "red";
};

const STOPS = {
  gold: [
    { offset: "0", color: "#ffe49a" },
    { offset: "0.5", color: "#f3c96b" },
    { offset: "1", color: "#a67c1e" },
  ],
  red: [
    { offset: "0", color: "#ff8b8d" },
    { offset: "0.5", color: "#e5383b" },
    { offset: "1", color: "#6a0b12" },
  ],
} as const;

export function BoltIcon({ size = 18, variant = "gold" }: BoltIconProps) {
  const id = `bolt-${variant}-${size}`;
  const stroke = variant === "gold" ? "rgba(255,220,140,0.35)" : "rgba(255,140,140,0.35)";
  return (
    <Svg width={size} height={size} viewBox="0 0 48 48" fill="none">
      <Defs>
        <LinearGradient id={id} x1="0" y1="0" x2="48" y2="48" gradientUnits="userSpaceOnUse">
          {STOPS[variant].map((s) => (
            <Stop key={s.offset} offset={s.offset} stopColor={s.color} />
          ))}
        </LinearGradient>
      </Defs>
      <Path
        d="M28 4 L14 26 L21 26 L19 44 L34 20 L27 20 L28 4 Z"
        fill={`url(#${id})`}
        stroke={stroke}
        strokeWidth={0.5}
      />
    </Svg>
  );
}
