import { create } from "zustand";
import { persist } from "zustand/middleware";

type SessionState = {
  accessToken: string | null;
  email: string | null;
  setSession: (accessToken: string, email: string) => void;
  clearSession: () => void;
};

export const useSessionStore = create<SessionState>()(
  persist(
    (set) => ({
      accessToken: null,
      email: null,
      setSession: (accessToken, email) => set({ accessToken, email }),
      clearSession: () => set({ accessToken: null, email: null }),
    }),
    {
      name: "superchat-session",
    },
  ),
);
