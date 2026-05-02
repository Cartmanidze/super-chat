import { useFonts } from "expo-font";
import { SafeAreaProvider } from "react-native-safe-area-context";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { StatusBar } from "expo-status-bar";
import { fontMap } from "./src/theme/fonts";
import { RootNavigator } from "./src/navigation/Root";
// Импорт ради побочного эффекта: внутри файла идёт `i18n.init(...)`,
// поэтому язык должен быть готов раньше, чем отрисуется первый экран.
import "./src/i18n";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      staleTime: 30_000,
      refetchOnWindowFocus: false,
    },
  },
});

export default function App() {
  const [fontsLoaded] = useFonts(fontMap);
  if (!fontsLoaded) return null;

  return (
    <SafeAreaProvider>
      <QueryClientProvider client={queryClient}>
        <RootNavigator />
        <StatusBar style="light" />
      </QueryClientProvider>
    </SafeAreaProvider>
  );
}
