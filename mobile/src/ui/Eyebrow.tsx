import { Text, type TextStyle } from "react-native";
import { colors, typography } from "../theme/tokens";

type EyebrowProps = {
  children: string;
  color?: string;
  style?: TextStyle;
};

export function Eyebrow({ children, color = colors.bolt400, style }: EyebrowProps) {
  return (
    <Text
      style={[
        {
          ...typography.mono,
          fontSize: 10,
          letterSpacing: 2.2,
          textTransform: "uppercase",
          color,
        },
        style,
      ]}
    >
      {children}
    </Text>
  );
}
