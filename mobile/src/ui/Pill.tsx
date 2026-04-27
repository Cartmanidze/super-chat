import type { ReactNode } from "react";
import { Text, View, type ViewStyle } from "react-native";
import { colors, radii, typography } from "../theme/tokens";

export type PillKind =
  | "kind"
  | "neutral"
  | "status"
  | "gold"
  | "past"
  | "confirmed"
  | "pending"
  | "moved"
  | "cancel"
  | "danger";

type PillProps = {
  children: ReactNode;
  kind?: PillKind;
  style?: ViewStyle;
};

const styles: Record<
  PillKind,
  { background: string; color: string; border: string; lineThrough?: boolean }
> = {
  kind: { background: "rgba(229,56,59,0.18)", color: colors.bone, border: "rgba(229,56,59,0.35)" },
  neutral: { background: "transparent", color: colors.ash400, border: "rgba(255,255,255,0.08)" },
  status: { background: "rgba(229,56,59,0.12)", color: colors.bolt400, border: "rgba(229,56,59,0.45)" },
  gold: { background: "rgba(230,181,74,0.10)", color: colors.gold400, border: "rgba(230,181,74,0.35)" },
  past: { background: "rgba(255,255,255,0.02)", color: colors.ash500, border: "rgba(255,255,255,0.05)" },
  confirmed: { background: "rgba(48,192,130,0.10)", color: colors.success, border: "rgba(48,192,130,0.35)" },
  pending: { background: "rgba(230,181,74,0.08)", color: colors.gold400, border: "rgba(230,181,74,0.35)" },
  moved: { background: "rgba(122,162,247,0.10)", color: colors.info, border: "rgba(122,162,247,0.35)" },
  cancel: {
    background: "rgba(255,255,255,0.02)",
    color: colors.ash500,
    border: "rgba(255,255,255,0.05)",
    lineThrough: true,
  },
  danger: { background: "rgba(229,56,59,0.12)", color: colors.bolt400, border: "rgba(229,56,59,0.45)" },
};

export function Pill({ children, kind = "kind", style }: PillProps) {
  const variant = styles[kind];
  return (
    <View
      style={[
        {
          flexDirection: "row",
          alignItems: "center",
          height: 22,
          paddingHorizontal: 10,
          borderRadius: radii.pill,
          borderWidth: 1,
          borderColor: variant.border,
          backgroundColor: variant.background,
          alignSelf: "flex-start",
        },
        style,
      ]}
    >
      {typeof children === "string" ? (
        <Text
          style={{
            ...typography.mono,
            fontSize: 10,
            color: variant.color,
            letterSpacing: 1,
            textTransform: "uppercase",
            textDecorationLine: variant.lineThrough ? "line-through" : "none",
          }}
        >
          {children}
        </Text>
      ) : (
        children
      )}
    </View>
  );
}
