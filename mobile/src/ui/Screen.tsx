import type { PropsWithChildren } from "react";
import { ScrollView, View, type ViewStyle } from "react-native";
import { LinearGradient } from "expo-linear-gradient";
import { SafeAreaView } from "react-native-safe-area-context";
import { colors } from "../theme/tokens";

type ScreenProps = PropsWithChildren<{
  scroll?: boolean;
  pad?: boolean;
  style?: ViewStyle;
}>;

export function Screen({ children, scroll = true, pad = true, style }: ScreenProps) {
  const Body = scroll ? ScrollView : View;
  return (
    <View style={{ flex: 1, backgroundColor: colors.ink950 }}>
      <LinearGradient
        colors={["rgba(229,56,59,0.26)", "transparent"]}
        start={{ x: 1, y: 0 }}
        end={{ x: 0.5, y: 0.4 }}
        style={{ position: "absolute", inset: 0 }}
      />
      <LinearGradient
        colors={["transparent", "rgba(193,18,31,0.14)"]}
        start={{ x: 0.5, y: 0.6 }}
        end={{ x: 0, y: 1 }}
        style={{ position: "absolute", inset: 0 }}
      />
      <SafeAreaView style={{ flex: 1 }} edges={["top", "left", "right"]}>
        <Body
          style={{ flex: 1 }}
          contentContainerStyle={[
            {
              paddingBottom: pad ? 120 : 0,
              flexGrow: 1,
            },
            style,
          ]}
          showsVerticalScrollIndicator={false}
        >
          {children}
        </Body>
      </SafeAreaView>
    </View>
  );
}
