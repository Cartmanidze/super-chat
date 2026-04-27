import { Text, View, type ViewStyle } from "react-native";
import { LinearGradient } from "expo-linear-gradient";
import { typography } from "../theme/tokens";

export type AvatarVariant = "g1" | "g2" | "g3" | "g4";

type AvatarProps = {
  who: string;
  size?: number;
  variant?: AvatarVariant;
  style?: ViewStyle;
};

const palettes: Record<AvatarVariant, { from: string; to: string; color: string }> = {
  g1: { from: "#ff8b8d", to: "#c1121f", color: "#fff" },
  g2: { from: "#f3c96b", to: "#a67c1e", color: "#1a1207" },
  g3: { from: "#d8d3cb", to: "#737373", color: "#fff" },
  g4: { from: "#9db8ff", to: "#4c6be6", color: "#fff" },
};

export function Avatar({ who, size = 28, variant = "g1", style }: AvatarProps) {
  const palette = palettes[variant];
  return (
    <View
      style={[
        {
          width: size,
          height: size,
          borderRadius: size / 2,
          alignItems: "center",
          justifyContent: "center",
          overflow: "hidden",
          borderWidth: 2,
          borderColor: "#0d0d0d",
        },
        style,
      ]}
    >
      <LinearGradient
        colors={[palette.from, palette.to]}
        start={{ x: 0, y: 0 }}
        end={{ x: 1, y: 1 }}
        style={{ position: "absolute", inset: 0 }}
      />
      <Text
        style={{
          ...typography.display,
          fontFamily: "Manrope_800ExtraBold",
          fontSize: Math.round(size * 0.36),
          color: palette.color,
          letterSpacing: 0,
        }}
      >
        {who}
      </Text>
    </View>
  );
}
