import type { ReactNode } from "react";
import { Pressable, Text, View, type ViewStyle } from "react-native";
import { LinearGradient } from "expo-linear-gradient";
import { colors, radii, shadows, typography } from "../theme/tokens";

export type ButtonVariant = "primary" | "ghost" | "gold" | "danger" | "link";

type ButtonProps = {
  children: ReactNode;
  variant?: ButtonVariant;
  disabled?: boolean;
  full?: boolean;
  style?: ViewStyle;
  onPress?: () => void;
};

export function Button({ children, variant = "primary", disabled, full, style, onPress }: ButtonProps) {
  const isGradient = variant === "primary" || variant === "gold";
  const baseStyle: ViewStyle = {
    minHeight: 48,
    paddingHorizontal: 18,
    borderRadius: radii.pill,
    borderWidth: 1,
    borderColor: borderFor(variant),
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    overflow: "hidden",
    width: full ? "100%" : undefined,
    opacity: disabled ? 0.5 : 1,
    backgroundColor: isGradient ? "transparent" : backgroundFor(variant),
  };

  return (
    <Pressable disabled={disabled} onPress={onPress} style={[baseStyle, style, isGradient ? shadowFor(variant) : null]}>
      {variant === "primary" ? (
        <LinearGradient
          colors={["#e5383b", "#c1121f"]}
          start={{ x: 0, y: 0 }}
          end={{ x: 1, y: 1 }}
          style={{ position: "absolute", inset: 0 }}
        />
      ) : null}
      {variant === "gold" ? (
        <LinearGradient
          colors={["#f3c96b", "#a67c1e"]}
          start={{ x: 0, y: 0 }}
          end={{ x: 1, y: 1 }}
          style={{ position: "absolute", inset: 0 }}
        />
      ) : null}
      <View style={{ flexDirection: "row", alignItems: "center", gap: 8 }}>
        {typeof children === "string" ? (
          <Text style={{ ...typography.bodySemi, fontSize: 14, color: textFor(variant) }}>{children}</Text>
        ) : (
          children
        )}
      </View>
    </Pressable>
  );
}

function borderFor(v: ButtonVariant): string {
  if (v === "primary") return "rgba(229,56,59,0.6)";
  if (v === "gold") return "rgba(230,181,74,0.6)";
  if (v === "danger") return "rgba(229,56,59,0.4)";
  if (v === "link") return "transparent";
  return colors.borderLine;
}

function backgroundFor(v: ButtonVariant): string {
  if (v === "ghost") return "rgba(255,255,255,0.03)";
  if (v === "danger") return "transparent";
  if (v === "link") return "transparent";
  return "transparent";
}

function textFor(v: ButtonVariant): string {
  if (v === "primary") return "#fff";
  if (v === "gold") return "#1a1207";
  if (v === "danger") return colors.bolt400;
  if (v === "link") return colors.bolt400;
  return colors.bone;
}

function shadowFor(v: ButtonVariant) {
  if (v === "primary") return shadows.redGlow;
  if (v === "gold") return shadows.goldGlow;
  return null;
}
