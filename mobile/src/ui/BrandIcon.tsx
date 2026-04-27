import { View } from "react-native";
import { LinearGradient } from "expo-linear-gradient";
import Svg, { Path, Rect, Circle } from "react-native-svg";

export type BrandKind = "tg" | "wa" | "gm" | "ol" | "sl" | "dc" | "muted";

type BrandIconProps = {
  kind: BrandKind;
  size?: number;
};

const palettes: Record<BrandKind, { from: string; to: string } | { muted: true }> = {
  tg: { from: "#2aabee", to: "#0088cc" },
  wa: { from: "#25d366", to: "#128c7e" },
  gm: { from: "#ea4335", to: "#c5221f" },
  ol: { from: "#0078d4", to: "#004578" },
  sl: { from: "#4a154b", to: "#2d0d2f" },
  dc: { from: "#5865f2", to: "#404eed" },
  muted: { muted: true },
};

export function BrandIcon({ kind, size = 44 }: BrandIconProps) {
  const radius = size * 0.3;
  const iconSize = size * 0.45;
  const palette = palettes[kind];

  return (
    <View
      style={{
        width: size,
        height: size,
        borderRadius: radius,
        alignItems: "center",
        justifyContent: "center",
        overflow: "hidden",
        borderWidth: "muted" in palette ? 1 : 0,
        borderColor: "rgba(255,255,255,0.12)",
        backgroundColor: "muted" in palette ? "rgba(255,255,255,0.03)" : "transparent",
      }}
    >
      {!("muted" in palette) ? (
        <LinearGradient
          colors={[palette.from, palette.to]}
          start={{ x: 0, y: 0 }}
          end={{ x: 1, y: 1 }}
          style={{ position: "absolute", inset: 0 }}
        />
      ) : null}
      <BrandSvg kind={kind} size={iconSize} />
    </View>
  );
}

function BrandSvg({ kind, size }: { kind: BrandKind; size: number }) {
  switch (kind) {
    case "tg":
      return (
        <Svg width={size} height={size} viewBox="0 0 24 24">
          <Path
            fill="#fff"
            d="M9.78 15.56 9.6 20a.76.76 0 0 0 1.22.5l2.6-2.17 3.4 2.5c.63.34 1.08.17 1.23-.58l2.23-10.45c.22-1.02-.37-1.42-.98-1.2L4.62 13.27c-.99.4-.98.96-.18 1.2l3.93 1.22 9.13-5.76c.43-.26.82-.12.5.17L9.78 15.56Z"
          />
        </Svg>
      );
    case "wa":
      return (
        <Svg width={size} height={size} viewBox="0 0 24 24">
          <Path
            fill="#fff"
            d="M17.5 14.4c-.3-.15-1.7-.85-2-.95-.25-.1-.45-.15-.65.15-.2.3-.75.95-.9 1.15-.2.2-.35.2-.65.05-.3-.15-1.25-.45-2.4-1.5-.9-.8-1.5-1.8-1.65-2.1-.15-.3 0-.45.15-.6.15-.15.3-.35.45-.55.15-.2.2-.3.3-.55.1-.2.05-.4 0-.55-.1-.15-.65-1.55-.9-2.15-.25-.55-.5-.5-.65-.5h-.55c-.2 0-.5.05-.75.35-.25.3-1 1-1 2.4 0 1.45 1 2.85 1.15 3.05.15.2 2 3.05 4.85 4.3.7.3 1.25.5 1.65.6.7.25 1.35.2 1.85.1.55-.05 1.7-.7 1.95-1.35.25-.65.25-1.2.15-1.35-.05-.1-.25-.15-.55-.3ZM12 20.25a8.25 8.25 0 0 1-4.4-1.25l-.3-.2-3.25.85.85-3.15-.2-.3A8.25 8.25 0 1 1 12 20.25Z"
          />
        </Svg>
      );
    case "gm":
      return (
        <Svg width={size} height={size} viewBox="0 0 24 24">
          <Path
            fill="#fff"
            d="M20 4H4a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V6a2 2 0 0 0-2-2Zm-.4 2L12 11 4.4 6h15.2ZM4 18V7.25l7.4 4.85a1 1 0 0 0 1.2 0L20 7.25V18H4Z"
          />
        </Svg>
      );
    case "ol":
      return (
        <Svg width={size} height={size} viewBox="0 0 24 24">
          <Rect x="3" y="4" width="12" height="16" rx="1" fill="#fff" />
          <Rect x="14" y="9" width="7" height="7" rx="1" fill="rgba(255,255,255,0.5)" />
          <Circle cx="9" cy="12" r="3" fill="#0078d4" />
        </Svg>
      );
    case "sl":
      return (
        <Svg width={size} height={size} viewBox="0 0 24 24">
          <Path
            fill="#fff"
            d="M6 15a2 2 0 1 1-2-2h2v2Zm1 0a2 2 0 0 1 4 0v5a2 2 0 0 1-4 0v-5Zm2-9a2 2 0 1 1 2-2v2H9Zm0 1a2 2 0 1 1 0 4H4a2 2 0 1 1 0-4h5Zm9 2a2 2 0 1 1 2 2h-2V9Zm-1 0a2 2 0 0 1-4 0V4a2 2 0 1 1 4 0v5Zm-2 9a2 2 0 1 1-2 2v-2h2Zm0-1a2 2 0 1 1 0-4h5a2 2 0 1 1 0 4h-5Z"
          />
        </Svg>
      );
    case "dc":
      return (
        <Svg width={size} height={size} viewBox="0 0 24 24">
          <Path
            fill="#fff"
            d="M19.3 5.3A16 16 0 0 0 15.1 4l-.2.4a14 14 0 0 0-5.8 0L8.9 4a16 16 0 0 0-4.2 1.3 18 18 0 0 0-3 12 14 14 0 0 0 4.3 2.2l.9-1.3a10 10 0 0 1-1.5-.7c.1-.1.3-.1.4-.2 3 1.4 6.3 1.4 9.3 0 .1.1.3.1.4.2l-1.5.7.9 1.3a14 14 0 0 0 4.3-2.2 18 18 0 0 0-3-12ZM9 15.5c-.8 0-1.5-.8-1.5-1.8s.7-1.8 1.5-1.8 1.5.8 1.5 1.8-.7 1.8-1.5 1.8Zm6 0c-.8 0-1.5-.8-1.5-1.8s.7-1.8 1.5-1.8 1.5.8 1.5 1.8-.7 1.8-1.5 1.8Z"
          />
        </Svg>
      );
    case "muted":
      return (
        <Svg width={size} height={size} viewBox="0 0 24 24" fill="none">
          <Path d="M12 5v14M5 12h14" stroke="#737373" strokeWidth={2} strokeLinecap="round" />
        </Svg>
      );
  }
}
