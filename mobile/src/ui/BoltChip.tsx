import { View } from "react-native";
import { LinearGradient } from "expo-linear-gradient";
import { BoltIcon } from "./BoltIcon";
import { shadows } from "../theme/tokens";

type BoltChipProps = {
  size?: number;
  radius?: number;
};

export function BoltChip({ size = 44, radius = 14 }: BoltChipProps) {
  return (
    <View
      style={[
        {
          width: size,
          height: size,
          borderRadius: radius,
          alignItems: "center",
          justifyContent: "center",
          overflow: "hidden",
        },
        shadows.redGlow,
      ]}
    >
      <LinearGradient
        colors={["#e5383b", "#6a0b12"]}
        start={{ x: 0, y: 0 }}
        end={{ x: 1, y: 1 }}
        style={{ position: "absolute", inset: 0 }}
      />
      <BoltIcon size={Math.round(size * 0.55)} variant="gold" />
    </View>
  );
}
