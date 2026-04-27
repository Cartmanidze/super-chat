import { View, type ViewStyle } from "react-native";
import { LinearGradient } from "expo-linear-gradient";

type DotsProps = {
  count: number;
  active: number;
  style?: ViewStyle;
};

export function Dots({ count, active, style }: DotsProps) {
  return (
    <View style={[{ flexDirection: "row", gap: 6 }, style]}>
      {Array.from({ length: count }).map((_, i) => {
        const on = i === active;
        return (
          <View
            key={i}
            style={{
              height: 4,
              borderRadius: 999,
              width: on ? 22 : 4,
              backgroundColor: on ? "transparent" : "rgba(255,255,255,0.12)",
              overflow: "hidden",
            }}
          >
            {on ? (
              <LinearGradient
                colors={["#e5383b", "#ff8b8d"]}
                start={{ x: 0, y: 0 }}
                end={{ x: 1, y: 0 }}
                style={{ position: "absolute", inset: 0 }}
              />
            ) : null}
          </View>
        );
      })}
    </View>
  );
}
