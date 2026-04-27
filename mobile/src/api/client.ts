import axios from "axios";

const baseURL = process.env.EXPO_PUBLIC_API_BASE_URL ?? "https://api.super-chat.org/api/v1";

export const api = axios.create({
  baseURL,
  timeout: 15_000,
  headers: { "Content-Type": "application/json" },
});

export function withBearer(token: string) {
  return { Authorization: `Bearer ${token}` };
}
