import type { ReactNode } from "react";
import { Pressable, Text, View } from "react-native";
import Svg, { Polyline } from "react-native-svg";
import { BoltIcon } from "./BoltIcon";
import { Eyebrow } from "./Eyebrow";
import { colors, typography } from "../theme/tokens";

type HeaderProps = {
  subtitle?: string;
  title?: ReactNode;
  right?: ReactNode;
  onBack?: () => void;
  compact?: boolean;
};

export function Header({ subtitle, title, right, onBack, compact }: HeaderProps) {
  return (
    <View
      style={{
        paddingHorizontal: 20,
        paddingTop: compact ? 8 : 12,
        paddingBottom: 16,
        gap: 8,
      }}
    >
      <View style={{ flexDirection: "row", justifyContent: "space-between", alignItems: "center" }}>
        <View style={{ flexDirection: "row", alignItems: "center", gap: 8 }}>
          {onBack ? (
            <Pressable
              onPress={onBack}
              style={{
                width: 36,
                height: 36,
                borderRadius: 18,
                borderWidth: 1,
                borderColor: colors.borderLine,
                backgroundColor: colors.surfaceMid,
                alignItems: "center",
                justifyContent: "center",
                marginLeft: -6,
              }}
            >
              <Svg width={14} height={14} viewBox="0 0 24 24" fill="none">
                <Polyline
                  points="15 18 9 12 15 6"
                  stroke={colors.bone}
                  strokeWidth={2.2}
                  strokeLinecap="round"
                  strokeLinejoin="round"
                />
              </Svg>
            </Pressable>
          ) : (
            <BoltIcon size={14} variant="gold" />
          )}
          {subtitle ? <Eyebrow>{subtitle}</Eyebrow> : null}
        </View>
        {right}
      </View>
      {title ? (
        typeof title === "string" ? (
          <Text
            style={{
              ...typography.display,
              fontFamily: "Manrope_800ExtraBold",
              fontSize: 24,
              lineHeight: 27,
              color: colors.bone,
              letterSpacing: -0.6,
            }}
          >
            {title}
          </Text>
        ) : (
          title
        )
      ) : null}
    </View>
  );
}
