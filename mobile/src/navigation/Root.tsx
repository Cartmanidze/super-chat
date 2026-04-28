import { useEffect, useState } from "react";
import { createNativeStackNavigator } from "@react-navigation/native-stack";
import { createBottomTabNavigator } from "@react-navigation/bottom-tabs";
import { NavigationContainer } from "@react-navigation/native";
import { useSessionStore } from "../store/session";
import { OnboardingScreen } from "../screens/OnboardingScreen";
import { AuthScreen } from "../screens/AuthScreen";
import { TodayScreen } from "../screens/TodayScreen";
import { ProfileScreen } from "../screens/ProfileScreen";
import { ConnectStack } from "./ConnectStack";
import { TabBar } from "./TabBar";

const Stack = createNativeStackNavigator();
const Tabs = createBottomTabNavigator();

const navTheme = {
  dark: true,
  colors: {
    primary: "#e5383b",
    background: "#060606",
    card: "#060606",
    text: "#f5f1eb",
    border: "rgba(255,255,255,0.06)",
    notification: "#e5383b",
  },
  fonts: {
    regular: { fontFamily: "Inter_400Regular", fontWeight: "400" as const },
    medium: { fontFamily: "Inter_500Medium", fontWeight: "500" as const },
    bold: { fontFamily: "Manrope_700Bold", fontWeight: "700" as const },
    heavy: { fontFamily: "Manrope_800ExtraBold", fontWeight: "800" as const },
  },
};

function MainTabs() {
  return (
    <Tabs.Navigator
      tabBar={(props) => <TabBar {...props} />}
      screenOptions={{ headerShown: false }}
      initialRouteName="Today"
    >
      <Tabs.Screen name="Today" component={TodayScreen} />
      <Tabs.Screen name="Connect" component={ConnectStack} />
      <Tabs.Screen name="Profile" component={ProfileScreen} />
    </Tabs.Navigator>
  );
}

export function RootNavigator() {
  const accessToken = useSessionStore((s) => s.accessToken);
  const isHydrated = useSessionStore((s) => s.isHydrated);
  const hydrate = useSessionStore((s) => s.hydrate);
  const [seenIntro, setSeenIntro] = useState(false);

  useEffect(() => {
    void hydrate();
  }, [hydrate]);

  if (!isHydrated) return null;
  return (
    <NavigationContainer theme={navTheme}>
      <Stack.Navigator screenOptions={{ headerShown: false, contentStyle: { backgroundColor: "#060606" } }}>
        {accessToken ? (
          <Stack.Screen name="Main" component={MainTabs} />
        ) : seenIntro ? (
          <Stack.Screen name="Auth" component={AuthScreen} />
        ) : (
          <Stack.Screen name="Onboarding">{() => <OnboardingScreen onFinish={() => setSeenIntro(true)} />}</Stack.Screen>
        )}
      </Stack.Navigator>
    </NavigationContainer>
  );
}
