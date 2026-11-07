import { expect, test, type APIRequestContext, type Page } from "@playwright/test";
import { ADMIN, apiGet, apiLogin, apiPost, expectToast, onboardStudent, registerStudent, signIn, studentSignIn, unique } from "./helpers";

const TOPIC = "Dimensional Analysis"; // free sample course, 3 single-correct questions whose option A is right

/** A signed-in student enrolled in the free sample course; returns the topic's id and the API token. */
async function studentInFreeCourse(page: Page, request: APIRequestContext, name: string) {
  const s = await registerStudent(request, name);
  await onboardStudent(request, s.token);
  const courses = await apiGet(request, "/api/v1/courses?free=true", s.token);
  const course = courses.items.find((c: { title: string }) => c.title.startsWith("NEET Physics Starter"));
  await apiPost(request, `/api/v1/courses/${course.id}/enroll`, s.token);
  const syllabus = await apiGet(request, `/api/v1/courses/${course.id}/syllabus`, s.token);
  const topic = syllabus.subjects[0].chapters[0].topics.find((t: { title: string }) => t.title === TOPIC);
  await studentSignIn(page, s.email, s.password);
  await page.waitForURL("**/home");
  return { ...s, topicId: topic.id as string };
}

test("a student asks a doubt from a topic, a teacher answers it in the portal, and the student follows up", async ({ page, browser, request }) => {
  const s = await studentInFreeCourse(page, request, "Doubt Student");
  const doubtText = `Why do work and torque share dimensions? ${unique("d")}`;

  await page.goto(`/topics/${s.topicId}`);
  await page.getByRole("button", { name: "Ask a doubt" }).click();
  const dialog = page.getByRole("dialog", { name: "Ask a doubt" });
  await expect(dialog.getByText(`About: ${TOPIC}`)).toBeVisible();
  await dialog.getByRole("button", { name: "Send" }).click();
  await expect(dialog.getByText("Write at least 5 characters.")).toBeVisible();
  await dialog.getByLabel("Your doubt").fill(doubtText);
  await dialog.getByRole("button", { name: "Send" }).click();
  await expectToast(page, "Doubt sent");

  await page.goto("/doubts");
  const item = page.getByRole("link", { name: new RegExp(doubtText.slice(0, 30)) });
  await expect(item).toContainText("Waiting for a teacher");
  await expect(item).toContainText(`About: ${TOPIC}`);

  // A teacher answers in the admin portal.
  const staff = await browser.newContext();
  const admin = await staff.newPage();
  await signIn(admin);
  await admin.getByRole("link", { name: "Doubts", exact: true }).first().click();
  await admin.getByRole("link", { name: new RegExp(doubtText.slice(0, 30)) }).click();
  await expect(admin.getByText(TOPIC).first()).toBeVisible();
  await admin.getByLabel("Your answer").fill("Both are force × distance, but torque is a moment, not energy.");
  await admin.getByRole("button", { name: "Send reply" }).click();
  await expectToast(admin, "Reply sent");
  await expect(admin.getByText("Answered").first()).toBeVisible();
  await staff.close();

  // The student sees the answer, asks a follow-up, then marks it solved.
  await item.click();
  await expect(page.getByText(/Teacher · /)).toBeVisible();
  await expect(page.getByText("torque is a moment, not energy")).toBeVisible();
  await expect(page.getByText("Answered").first()).toBeVisible();
  await page.getByLabel("Ask a follow-up").fill("So the units match but the meaning differs?");
  await page.getByRole("button", { name: "Send", exact: true }).click();
  await expect(page.getByText("So the units match but the meaning differs?")).toBeVisible();
  await expect(page.getByText("Waiting for a teacher").first()).toBeVisible();
  await page.getByRole("button", { name: "My doubt is solved" }).click();
  await expect(page.getByText("Resolved").first()).toBeVisible();
  await expect(page.getByText("Still unclear? Ask a follow-up")).toBeVisible();

  const notes = await apiGet(request, "/api/v1/notifications", s.token);
  expect(notes.items[0]).toMatchObject({ type: "doubt", title: "Your doubt was answered" });
});

test("finishing a topic offers its quiz, which is scored when submitted", async ({ page, request }) => {
  const s = await studentInFreeCourse(page, request, "Quiz Student");
  // Cover the topic: watch the video and open the notes (through the API, as the student's phone).
  const topic = await apiGet(request, `/api/v1/topics/${s.topicId}`, s.token);
  for (const r of topic.resources)
    await apiPost(request, "/api/v1/learning/progress", s.token, { resourceId: r.id, positionSeconds: r.durationSeconds ?? 0, markCompleted: true });

  await page.goto(`/topics/${s.topicId}`);
  const card = page.getByRole("region", { name: "Topic quiz" });
  await expect(card).toContainText("You've covered this topic");
  await expect(card).toContainText("3 questions · pass mark 60%");
  await card.getByRole("link", { name: "Start quiz" }).click();

  await expect(page.getByRole("heading", { name: `Topic quiz: ${TOPIC}` })).toBeVisible();
  const questions = page.locator("ol > li");
  await expect(questions).toHaveCount(3);
  await questions.nth(0).getByRole("radio").first().click();
  await questions.nth(1).getByRole("radio").first().click();
  await page.getByRole("button", { name: "Submit quiz" }).click();
  const confirm = page.getByRole("dialog", { name: "Submit quiz" });
  await expect(confirm).toContainText("1 question is unanswered. Submit anyway?");
  await confirm.getByRole("button", { name: "Submit", exact: true }).click();

  await expect(page.getByText("67%")).toBeVisible();
  await expect(page.getByText("Well done! You passed this topic's quiz.")).toBeVisible();
  await expect(page.getByRole("heading", { name: "Review answers" })).toBeVisible();
  await expect(questions.nth(2)).toContainText("Not answered");
  await expect(questions.nth(0)).toContainText("Correct");
  await expect(questions.nth(0).getByRole("button", { name: "Ask a doubt" })).toBeVisible();

  await page.getByRole("link", { name: "Back to topic" }).click();
  await expect(card).toContainText("Passed · Best score 67%");
  await expect(card.getByRole("link", { name: "Retake quiz" })).toBeVisible();
});

test("staff set how many questions a topic's quiz has", async ({ page, request }) => {
  const token = await apiLogin(request, ADMIN.email, ADMIN.password);
  const exams = await apiGet(request, "/api/v1/admin/exams", token);
  const title = unique("E2E Quiz Course");
  const { id: courseId } = await apiPost(request, "/api/v1/admin/courses", token, {
    examId: exams[0].id, slug: title.toLowerCase().replace(/[^a-z0-9]+/g, "-"), titleEn: title, isFree: true, priceInPaise: 0,
  });
  const { id: subjectId } = await apiPost(request, "/api/v1/admin/subjects", token, { parentId: courseId, titleEn: "Physics" });
  const { id: chapterId } = await apiPost(request, "/api/v1/admin/chapters", token, { parentId: subjectId, titleEn: "Optics" });
  await apiPost(request, "/api/v1/admin/topics", token, { parentId: chapterId, titleEn: "Reflection" });

  await signIn(page);
  await page.goto(`/admin/catalog/${courseId}`);
  const row = page.getByRole("listitem", { name: "Topic Reflection" });
  await expect(row).toContainText("Quiz: needs published questions");
  await row.getByRole("button", { name: "Edit" }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByLabel("Questions per quiz")).toHaveValue("5");
  await dialog.getByLabel("Questions per quiz").fill("0");
  await dialog.getByLabel("Pass mark (%)").fill("70");
  await dialog.getByRole("button", { name: "Save" }).click();
  await expectToast(page, "Saved");
  await expect(row).toContainText("No quiz");

  const tree = await apiGet(request, `/api/v1/admin/courses/${courseId}/tree`, token);
  expect(tree.subjects[0].chapters[0].topics[0]).toMatchObject({ quizQuestionCount: 0, quizPassPercent: 70 });
});
