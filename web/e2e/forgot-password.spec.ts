import { expect, test } from "@playwright/test";
import { API_URL } from "../playwright.config";
import { ADMIN, apiGet, apiLogin, onboardStudent, registerStudent, studentSignIn } from "./helpers";

test("forgot password: the emailed link sets a new password once", async ({ page, request }) => {
  const s = await registerStudent(request, "Forgetful Learner");
  await onboardStudent(request, s.token);

  // From the sign-in page.
  await page.goto("/login");
  await page.getByRole("tab", { name: "Email" }).click();
  await page.getByRole("link", { name: "Forgot password?" }).click();
  await expect(page.getByRole("heading", { name: "Reset your password" })).toBeVisible();
  // Visited pages stay in the document (hidden), so pick the visible field.
  await page.getByLabel("Email").filter({ visible: true }).fill(s.email);
  await page.getByRole("button", { name: "Send reset link" }).click();
  await expect(page.getByRole("status").filter({ visible: true })).toContainText(`If an account exists for ${s.email}, we've sent a link`);

  // This test server doesn't send email; staff can read it in the test outbox.
  const admin = await apiLogin(request, ADMIN.email, ADMIN.password);
  const emails: { to: string; text: string }[] = await apiGet(request, "/api/v1/admin/test-emails", admin);
  const email = emails.find((e) => e.to === s.email)!;
  const token = /reset-password\?token=([A-Za-z0-9_-]+)/.exec(email.text)![1];

  await page.goto(`/reset-password?token=${token}`);
  await page.getByLabel("New password", { exact: true }).fill("short");
  await page.getByRole("button", { name: "Save" }).click();
  await expect(page.getByText("Use at least 8 characters")).toBeVisible();
  await page.getByLabel("New password", { exact: true }).fill("Brand-new-Passw0rd");
  await page.getByLabel("Confirm new password").fill("Brand-new-Passw0rd");
  await page.getByRole("button", { name: "Save" }).click();
  await expect(page.getByText("Your password has been changed. Sign in with your new password.")).toBeVisible();

  await page.getByRole("link", { name: "Sign in", exact: true }).click();
  await studentSignIn(page, s.email, "Brand-new-Passw0rd");
  await page.waitForURL("**/home");
  const old = await request.post(`${API_URL}/api/v1/auth/login`, { data: { email: s.email, password: s.password, deviceSlot: "phone", deviceId: "check" } });
  expect(old.status()).toBe(401);

  // The link works only once.
  await page.goto(`/reset-password?token=${token}`);
  await page.getByLabel("New password", { exact: true }).fill("Another-Passw0rd");
  await page.getByLabel("Confirm new password").fill("Another-Passw0rd");
  await page.getByRole("button", { name: "Save" }).click();
  await expect(page.getByText("This reset link has expired or was already used. Ask for a new one.")).toBeVisible();
});

test("the admin sign-in page links to forgot password and back", async ({ page }) => {
  await page.goto("/admin/login");
  await page.getByRole("link", { name: "Forgot password?" }).click();
  await expect(page).toHaveURL(/\/forgot-password\?admin=1$/);
  await page.getByRole("link", { name: "Back to sign in" }).click();
  await expect(page).toHaveURL(/\/admin\/login/);
});
