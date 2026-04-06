import assert from "node:assert/strict";
import test from "node:test";
import { getTelegramConnectionRefetchInterval } from "./telegram-connection-polling.ts";

test("polls while telegram login still needs action", () => {
  assert.equal(
    getTelegramConnectionRefetchInterval({
      state: "Pending",
      requiresAction: true,
    }),
    3000,
  );
});

test("stops polling when telegram is already connected", () => {
  assert.equal(
    getTelegramConnectionRefetchInterval({
      state: "Connected",
      requiresAction: false,
    }),
    false,
  );
});
