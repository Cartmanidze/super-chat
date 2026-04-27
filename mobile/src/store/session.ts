import { create } from "zustand";
import * as SecureStore from "expo-secure-store";

const TOKEN_KEY = "superchat-access-token";
const EMAIL_KEY = "superchat-email";

type SessionState = {
  accessToken: string | null;
  email: string | null;
  isHydrated: boolean;
  hydrate: () => Promise<void>;
  setSession: (token: string, email: string) => Promise<void>;
  clearSession: () => Promise<void>;
};

export const useSessionStore = create<SessionState>((set) => ({
  accessToken: null,
  email: null,
  isHydrated: false,
  async hydrate() {
    try {
      const [token, email] = await Promise.all([
        SecureStore.getItemAsync(TOKEN_KEY),
        SecureStore.getItemAsync(EMAIL_KEY),
      ]);
      set({ accessToken: token, email, isHydrated: true });
    } catch {
      set({ isHydrated: true });
    }
  },
  async setSession(token, email) {
    await SecureStore.setItemAsync(TOKEN_KEY, token);
    await SecureStore.setItemAsync(EMAIL_KEY, email);
    set({ accessToken: token, email });
  },
  async clearSession() {
    await SecureStore.deleteItemAsync(TOKEN_KEY);
    await SecureStore.deleteItemAsync(EMAIL_KEY);
    set({ accessToken: null, email: null });
  },
}));
