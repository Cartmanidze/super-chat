import { createNativeStackNavigator } from "@react-navigation/native-stack";
import { ConnectionsScreen } from "../screens/ConnectionsScreen";
import { TelegramLoginScreen } from "../screens/TelegramLoginScreen";

export type ConnectStackParamList = {
  ConnectionsList: undefined;
  TelegramLogin: undefined;
};

const Stack = createNativeStackNavigator<ConnectStackParamList>();

export function ConnectStack() {
  return (
    <Stack.Navigator screenOptions={{ headerShown: false, contentStyle: { backgroundColor: "#060606" } }}>
      <Stack.Screen name="ConnectionsList" component={ConnectionsScreen} />
      <Stack.Screen name="TelegramLogin">
        {(props) => <TelegramLoginScreen onClose={() => props.navigation.goBack()} />}
      </Stack.Screen>
    </Stack.Navigator>
  );
}
