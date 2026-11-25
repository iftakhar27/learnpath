import { expect, test } from "@playwright/test";
import { apiGet, apiPost, expectToast, registerStudent, signIn, unique } from "./helpers";

const PDF = Buffer.from(
  "%PDF-1.4\n1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n2 0 obj\n<< /Type /Pages /Kids [] /Count 0 >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF\n",
  "latin1",
);

test("staff upload lesson notes in the course editor; students open them", async ({ page, request, browser }) => {
  const title = unique("E2E Notes course");
  await signIn(page);
  await page.getByRole("link", { name: "Courses & syllabus" }).click();
  await page.getByRole("button", { name: "New course" }).click();
  const courseDialog = page.getByRole("dialog");
  await courseDialog.getByLabel("Exam").selectOption({ label: "NEET (UG)" });
  await courseDialog.getByLabel("Title (English)").fill(title);
  await courseDialog.getByLabel("Free course").check();
  await courseDialog.getByRole("button", { name: "Create course" }).click();
  await expect(page.getByRole("heading", { name: new RegExp(title) })).toBeVisible();
  const courseId = page.url().split("/").pop()!;
  for (const [button, name, toast] of [
    ["Add subject", "Physics", "Subject added"],
    ["Add chapter", "Orbits (E2E notes chapter)", "Chapter added"],
    ["Add topic", "Kepler's laws", "Topic added"],
  ] as const) {
    await page.getByRole("button", { name: button }).click();
    await page.getByRole("dialog").getByLabel("Title (English)").fill(name);
    await page.getByRole("dialog").getByRole("button", { name: "Save" }).click();
    await expectToast(page, toast);
  }

  // Notes need a file; then it shows in the outline and can be published.
  await page.getByRole("button", { name: "Add video / notes" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Type").selectOption("Document");
  await dialog.getByLabel("Title (English)").fill("Kepler's laws - notes");
  await dialog.getByRole("button", { name: "Save" }).click();
  await expect(dialog.getByText("Choose the notes file (PDF).")).toBeVisible();
  await dialog.getByLabel("Notes file").setInputFiles({ name: "kepler notes.pdf", mimeType: "application/pdf", buffer: PDF });
  await dialog.getByRole("button", { name: "Save" }).click();
  await expectToast(page, "Resource added as draft");
  const row = page.locator("li").filter({ hasText: "Kepler's laws - notes" }).last();
  await expect(row).toContainText("kepler-notes.pdf");
  await row.getByRole("button", { name: "Publish" }).click();
  await expectToast(page, "Resource updated");
  await page.getByRole("button", { name: "Publish" }).first().click();
  await page.getByRole("dialog").getByRole("button", { name: "Publish" }).click();
  await expectToast(page, "Course status updated");

  // A student opens them: a signed link to the PDF.
  const s = await registerStudent(request, "Notes Student");
  const courses = await apiGet(request, `/api/v1/courses?q=${encodeURIComponent(title)}`, s.token);
  const course = courses.items[0];
  await apiPost(request, `/api/v1/courses/${course.id}/enroll`, s.token);
  const tree = await apiGet(request, `/api/v1/courses/${courseId}/syllabus`, s.token);
  const topicId = tree.subjects[0].chapters[0].topics[0].id;
  const topic = await apiGet(request, `/api/v1/topics/${topicId}`, s.token);
  const notes = topic.resources.find((r: { title: string }) => r.title === "Kepler's laws - notes");
  const playback = await apiGet(request, `/api/v1/resources/${notes.id}/playback`, s.token);
  expect(playback.url).toMatch(new RegExp(`^/api/v1/resources/${notes.id}/file\\?expires=`));
  const anonymous = await browser.newContext();
  const file = await anonymous.request.get(playback.url);
  expect(file.status()).toBe(200);
  expect(file.headers()["content-type"]).toBe("application/pdf");
  expect(file.headers()["content-disposition"]).toContain("kepler-notes.pdf");
  await anonymous.close();
});
