import assert from "node:assert/strict";
import test from "node:test";
import {
  getTelegramLoginFormState,
  getTelegramLoginStepMeta,
} from "./telegram-login-form-state.ts";

test("returns progress metadata and helper copy for phone, code and password steps", () => {
  assert.deepEqual(getTelegramLoginStepMeta("phone"), {
    title: "Номер телефона",
    description: "Шаг 1 из 3",
    hint: "Укажите номер в международном формате, например +79991234567.",
    submitLabel: "Продолжить",
    fieldLabel: "Телефон",
    placeholder: "+79991234567",
    inputType: "tel",
    index: 0,
    accent: "Отправим номер в Telegram bridge и перейдем к следующему шагу.",
  });

  assert.equal(getTelegramLoginStepMeta("code").index, 1);
  assert.equal(getTelegramLoginStepMeta("password").index, 2);
});

test("locks fields and hides submit action after successful send while server still stays on the same step", () => {
  assert.deepEqual(
    getTelegramLoginFormState({
      serverStep: "code",
      submittedStep: "code",
      isSubmitting: false,
    }),
    {
      isLocked: true,
      showSubmitButton: false,
      showWaitingNote: true,
    },
  );
});

test("unlocks the form when server moves to the next step", () => {
  assert.deepEqual(
    getTelegramLoginFormState({
      serverStep: "password",
      submittedStep: "code",
      isSubmitting: false,
    }),
    {
      isLocked: false,
      showSubmitButton: true,
      showWaitingNote: false,
    },
  );
});
