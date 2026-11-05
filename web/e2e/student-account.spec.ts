import { expect, test, type APIRequestContext, type Page } from "@playwright/test";
import { API_URL } from "../playwright.config";
import { ADMIN, apiGet, apiLogin, apiPost, onboardStudent, registerStudent, studentSignIn, unique } from "./helpers";

async function signedInStudent(page: Page, request: APIRequestContext, name = "Account Student") {
  const s = await registerStudent(request, name);
  await onboardStudent(request, s.token);
  await studentSignIn(page, s.email, s.password);
  await page.waitForURL("**/home");
  return s;
}

async function freeCourse(request: APIRequestContext, token: string) {
  const courses = await apiGet(request, "/api/v1/courses?free=true", token);
  return courses.items[0] as { id: string; title: string };
}

test("study preferences save, the plan can be worked through, and progress charts submitted tests", async ({ page, request }) => {
  const s = await signedInStudent(page, request);
  const free = await freeCourse(request, s.token);
  await apiPost(request, `/api/v1/courses/${free.id}/enroll`, s.token);

  // Preferences from the profile.
  await page.goto("/profile");
  await page.getByRole("link", { name: "Study preferences" }).click();
  await page.getByRole("button", { name: "Next" }).click();
  await page.getByRole("radio", { name: "Dropper" }).click();
  await page.getByRole("button", { name: "Next" }).click();
  await page.getByRole("button", { name: "Save" }).click();
  await page.waitForURL("**/profile");
  await expect.poll(async () => (await apiGet(request, "/api/v1/student/profile", s.token)).classLevel).toBe("Dropper");

  // Study plan: complete one task, skip another.
  await page.goto("/plan");
  await expect(page.getByRole("heading", { name: "Study plan" })).toBeVisible();
  const doneButtons = page.getByRole("button", { name: /^Done: / });
  await expect(doneButtons.first()).toBeVisible();
  const before = await doneButtons.count();
  await doneButtons.first().click();
  await expect(page.getByText("Completed").first()).toBeVisible();
  if (before > 1) {
    await page.getByRole("button", { name: /^Skip: / }).first().click();
    await expect(page.getByText("Skipped").first()).toBeVisible();
  }

  // A submitted test shows on the progress chart, with a tooltip and a table view.
  const tests = await apiGet(request, "/api/v1/tests", s.token);
  const quiz = tests.find((t: { isFree: boolean; locked: boolean }) => t.isFree && !t.locked);
  const attempt = await apiPost(request, `/api/v1/tests/${quiz.id}/attempts`, s.token);
  await apiPost(request, `/api/v1/attempts/${attempt.attemptId}/submit`, s.token);
  await page.goto("/progress");
  await expect(page.getByRole("heading", { name: "My progress" })).toBeVisible();
  const point = page.getByRole("button", { name: new RegExp(`${quiz.title.replace(/[()]/g, "\\$&")}.*%`) });
  await point.focus();
  await expect(page.getByRole("tooltip")).toContainText(quiz.title);
  await page.getByText("Show as table").click();
  await expect(page.getByRole("cell", { name: quiz.title })).toBeVisible();
});

test("notifications from staff show on the bell and can be read", async ({ page, request }) => {
  const s = await signedInStudent(page, request);
  const free = await freeCourse(request, s.token);
  await apiPost(request, `/api/v1/courses/${free.id}/enroll`, s.token);
  const admin = await apiLogin(request, ADMIN.email, ADMIN.password);
  const title = unique("Live doubt session");
  await apiPost(request, "/api/v1/admin/announcements", admin, { title, body: "Join us on Sunday at 6 pm.", courseId: free.id });

  await page.reload();
  await expect(page.getByRole("link", { name: /^Notifications: [1-9]\d* unread notifications?$/ })).toBeVisible();
  await page.getByRole("link", { name: /^Notifications:/ }).click();
  await page.getByRole("button", { name: new RegExp(title) }).click();
  await expect(page.getByRole("button", { name: new RegExp(`Unread: ${title}`) })).toHaveCount(0);
});

test("saved items, support tickets with replies, and purchases", async ({ page, request }) => {
  const s = await signedInStudent(page, request);
  const free = await freeCourse(request, s.token);
  const syllabus = await apiGet(request, `/api/v1/courses/${free.id}/syllabus`, s.token);
  const topic = syllabus.subjects[0].chapters[0].topics[0];
  await apiPost(request, "/api/v1/learning/bookmarks", s.token, { entityType: "Topic", entityId: topic.id });

  await page.goto("/bookmarks");
  await expect(page.getByRole("link", { name: new RegExp(topic.title) })).toBeVisible();
  await page.getByRole("button", { name: `Remove: ${topic.title}` }).click();
  await expect(page.getByText("Nothing saved yet.", { exact: false })).toBeVisible();

  await page.goto("/support");
  await page.getByRole("button", { name: "Send" }).click();
  await expect(page.getByText("This field is required").first()).toBeVisible();
  const subject = unique("Video not loading");
  await page.getByLabel("Topic").selectOption("technical");
  await page.getByLabel("Subject").fill(subject);
  await page.getByLabel("How can we help?").fill("The lesson video shows an error on my laptop.");
  await page.getByRole("button", { name: "Send" }).click();
  await expect(page.getByRole("heading", { name: subject })).toBeVisible();
  const admin = await apiLogin(request, ADMIN.email, ADMIN.password);
  const ticket = (await apiGet(request, "/api/v1/support/tickets", s.token)).find((t: { subject: string }) => t.subject === subject);
  await apiPost(request, `/api/v1/admin/tickets/${ticket.id}/respond`, admin, { response: "Please try another browser while we fix it.", status: "Resolved" });
  await page.reload();
  await expect(page.getByText("Reply from support")).toBeVisible();
  await expect(page.getByText("Please try another browser while we fix it.")).toBeVisible();

  // Starting a purchase on a test server leaves a pending order.
  const paid = (await apiGet(request, "/api/v1/courses?free=false", s.token)).items[0];
  await apiPost(request, "/api/v1/payments/orders", s.token, { courseId: paid.id });
  await page.goto("/purchases");
  await expect(page.getByText(paid.title)).toBeVisible();
  await expect(page.getByText("Pending")).toBeVisible();
});

test("signing out, and deleting the account signs out and blocks sign-in", async ({ page, request }) => {
  const s = await signedInStudent(page, request);
  await page.goto("/profile");
  await page.getByRole("button", { name: "Sign out" }).click();
  await page.waitForURL("**/login");
  await page.goto("/home");
  await page.waitForURL(/\/login\?next=/);

  await studentSignIn(page, s.email, s.password);
  await page.waitForURL("**/home");
  await page.goto("/profile");
  await page.getByRole("button", { name: "Delete my account" }).click();
  await page.getByRole("dialog", { name: "Delete my account" }).getByRole("button", { name: "Delete" }).click();
  await page.waitForURL(/\/login\?deleted=1/);
  await expect(page.getByText("Your deletion request has been recorded.")).toBeVisible();
  const res = await request.post(`${API_URL}/api/v1/auth/login`, { data: { email: s.email, password: s.password } });
  expect(res.status()).toBe(403);
});
