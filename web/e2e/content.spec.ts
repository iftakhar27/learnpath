import { expect, test } from "@playwright/test";
import { apiGet, apiLogin, apiPost, ADMIN, expectToast, registerStudent, signIn, unique } from "./helpers";

test("staff build a course, question and test end to end, and a student can take the test", async ({ page, request }) => {
  const title = unique("E2E Chemistry");
  const stem = `How many atoms are in one molecule of water? ${unique("q")}`;
  await signIn(page);

  // Course
  await page.getByRole("link", { name: "Courses & syllabus" }).click();
  await page.getByRole("button", { name: "New course" }).click();
  const courseDialog = page.getByRole("dialog");
  await courseDialog.getByLabel("Exam").selectOption({ label: "JEE Main" });
  await courseDialog.getByLabel("Title (English)").fill(title);
  await expect(courseDialog.getByLabel("URL slug")).toHaveValue(/^e2e-chemistry-/);
  await courseDialog.getByLabel("Free course").check();
  await courseDialog.getByRole("button", { name: "Create course" }).click();
  await expect(page.getByRole("heading", { name: new RegExp(title) })).toBeVisible();
  const courseId = page.url().split("/").pop()!;

  // Syllabus: subject → chapter → topic → video
  for (const [button, name, toast] of [
    ["Add subject", "Chemistry", "Subject added"],
    ["Add chapter", "Mole Concept", "Chapter added"],
    ["Add topic", "Avogadro number", "Topic added"],
  ] as const) {
    await page.getByRole("button", { name: button }).click();
    await page.getByRole("dialog").getByLabel("Title (English)").fill(name);
    await page.getByRole("dialog").getByRole("button", { name: "Save" }).click();
    await expectToast(page, toast);
  }
  await page.getByRole("button", { name: "Add video / notes" }).click();
  const resDialog = page.getByRole("dialog");
  await resDialog.getByLabel("Title (English)").fill("Intro to moles");
  await resDialog.getByLabel("Video ID at the streaming provider").fill("vid-e2e-001");
  await resDialog.getByLabel("Duration (mm:ss)").fill("08:30");
  await resDialog.getByRole("button", { name: "Save" }).click();
  await expectToast(page, "Resource added as draft");
  const resourceRow = page.locator("li").filter({ hasText: "Intro to moles" }).last();
  await expect(resourceRow).toContainText("8:30");
  await resourceRow.getByRole("button", { name: "Publish" }).click();
  await expectToast(page, "Resource updated");
  await expect(resourceRow).toContainText("Published");

  // Question, started from the topic's question link so the topic is pre-selected
  await page.getByRole("link", { name: "0 questions" }).click();
  await expect(page.getByText("No questions match")).toBeVisible();
  await page.getByRole("link", { name: "Add a question" }).click();
  // Visited pages stay in the DOM (hidden) in Next.js 16, so target the visible copy.
  await expect(page.getByLabel("Topic").filter({ visible: true })).toHaveValue(/.+/);
  await page.getByLabel("Question text (English)").fill(stem);
  for (const [letter, text] of [["A", "3"], ["B", "2"], ["C", "1"], ["D", "4"]]) await page.getByLabel(`Option ${letter} (English)`).fill(text);
  await page.getByRole("button", { name: "Save question" }).click();
  await expect(page.getByRole("alert").filter({ hasText: "Mark exactly one option as correct" })).toBeVisible();
  await page.getByLabel("Option A is correct").check();
  await page.getByLabel("Explanation (English)").fill("H₂O has two hydrogen atoms and one oxygen atom.");
  await page.getByRole("button", { name: "Save question" }).click();
  await expectToast(page, "Question created as draft");
  await expect(page).toHaveURL(/\/admin\/questions\/[0-9a-f-]{36}$/);
  await page.getByRole("button", { name: "Publish" }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Publish" }).click();
  await expectToast(page, "Status updated");

  // Test
  await page.goto(`/admin/tests/new?courseId=${courseId}`);
  await page.getByLabel("Title (English)").fill(`${title} quiz`);
  await page.getByLabel("Duration (minutes)").fill("10");
  await page.getByLabel("Free test").check();
  await page.getByRole("button", { name: "Add questions" }).first().click();
  const picker = page.getByRole("dialog");
  await picker.getByLabel(/^Select: How many atoms/).check();
  await picker.getByRole("button", { name: "Add 1 question" }).click();
  await expect(page.getByText(stem.slice(0, 40))).toBeVisible();
  await page.getByRole("button", { name: "Create test" }).click();
  await expectToast(page, "Test created as draft");
  await expect(page).toHaveURL(/\/admin\/tests\/[0-9a-f-]{36}$/);
  const testId = page.url().split("/").pop()!;
  await page.getByRole("button", { name: "Publish" }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Publish" }).click();
  await expectToast(page, "Test status updated");

  // Publish the course
  await page.goto(`/admin/catalog/${courseId}`);
  await page.getByRole("button", { name: "Publish" }).first().click();
  await page.getByRole("dialog").getByRole("button", { name: "Publish" }).click();
  await expectToast(page, "Course status updated");

  // A student now sees the course and can take the test through the real API
  const student = await registerStudent(request);
  const courses = await apiGet(request, `/api/v1/courses?q=${encodeURIComponent(title)}`, student.token);
  expect(courses.items).toHaveLength(1);
  const attempt = await apiPost(request, `/api/v1/tests/${testId}/attempts`, student.token);
  expect(attempt.questions).toHaveLength(1);
  expect(attempt.questions[0].stem).toBe(stem);

  // And every step was audited
  const adminToken = await apiLogin(request, ADMIN.email, ADMIN.password);
  const logs = await apiGet(request, `/api/v1/admin/audit-logs?entityType=Test`, adminToken);
  expect(logs.items.some((l: { entityId: string; action: string }) => l.entityId === testId && l.action === "test.status")).toBe(true);
});

test("CSV import reports row-level errors and imports valid rows as drafts", async ({ page, request }) => {
  const token = await apiLogin(request, ADMIN.email, ADMIN.password);
  const courses = await apiGet(request, "/api/v1/admin/courses", token);
  const sample = courses.find((c: { slug: string }) => c.slug === "neet-physics-starter-sample");
  const tree = await apiGet(request, `/api/v1/admin/courses/${sample.id}/tree`, token);
  const topicId = tree.subjects[0].chapters[0].topics[0].id;
  const ref = unique("CSV");
  const csv = [
    "external_ref,topic_id,type,difficulty,stem_en,stem_hi,option_a,option_b,option_c,option_d,correct,explanation_en,marks,negative_marks",
    `${ref}-1,${topicId},SingleCorrect,Easy,"Unit of force, in SI?",बल का मात्रक?,Newton,Joule,,,A,Basic,4,1`,
    `${ref}-2,${topicId},Essay,Easy,Bad type,,,,,,x,,4,1`,
  ].join("\n");

  await signIn(page);
  await page.goto("/admin/questions/import");
  await page.getByLabel("CSV file").setInputFiles({ name: "questions.csv", mimeType: "text/csv", buffer: Buffer.from(csv, "utf8") });
  await page.getByRole("button", { name: "Upload and import" }).click();
  await expect(page.getByText("1 imported as draft")).toBeVisible();
  await expect(page.getByText("1 need fixing")).toBeVisible();
  await expect(page.getByRole("cell", { name: "Unknown type 'Essay'." })).toBeVisible();

  await page.getByRole("link", { name: "Review drafts" }).click();
  await page.getByLabel("Search").fill(`${ref}-1`);
  await page.getByLabel("Search").press("Enter");
  await expect(page).toHaveURL(new RegExp(`q=${ref}-1`));
  // Earlier runs imported the same text under other references; only this run's row should match.
  await expect(page.getByRole("link", { name: "Unit of force, in SI?" }).filter({ visible: true })).toHaveCount(1);
});
