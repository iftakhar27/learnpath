import { devices, expect, test } from "@playwright/test";
import { API_URL } from "../playwright.config";
import { onboardStudent, registerStudent, studentSignIn } from "./helpers";

test("a second computer is asked first; continuing signs the first one out with an explanation", async ({ browser, request }) => {
  const s = await registerStudent(request, "Shared Account");
  await onboardStudent(request, s.token);

  // Computer A signs in.
  const a = await (await browser.newContext()).newPage();
  await studentSignIn(a, s.email, s.password);
  await a.waitForURL("**/home");

  // Computer B (its own browser, so another device) is asked first.
  const b = await (await browser.newContext()).newPage();
  await studentSignIn(b, s.email, s.password);
  const dialog = b.getByRole("dialog", { name: "Already signed in on another device" });
  await expect(dialog).toContainText(/This account is signed in on .+ \(last active (just now|\d+ minutes? ago)\)/);

  // Cancel keeps A signed in.
  await dialog.getByRole("button", { name: "Cancel" }).last().click();
  await expect(b).toHaveURL(/\/login/);
  await a.reload();
  await expect(a.getByRole("heading", { name: "Hi, Shared" })).toBeVisible();

  // Continue here: B is in, A is out at once, and told why.
  await b.getByRole("button", { name: "Sign in", exact: true }).click();
  await b.getByRole("dialog").getByRole("button", { name: "Continue here" }).click();
  await b.waitForURL("**/home");
  // Whatever A does next (here: opening Learn), it is signed out and told why.
  await a.goto("/learn");
  await a.waitForURL(/\/login\?.*replaced=1/);
  await expect(a.getByText("You were signed out because your account was signed in on another device.")).toBeVisible();
});

test("the same student can use a phone and a computer at the same time", async ({ browser, request }) => {
  const s = await registerStudent(request, "Two Devices");
  await onboardStudent(request, s.token);
  // Free the phone slot used to create the account, so the emulated phone below is the student's phone.
  await request.post(`${API_URL}/api/v1/auth/logout`, { data: { refreshToken: (await (await request.post(`${API_URL}/api/v1/auth/login`, { data: { email: s.email, password: s.password, deviceSlot: "phone", deviceId: `api-${s.email}` } })).json()).refreshToken } });

  const laptop = await (await browser.newContext()).newPage();
  await studentSignIn(laptop, s.email, s.password);
  await laptop.waitForURL("**/home");

  const phone = await (await browser.newContext({ ...devices["iPhone 13"] })).newPage();
  await studentSignIn(phone, s.email, s.password);
  await phone.waitForURL("**/home");
  await expect(phone.getByRole("dialog")).toHaveCount(0);

  await laptop.reload();
  await expect(laptop.getByRole("heading", { name: "Hi, Two" })).toBeVisible();
});
