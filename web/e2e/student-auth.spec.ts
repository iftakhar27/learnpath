import { expect, test } from "@playwright/test";
import { API_URL } from "../playwright.config";
import { ADMIN, apiGet, apiLogin, onboardStudent, registerStudent, studentSignIn, unique } from "./helpers";

test("a new student registers, sets up their preparation and lands on home", async ({ page, request }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Prepare smarter for NEET, JEE and Boards" })).toBeVisible();
  await page.getByRole("link", { name: "Get started" }).click();

  // Validation in the student's language before anything is sent.
  await page.getByRole("button", { name: "Create account" }).click();
  await expect(page.getByText("This field is required").first()).toBeVisible();
  await expect(page.getByText("Please accept the terms to continue")).toBeVisible();

  const email = `${unique("web")}@e2e.test`;
  await page.getByLabel("Full name").fill("Meera Iyer");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password").fill("short");
  await page.getByRole("button", { name: "Create account" }).click();
  await expect(page.getByText("Use at least 8 characters")).toBeVisible();
  await page.getByLabel("Password").fill("Passw0rd!x");
  await page.getByLabel("I agree to the Terms of Service and Privacy Policy").check();
  await page.getByRole("button", { name: "Create account" }).click();

  await page.waitForURL("**/onboarding");
  await page.getByRole("button", { name: "Next" }).click();
  await expect(page.getByText("Choose an exam to continue")).toBeVisible();
  await page.getByRole("radio", { name: "NEET (UG)" }).click();
  await page.getByRole("button", { name: "Next" }).click();
  await page.getByRole("radio", { name: "Class 12" }).click();
  await page.getByRole("radio", { name: "CBSE" }).click();
  await page.getByRole("button", { name: "Next" }).click();
  await page.getByRole("button", { name: "Get started" }).click();

  await page.waitForURL("**/home");
  await expect(page.getByRole("heading", { name: "Hi, Meera" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Today's plan" })).toBeVisible();
  await expect(page.getByText("Start your streak today")).toBeVisible();
});

test("Hindi chosen before sign-up carries through onboarding into the profile", async ({ page, request }) => {
  await page.goto("/");
  await page.getByRole("button", { name: "हिंदी" }).click();
  await expect(page.getByRole("heading", { name: "NEET, JEE और बोर्ड की स्मार्ट तैयारी" })).toBeVisible();
  await page.getByRole("link", { name: "शुरू करें" }).click();

  const email = `${unique("hindi")}@e2e.test`;
  await page.getByLabel("पूरा नाम").fill("Arjun Verma");
  await page.getByLabel("ईमेल").fill(email);
  await page.getByLabel("पासवर्ड").fill("Passw0rd!x");
  await page.locator('input[type="checkbox"]').check();
  await page.getByRole("button", { name: "खाता बनाएँ" }).click();

  await page.waitForURL("**/onboarding");
  // Exam names come from the API in Hindi too.
  await page.getByRole("radio", { name: "जेईई मेन" }).click();
  await page.getByRole("button", { name: "आगे" }).click();
  await page.getByRole("button", { name: "आगे" }).click();
  await page.getByRole("button", { name: "शुरू करें" }).click();
  await page.waitForURL("**/home");
  await expect(page.getByRole("heading", { name: "आज की योजना" })).toBeVisible();

  const token = await apiLogin(request, email, "Passw0rd!x");
  expect((await apiGet(request, "/api/v1/student/profile", token)).language).toBe("hi");

  // Switching back to English saves it to the profile too (the app follows it).
  await page.getByRole("button", { name: "English" }).click();
  await expect(page.getByRole("heading", { name: "Today's plan" })).toBeVisible();
  await expect.poll(async () => (await apiGet(request, "/api/v1/student/profile", token)).language).toBe("en");
});

test("a student signs in with a mobile number and the code sent to it", async ({ page, request }) => {
  const phone = `8${String(Date.now()).slice(-9)}`;
  await page.goto("/login");
  await page.getByLabel("Mobile number").fill("12345");
  await page.getByRole("button", { name: "Send OTP" }).click();
  await expect(page.getByText("Enter a 10-digit mobile number")).toBeVisible();

  await page.getByLabel("Mobile number").fill(`+91 ${phone}`);
  await page.getByRole("button", { name: "Send OTP" }).click();
  await expect(page.getByText(`OTP sent to +91 ${phone.slice(0, 5)} ${phone.slice(5)}`)).toBeVisible();
  await expect(page.getByText(/Resend in \d+s/)).toBeVisible();

  // Test servers don't send SMS; staff read the code (what the admin dashboard shows).
  const admin = await apiLogin(request, ADMIN.email, ADMIN.password);
  const codes: { phone: string; code: string }[] = await apiGet(request, "/api/v1/admin/test-otp-codes", admin);
  const code = codes.find((c) => c.phone.endsWith(phone.slice(-4)))!.code;

  await page.getByLabel("Enter the 6-digit OTP").fill(code === "000000" ? "111111" : "000000");
  await page.getByLabel("I agree to the Terms of Service and Privacy Policy").check();
  await page.getByRole("button", { name: "Verify" }).click();
  await expect(page.getByRole("alert").filter({ hasText: "Incorrect OTP" })).toBeVisible();

  await page.getByLabel("Enter the 6-digit OTP").fill(code);
  await page.getByLabel("Your name (for new accounts)").fill("Kabir Singh");
  await page.getByRole("button", { name: "Verify" }).click();
  await page.waitForURL("**/onboarding");
  await expect(page.getByRole("heading", { name: "Set up your preparation" })).toBeVisible();
});

test("signed-out visitors are sent to login and returned; wrong passwords are explained", async ({ page, request }) => {
  const s = await registerStudent(request, "Riya Das");
  await onboardStudent(request, s.token);

  await page.goto("/home");
  await page.waitForURL(/\/login\?next=%2Fhome/);
  await studentSignIn(page, s.email, "wrong-password");
  await expect(page.getByRole("alert").filter({ hasText: "Incorrect email or password" })).toBeVisible();

  await page.getByLabel("Password").fill(s.password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL("**/home");
  await expect(page.getByRole("heading", { name: "Hi, Riya" })).toBeVisible();

  // Signed-in visitors skip the landing and login pages.
  await page.goto("/login");
  await page.waitForURL("**/home");
});

test("the website's session routes refuse cross-site requests and keep tokens out of page scripts", async ({ page, request }) => {
  const res = await request.post("/api/session/register", { headers: { origin: "https://evil.example" }, data: {} });
  expect(res.status()).toBe(403);

  const s = await registerStudent(request);
  await onboardStudent(request, s.token);
  await studentSignIn(page, s.email, s.password);
  await page.waitForURL("**/home");
  expect(await page.evaluate(() => document.cookie)).not.toContain("lp_rt");
  const cookies = await page.context().cookies();
  expect(cookies.find((c) => c.name === "lp_rt")?.httpOnly).toBe(true);
  void API_URL;
});

test("each page load wakes the API from the browser (free hosting only wakes on outside requests)", async ({ page }) => {
  const wake = page.waitForRequest((r) => r.url() === `${API_URL}/health`);
  await page.goto("/login");
  expect((await wake).method()).toBe("GET");
});
