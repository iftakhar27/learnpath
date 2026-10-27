import { expect, type APIRequestContext, type Page } from "@playwright/test";
import { API_URL } from "../playwright.config";

export const ADMIN = { email: "admin@example.com", password: "ChangeMe!2026" };
export const unique = (prefix: string) => `${prefix}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 6)}`;

/** Fills and submits the login form without waiting for the outcome (for negative tests). */
export async function submitLogin(page: Page, email: string, password: string) {
  await page.goto("/admin/login");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password").fill(password);
  await page.getByRole("button", { name: "Sign in" }).click();
}

/** Signs in and waits until the portal has loaded, so the session cookies are set. */
export async function signIn(page: Page, email = ADMIN.email, password = ADMIN.password) {
  await submitLogin(page, email, password);
  await page.waitForURL((url) => !url.pathname.startsWith("/admin/login"));
  await expect(page.getByRole("navigation", { name: "Admin" }).first()).toBeAttached();
}

/** Calls the API directly (not through the portal) to set up or verify state. */
export async function apiLogin(request: APIRequestContext, email: string, password: string): Promise<string> {
  const res = await request.post(`${API_URL}/api/v1/auth/login`, { data: { email, password } });
  expect(res.ok()).toBeTruthy();
  return (await res.json()).accessToken;
}

export async function registerStudent(request: APIRequestContext, name = "E2E Student") {
  const email = `${unique("student")}@e2e.test`;
  const res = await request.post(`${API_URL}/api/v1/auth/register`, { data: { email, password: "Passw0rd!x", fullName: name, acceptTerms: true } });
  expect(res.ok()).toBeTruthy();
  const body = await res.json();
  return { email, password: "Passw0rd!x", token: body.accessToken as string, userId: body.userId as string };
}

export async function apiGet(request: APIRequestContext, path: string, token: string) {
  const res = await request.get(`${API_URL}${path}`, { headers: { authorization: `Bearer ${token}` } });
  expect(res.ok(), `${path} -> ${res.status()}`).toBeTruthy();
  return res.json();
}

export async function apiPost(request: APIRequestContext, path: string, token: string, data?: unknown) {
  const res = await request.post(`${API_URL}${path}`, { headers: { authorization: `Bearer ${token}` }, data });
  expect(res.ok(), `${path} -> ${res.status()}`).toBeTruthy();
  return res.status() === 204 ? null : res.json();
}

/** Toasts confirm saves; wait for one so the next step sees committed data. */
export async function expectToast(page: Page, text: string | RegExp) {
  await expect(page.getByRole("status").filter({ hasText: text }).first()).toBeVisible();
}
