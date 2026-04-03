import { env } from "../config/env";
import { HttpApi } from "./http-api";

export const api = new HttpApi(env.apiBaseUrl);
