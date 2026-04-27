import type { PropsWithChildren } from "react";
import { View, type ViewStyle } from "react-native";
import { LinearGradient } from "expo-linear-gradient";
import { colors, radii, shadows } from "../theme/tokens";

type CardProps = PropsWithChildren<{
  accent?: boolean;
  style?: ViewStyle;
}>;

export function Card({ children, accent, style }: CardProps) {
  return (
    <View
      style={[
        {
          padding: 18,
          borderRadius: radii.xxl,
          borderWidth: 1,
          borderColor: accent ? "rgba(229,56,59,0.4)" : colors.borderSoft,
          backgroundColor: accent ? "#0e0808" : colors.ink850,
          overflow: "hidden",
          position: "relative",
        },
        accent ? shadows.card : null,
        style,
      ]}
    >
      {accent ? (
        <>
          <LinearGradient
            colors={["#1b1012", "#0e0808"]}
            start={{ x: 0, y: 0 }}
            end={{ x: 0, y: 1 }}
            style={{ position: "absolute", inset: 0 }}
          />
          <LinearGradient
            colors={["rgba(229,56,59,0.22)", "transparent"]}
            start={{ x: 1, y: 0 }}
            end={{ x: 0, y: 1 }}
            style={{ position: "absolute", inset: 0 }}
          />
        </>
      ) : (
        <LinearGradient
          colors={["rgba(255,255,255,0.03)", "rgba(255,255,255,0.01)"]}
          start={{ x: 0, y: 0 }}
          end={{ x: 0, y: 1 }}
          style={{ position: "absolute", inset: 0 }}
        />
      )}
      <View>{children}</View>
    </View>
  );
}
