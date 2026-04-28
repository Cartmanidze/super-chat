import { Pressable, Text, View } from "react-native";
import { BlurView } from "expo-blur";
import { LinearGradient } from "expo-linear-gradient";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import type { BottomTabBarProps } from "@react-navigation/bottom-tabs";
import { colors, typography } from "../theme/tokens";

const ICONS: Record<string, string> = {
  Today: "⚡",
  Connect: "◎",
  Profile: "◉",
};

const LABELS: Record<string, string> = {
  Today: "Сегодня",
  Connect: "Источники",
  Profile: "Профиль",
};

export function TabBar({ state, navigation }: BottomTabBarProps) {
  const insets = useSafeAreaInsets();
  return (
    <View
      style={{
        position: "absolute",
        left: 14,
        right: 14,
        bottom: Math.max(insets.bottom, 14),
        borderRadius: 999,
        overflow: "hidden",
        borderWidth: 1,
        borderColor: colors.borderLine,
      }}
    >
      <BlurView intensity={50} tint="dark" style={{ position: "absolute", inset: 0 }} />
      <View style={{ position: "absolute", inset: 0, backgroundColor: "rgba(14,14,14,0.6)" }} />
      <View style={{ flexDirection: "row", padding: 5, gap: 4 }}>
        {state.routes.map((route, i) => {
          const active = i === state.index;
          return (
            <Pressable
              key={route.key}
              onPress={() => {
                const event = navigation.emit({ type: "tabPress", target: route.key, canPreventDefault: true });
                if (!event.defaultPrevented && !active) navigation.navigate(route.name as never);
              }}
              // Every tab has the SAME box-model: borderWidth 1 always present.
              // Inactive tabs use a transparent border so the inner content does
              // not shift by 1 px when the active border appears.
              style={{
                flex: 1,
                height: 40,
                borderRadius: 999,
                alignItems: "center",
                justifyContent: "center",
                flexDirection: "row",
                gap: 6,
                overflow: "hidden",
                borderWidth: 1,
                borderColor: active ? "rgba(229,56,59,0.5)" : "transparent",
              }}
            >
              {active ? (
                <LinearGradient
                  colors={["rgba(229,56,59,0.3)", "rgba(255,255,255,0.02)"]}
                  start={{ x: 0, y: 0 }}
                  end={{ x: 1, y: 1 }}
                  style={{ position: "absolute", inset: 0 }}
                />
              ) : null}
              <Text style={{ fontSize: 12, color: active ? colors.bolt400 : colors.ash500 }}>{ICONS[route.name]}</Text>
              <Text
                numberOfLines={1}
                style={{
                  ...typography.bodySemi,
                  fontSize: 12,
                  color: active ? colors.bone : colors.ash400,
                  letterSpacing: 0.2,
                }}
              >
                {LABELS[route.name]}
              </Text>
            </Pressable>
          );
        })}
      </View>
    </View>
  );
}
