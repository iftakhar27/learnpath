import { expect, test, type Page } from "@playwright/test";
import { apiGet, apiPost, onboardStudent, registerStudent, studentSignIn } from "./helpers";

const FREE_TEST = "Dimensional Analysis Quiz (Sample)";

async function signedInStudent(page: Page, request: import("@playwright/test").APIRequestContext) {
  const s = await registerStudent(request, "Assess Student");
  await onboardStudent(request, s.token);
  await studentSignIn(page, s.email, s.password);
  await page.waitForURL("**/home");
  return s;
}

/** Answers the visible question: first option for option questions, a number for numerical ones. */
async function answer(page: Page, region = page.getByRole("main")) {
  const numeric = region.getByLabel("Enter your answer");
  if (await numeric.count()) return numeric.fill("2");
  await region.getByRole("radio").or(region.getByRole("checkbox")).first().click();
}

test("practice a topic: check answers, read explanations, report a problem, see the summary", async ({ page, request }) => {
  const s = await signedInStudent(page, request);
  const courses = await apiGet(request, "/api/v1/courses?free=true&q=NEET%20Physics%20Starter", s.token);
  const free = courses.items[0];
  await apiPost(request, `/api/v1/courses/${free.id}/enroll`, s.token);
  const syllabus = await apiGet(request, `/api/v1/courses/${free.id}/syllabus`, s.token);
  const topic = syllabus.subjects[0].chapters[0].topics[0];

  await page.goto(`/topics/${topic.id}`);
  await page.getByRole("link", { name: /Practise this topic/ }).click();
  await expect(page.getByText(/Question 1 of \d+/).first()).toBeVisible();
  const total = Number((await page.getByText(/Question 1 of \d+/).first().innerText()).match(/of (\d+)/)![1]);

  await expect(page.getByRole("button", { name: "Check answer" })).toBeDisabled();
  await answer(page);
  await page.getByRole("button", { name: "Check answer" }).click();
  await expect(page.getByText(/^(Correct|Incorrect|Partially correct)$/)).toBeVisible();

  await page.getByRole("button", { name: "Report a problem" }).click();
  await page.getByLabel("Typo or formatting").check();
  await page.getByLabel("Details (optional)").fill("Unit symbol looks wrong.");
  await page.getByRole("button", { name: "Send" }).click();
  await expect(page.getByText("Thanks, our team will review it.")).toBeVisible();

  for (let i = 1; i < total; i++) {
    await page.getByRole("button", { name: "Next" }).click();
    await answer(page);
    await page.getByRole("button", { name: "Check answer" }).click();
  }
  await page.getByRole("button", { name: "Finish" }).click();
  await expect(page.getByRole("heading", { name: "Practice summary" })).toBeVisible();
  await expect(page.getByText("Accuracy")).toBeVisible();
});

test("take a test: answer, mark for review, use the palette, submit, see the result and review", async ({ page, request }) => {
  const s = await signedInStudent(page, request);
  await page.getByRole("link", { name: "Tests" }).first().click();
  await page.getByRole("link", { name: `Start test: ${FREE_TEST}` }).click();
  await expect(page.getByRole("heading", { name: FREE_TEST })).toBeVisible();
  await expect(page.getByText("The timer keeps running if you leave.")).toBeVisible();
  await page.getByRole("button", { name: "Start test" }).click();

  await page.waitForURL(/\/exam\//);
  await expect(page.getByText("Question 1 of 3").first()).toBeVisible();
  await expect(page.getByRole("timer")).toHaveText(/(10:00|9:5\d)/);
  await answer(page);
  await expect(page.getByRole("status").filter({ hasText: /^Saved$/ }).first()).toBeVisible();

  await page.getByRole("button", { name: "Save & next" }).click();
  await expect(page.getByText("Question 2 of 3").first()).toBeVisible();
  await page.getByRole("button", { name: "Mark for review" }).click();
  await expect(page.getByRole("button", { name: "Unmark review" })).toBeVisible();

  const palette = page.getByRole("complementary", { name: "Questions" });
  await expect(palette.getByRole("button", { name: "Question 1 of 3, Answered" })).toBeVisible();
  await expect(palette.getByRole("button", { name: "Question 2 of 3, Marked for review" })).toBeVisible();
  await palette.getByRole("button", { name: /Question 3 of 3/ }).click();
  await expect(page.getByText("Question 3 of 3").first()).toBeVisible();

  // The server has what the palette shows.
  const attemptId = page.url().split("/exam/")[1];
  await expect.poll(async () => (await apiGet(request, `/api/v1/attempts/${attemptId}`, s.token)).questions[1].markedForReview).toBe(true);

  await page.getByRole("button", { name: "Submit", exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "Submit the test?" });
  await expect(dialog).toContainText("Answered: 1");
  await expect(dialog).toContainText("Marked for review: 1");
  await expect(dialog).toContainText("Not answered: 2");
  await dialog.getByRole("button", { name: "Submit", exact: true }).click();

  await page.waitForURL(/\/results\//);
  await expect(page.getByRole("heading", { name: FREE_TEST })).toBeVisible();
  await expect(page.getByText(/\d+ \/ \d+/).first()).toBeVisible();
  await page.getByRole("link", { name: "Review answers" }).click();
  await expect(page.getByRole("heading", { name: "Review answers" })).toBeVisible();
  await expect(page.getByRole("listitem").filter({ hasText: "Question 1 of 3" })).toBeVisible();
  await page.getByRole("radio", { name: "Skipped" }).click();
  await expect(page.getByRole("listitem").filter({ hasText: "Question 1 of 3" })).toHaveCount(0);
  await expect(page.getByRole("listitem").filter({ hasText: "Question 3 of 3" })).toBeVisible();
});

test("an attempt resumes after reload with answers kept, and offline answers are saved once back online", async ({ page, request }) => {
  const s = await signedInStudent(page, request);
  const tests = await apiGet(request, "/api/v1/tests", s.token);
  const quiz = tests.find((t: { title: string }) => t.title === FREE_TEST);
  await page.goto(`/tests/${quiz.id}`);
  await page.getByRole("button", { name: "Start test" }).click();
  await page.waitForURL(/\/exam\//);
  const attemptId = page.url().split("/exam/")[1];
  await answer(page);
  await expect(page.getByRole("status").filter({ hasText: /^Saved$/ }).first()).toBeVisible();

  await page.reload();
  await expect(page.getByText("Question 1 of 3").first()).toBeVisible();
  const palette = page.getByRole("complementary", { name: "Questions" });
  await expect(palette.getByRole("button", { name: "Question 1 of 3, Answered" })).toBeVisible();

  // Offline: the answer is kept locally and retried.
  await palette.getByRole("button", { name: /Question 2 of 3/ }).click();
  await page.context().setOffline(true);
  await answer(page);
  await expect(page.getByText("Offline. Answers will be saved when you're back online.").first()).toBeVisible();
  await page.context().setOffline(false);
  await expect(page.getByRole("status").filter({ hasText: /^Saved$/ }).first()).toBeVisible({ timeout: 20_000 });
  await expect.poll(async () => (await apiGet(request, `/api/v1/attempts/${attemptId}`, s.token)).questions[1].response).not.toBeNull();
});
