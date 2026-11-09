import { expect, test } from "@playwright/test";
import { API_URL } from "../playwright.config";
import { expectToast, onboardStudent, registerStudent, signIn, studentSignIn } from "./helpers";

test("an admin resets a forgotten password, and the student signs in with it and changes it", async ({ page, browser, request }) => {
  const s = await registerStudent(request, "Forgetful Student");
  await onboardStudent(request, s.token);

  // Staff: Students → the student → Reset password → a temporary password shown once.
  const staff = await browser.newContext();
  const admin = await staff.newPage();
  await signIn(admin);
  await admin.goto(`/admin/students/${s.userId}`);
  await admin.getByRole("button", { name: "Reset password" }).click();
  await admin.getByRole("dialog", { name: "Reset password?" }).getByRole("button", { name: "Reset password" }).click();
  const shown = admin.getByRole("dialog", { name: "Temporary password" });
  await expect(shown).toContainText(s.email);
  const temporary = (await shown.getByLabel("Temporary password").innerText()).trim();
  expect(temporary).toMatch(/^[A-Za-z2-9]{4}-[A-Za-z2-9]{4}-[A-Za-z2-9]{4}$/);
  await staff.close();

  // The old password no longer works; the temporary one does.
  const old = await request.post(`${API_URL}/api/v1/auth/login`, { data: { email: s.email, password: s.password, deviceSlot: "phone", deviceId: "check" } });
  expect(old.status()).toBe(401);
  await studentSignIn(page, s.email, temporary);
  await page.waitForURL("**/home");

  // Profile → Change password.
  await page.goto("/profile");
  await page.getByRole("button", { name: "Change password" }).click();
  const dialog = page.getByRole("dialog", { name: "Change password" });
  await dialog.getByLabel("Current password").fill(temporary);
  await dialog.getByLabel("New password", { exact: true }).fill("short");
  await dialog.getByRole("button", { name: "Save" }).click();
  await expect(dialog.getByText("Use at least 8 characters")).toBeVisible();
  await dialog.getByLabel("New password", { exact: true }).fill("My-own-Passw0rd");
  await dialog.getByLabel("Confirm new password").fill("My-own-Passw0rd");
  await dialog.getByRole("button", { name: "Save" }).click();
  await expectToast(page, "Password changed");
  // This browser stays signed in; the new password works.
  await page.goto("/home");
  await expect(page).toHaveURL(/\/home$/);
  const fresh = await request.post(`${API_URL}/api/v1/auth/login`, { data: { email: s.email, password: "My-own-Passw0rd", deviceSlot: "phone", deviceId: "check" } });
  expect(fresh.ok()).toBeTruthy();
});
