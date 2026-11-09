import { expect, test } from "@playwright/test";
import { ADMIN, apiGet, apiLogin, apiPost, onboardStudent, registerStudent } from "./helpers";

const PAID = "NEET Physics: Mechanics (Sample)";

test("the app's Buy on website opens the course signed in, with checkout started, and the link works once", async ({ page, request }) => {
  // The student uses the app (its session is the phone's) and taps "Buy on website".
  const s = await registerStudent(request, "App Buyer");
  await onboardStudent(request, s.token);
  const courses = await apiGet(request, `/api/v1/courses?free=false&q=${encodeURIComponent(PAID)}`, s.token);
  const course = courses.items[0];
  const { code } = await apiPost(request, "/api/v1/auth/handoff", s.token);
  const link = `/handoff?code=${encodeURIComponent(code)}&next=${encodeURIComponent(`/learn/${course.id}?buy=1&from=app`)}`;

  await page.goto(link);
  await page.waitForURL(`**/learn/${course.id}?buy=1&from=app`);
  await expect(page.getByRole("heading", { name: PAID, level: 1 })).toBeVisible();
  // Checkout starts by itself (this test server simulates payments, so it explains that instead of opening Razorpay).
  await expect(page.getByText("Payments are simulated on this test server")).toBeVisible();
  // The app stays signed in alongside the website.
  await apiGet(request, "/api/v1/student/profile", s.token);

  // Once the course is owned, the page points back to the app.
  const admin = await apiLogin(request, ADMIN.email, ADMIN.password);
  await apiPost(request, `/api/v1/admin/users/${s.userId}/enrollments`, admin, { courseId: course.id, reason: "e2e" });
  await page.reload();
  await expect(page.getByText("You have this course. Go back to the app to start learning.")).toBeVisible();

  // The link is single-use: opening it again asks the visitor to sign in.
  await page.context().clearCookies();
  await page.goto(link);
  await expect(page.getByText("This link from the app has expired. Please sign in to continue.")).toBeVisible();
  await page.getByRole("link", { name: "Sign in" }).click();
  await page.waitForURL(/\/login\?next=/);
});
