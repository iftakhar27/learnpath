import { expect, test } from "@playwright/test";
import { ADMIN, registerStudent, signIn, submitLogin } from "./helpers";

test("signed-out visitors are sent to login and returned to the page they asked for", async ({ page }) => {
  await page.goto("/admin/questions");
  await expect(page).toHaveURL(/\/admin\/login\?next=%2Fadmin%2Fquestions/);
  await page.getByLabel("Email").fill(ADMIN.email);
  await page.getByLabel("Password").fill(ADMIN.password);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Question bank" })).toBeVisible();
});

test("a wrong password shows an error and creates no session", async ({ page }) => {
  await submitLogin(page, ADMIN.email, "not-the-password");
  await expect(page.getByRole("alert").filter({ hasText: "Incorrect email or password" })).toBeVisible();
  expect((await page.context().cookies()).some((c) => c.name === "lp_rt")).toBe(false);
});

test("student accounts cannot sign in to the admin portal", async ({ page, request }) => {
  const student = await registerStudent(request);
  await submitLogin(page, student.email, student.password);
  await expect(page.getByRole("alert").filter({ hasText: "does not have access to the admin portal" })).toBeVisible();
  expect((await page.context().cookies()).some((c) => c.name === "lp_rt")).toBe(false);
});

test("session tokens are httpOnly and never exposed to page scripts", async ({ page }) => {
  await signIn(page);
  await expect(page.getByRole("heading", { name: "Dashboard" })).toBeVisible();
  const cookies = await page.context().cookies();
  for (const name of ["lp_at", "lp_rt"]) expect(cookies.find((c) => c.name === name)?.httpOnly).toBe(true);
  expect(await page.evaluate(() => document.cookie)).not.toContain("lp_");
});

test("an expired access token is refreshed transparently", async ({ page }) => {
  await signIn(page);
  await expect(page.getByRole("heading", { name: "Dashboard" })).toBeVisible();
  const before = (await page.context().cookies()).find((c) => c.name === "lp_rt")?.value;

  // Simulate the 15-minute access token expiring; several requests then race to refresh at once.
  await page.context().clearCookies({ name: "lp_at" });
  await page.reload();
  await expect(page.getByText("Registered students")).toBeVisible();
  await page.getByRole("link", { name: "Question bank" }).click();
  await expect(page.locator("tbody tr").first()).toBeVisible();

  const after = await page.context().cookies();
  expect(after.find((c) => c.name === "lp_at")).toBeTruthy();
  expect(after.find((c) => c.name === "lp_rt")?.value).not.toBe(before);
});

test("signing out ends the session", async ({ page }) => {
  await signIn(page);
  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page).toHaveURL(/\/admin\/login/);
  await page.goto("/admin");
  await expect(page).toHaveURL(/\/admin\/login/);
});
