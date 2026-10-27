import { expect, test } from "@playwright/test";
import { apiGet, apiLogin, apiPost, ADMIN, expectToast, registerStudent, signIn, unique } from "./helpers";

test("teachers edit content but cannot publish or reach admin-only areas", async ({ page, request }) => {
  const teacher = await registerStudent(request, "E2E Teacher");
  const adminToken = await apiLogin(request, ADMIN.email, ADMIN.password);
  await apiPost(request, `/api/v1/admin/users/${teacher.userId}/roles`, adminToken, { role: "Teacher" });

  await signIn(page, teacher.email, teacher.password);
  await expect(page.getByRole("heading", { name: "Dashboard" })).toBeVisible();
  const nav = page.getByRole("navigation", { name: "Admin" });
  await expect(nav.getByRole("link", { name: "Question bank" })).toBeVisible();
  for (const hidden of ["Students", "Orders", "Announcements", "Audit log"]) await expect(nav.getByRole("link", { name: hidden })).toHaveCount(0);

  await page.goto("/admin/audit");
  await expect(page.getByText("You don't have access to this section.")).toBeVisible();

  await page.getByRole("link", { name: "Courses & syllabus" }).click();
  await page.getByRole("link", { name: "NEET Physics: Mechanics (Sample)" }).click();
  await expect(page.getByText("Laws of Motion").first()).toBeVisible();
  await expect(page.getByRole("button", { name: /^(Publish|Unpublish|Archive)$/ })).toHaveCount(0);
});

test("support replies to a ticket and grants course access", async ({ page, request }) => {
  const student = await registerStudent(request, "E2E Support Student");
  const subject = unique("Charged twice");
  await apiPost(request, "/api/v1/support/tickets", student.token, { category: "payment", subject, message: "I was charged twice for the course." });

  await signIn(page);
  await page.getByRole("link", { name: "Support" }).click();
  const ticket = page.locator("li").filter({ hasText: subject });
  await expect(ticket).toContainText("High priority");
  await ticket.getByRole("button", { name: "Reply" }).click();
  await page.getByRole("dialog").getByLabel("Your reply").fill("We have refunded the duplicate charge.");
  await page.getByRole("dialog").getByRole("button", { name: "Send reply" }).click();
  await expectToast(page, "Reply sent");
  await expect(page.locator("li").filter({ hasText: subject })).toHaveCount(0); // no longer open

  const mine = await apiGet(request, "/api/v1/support/tickets", student.token);
  expect(mine[0]).toMatchObject({ status: "Resolved", staffResponse: "We have refunded the duplicate charge." });

  // Grant course access from the student's page
  await page.getByRole("link", { name: "Students" }).click();
  await page.getByLabel("Search").fill(student.email);
  await page.getByLabel("Search").press("Enter");
  await expect(page).toHaveURL(/q=/);
  await expect(page.getByRole("link", { name: "E2E Support Student" })).toHaveCount(1); // filtered result, not the previous page
  await page.getByRole("link", { name: "E2E Support Student" }).click();
  await page.getByRole("button", { name: "Grant course access" }).click();
  await page.getByRole("dialog").getByLabel("Course").selectOption({ label: "NEET Physics: Mechanics (Sample)" });
  await page.getByRole("dialog").getByLabel("Reason").fill("Goodwill after duplicate charge");
  await page.getByRole("dialog").getByRole("button", { name: "Grant access" }).click();
  await expectToast(page, "Course access granted");
  await expect(page.getByRole("row").filter({ hasText: "NEET Physics: Mechanics (Sample)" })).toContainText("Admin");

  const courses = await apiGet(request, "/api/v1/courses?free=false", student.token);
  expect(courses.items.find((c: { slug: string }) => c.slug === "neet-physics-mechanics-sample").enrolled).toBe(true);

  await page.goto("/admin/audit");
  await page.getByLabel("Action starts with").fill("enrollment");
  await page.getByLabel("Action starts with").press("Enter");
  await expect(page.getByRole("cell", { name: "enrollment.grant" }).first()).toBeVisible();
});

test("announcements reach enrolled students", async ({ page, request }) => {
  const student = await registerStudent(request);
  const token = await apiLogin(request, ADMIN.email, ADMIN.password);
  const courses = await apiGet(request, "/api/v1/admin/courses", token);
  const free = courses.find((c: { slug: string }) => c.slug === "neet-physics-starter-sample");
  await apiPost(request, `/api/v1/courses/${free.id}/enroll`, student.token);
  const title = unique("Mock test on Sunday");

  await signIn(page);
  await page.getByRole("link", { name: "Announcements" }).click();
  await page.getByLabel("Audience").selectOption({ label: "NEET Physics Starter (Sample, Free)" });
  await page.getByLabel("Title").fill(title);
  await page.getByLabel("Message").fill("Full-length mock opens at 10 am.");
  await page.getByRole("button", { name: "Review and send" }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Send" }).click();
  await expectToast(page, /Sent to \d+ student/);

  const inbox = await apiGet(request, "/api/v1/notifications", student.token);
  expect(inbox.items.some((n: { title: string }) => n.title === title)).toBe(true);
});
