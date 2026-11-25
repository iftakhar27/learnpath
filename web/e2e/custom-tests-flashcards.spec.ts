import { expect, test } from "@playwright/test";
import { STUDENT_AREAS, config } from "../src/proxy";
import { onboardStudent, registerStudent, signIn, studentSignIn, unique } from "./helpers";

test("a student builds a custom test from chosen chapters and takes it in the exam player", async ({ page, request }) => {
  const s = await registerStudent(request, "Custom Test Student");
  await onboardStudent(request, s.token);
  await studentSignIn(page, s.email, s.password);
  await page.waitForURL("**/home");

  await page.goto("/tests");
  await page.getByRole("link", { name: /Create a custom test/ }).click();
  await expect(page.getByRole("heading", { name: "Create a custom test", level: 1 })).toBeVisible();
  await expect(page.getByRole("tab", { name: "Physics" })).toHaveAttribute("aria-selected", "true");

  // Free chapters can be picked; chapters only in paid courses can't.
  const units = page.getByRole("checkbox", { name: /Units and Measurement/ });
  await expect(page.getByRole("checkbox", { name: /Laws of Motion/ })).toBeDisabled();
  await expect(page.getByText("Choose at least one chapter")).toBeVisible();
  await expect(page.getByRole("button", { name: "Create test" })).toBeDisabled();
  await units.check();
  await expect(page.getByText("1 chapter selected")).toBeVisible();
  await expect(page.getByRole("group", { name: "Number of questions" }).getByRole("button", { name: /^All \(\d+\)$/ })).toHaveAttribute("aria-pressed", "true");
  await expect(page.getByText(/^\d+ questions? · \d+ min$/)).toBeVisible();
  await page.getByRole("button", { name: "Hard" }).click();
  await page.getByRole("button", { name: "Any" }).click();
  await expect(page.getByRole("checkbox", { name: /Skip questions I've already got right/ })).toBeChecked();

  // Creating it opens the instructions; it runs like any other test.
  await page.getByRole("button", { name: "Create test" }).click();
  await expect(page.getByRole("heading", { name: "Custom test: Units and Measurement", level: 1 })).toBeVisible();
  await expect(page.getByText("Made by you from: Units and Measurement.")).toBeVisible();
  await page.getByRole("button", { name: "Start test" }).click();
  await expect(page.getByText(/^Question 1 of \d+$/).first()).toBeVisible();

  // Listed under "My custom tests", ready to resume.
  await page.goto("/tests");
  const mine = page.getByRole("region", { name: "My custom tests" });
  await expect(mine.getByText("Your test")).toBeVisible();
  await expect(mine.getByRole("link", { name: "Resume test: Custom test: Units and Measurement" })).toBeVisible();
});

test("flash cards: flip, answer, and the deck remembers what's due", async ({ page, request }) => {
  const s = await registerStudent(request, "Flash Card Student");
  await onboardStudent(request, s.token);
  await studentSignIn(page, s.email, s.password);
  await page.waitForURL("**/home");

  await page.goto("/practice");
  await page.getByRole("link", { name: /Flash cards/ }).first().click();
  await expect(page.getByRole("heading", { name: "Flash cards", level: 1 })).toBeVisible();
  const deck = page.getByRole("region", { name: "Physics · Class 11" }).getByRole("link", { name: /Units and Measurement/ });
  await expect(deck).toContainText("11 cards");
  await expect(deck).toContainText("11 cards due");
  await deck.click();

  await expect(page.getByRole("heading", { name: "Units and Measurement", level: 1 })).toBeVisible();
  await expect(page.getByText("Card 1 of 11")).toBeVisible();
  await expect(page.getByRole("button", { name: /^Question: How many SI base units are there\?/ })).toBeVisible();
  await page.getByRole("button", { name: "Show answer" }).click();
  await expect(page.getByRole("button", { name: /^Answer: Seven: metre, kilogram/ })).toBeVisible();
  await page.getByRole("button", { name: "Got it" }).click();

  await expect(page.getByText("Card 2 of 11")).toBeVisible();
  await page.getByRole("button", { name: /^Question: Dimensional formula of force/ }).click(); // tapping the card flips it
  await page.getByRole("button", { name: "Still learning" }).click();
  await expect(page.getByText("Card 3 of 11")).toBeVisible();

  // Keyboard: Space flips, → is "got it".
  await page.locator("body").press("Space");
  await expect(page.getByRole("button", { name: "Got it" })).toBeVisible();
  await page.locator("body").press("ArrowRight");
  await expect(page.getByText("Card 4 of 11")).toBeVisible();

  // The chapter page links to its deck.
  const chapterId = page.url().split("/").pop()!;
  await page.goto(`/library/chapter/${chapterId}`);
  await expect(page.getByRole("link", { name: /Flash cards/ })).toContainText("11 flash cards");

  // Two known cards wait; the one still being learnt is due again.
  await page.goto("/flashcards");
  await expect(page.getByRole("region", { name: "Physics · Class 11" }).getByRole("link", { name: /Units and Measurement/ })).toContainText("9 cards due");
});

test("staff write, paste, edit and delete flash cards", async ({ page }) => {
  await signIn(page);
  await page.goto("/admin/flashcards");
  await expect(page.getByRole("heading", { name: "Flash cards", level: 1 })).toBeVisible();
  await page.getByRole("link", { name: "7. Gravitation" }).click();
  await expect(page.getByRole("heading", { name: "7. Gravitation", level: 1 })).toBeVisible();

  const list = page.getByRole("list").filter({ has: page.getByRole("button", { name: /^Edit: / }) });
  const front = unique("Value of G");
  await page.getByLabel("Front (English)").fill(front);
  await page.getByLabel("Back (English)").fill("6.67 × 10⁻¹¹ N m² kg⁻²");
  await page.getByRole("button", { name: "Add card" }).click();
  await expect(page.getByText("Card added")).toBeVisible();
  await expect(list.getByText(front)).toBeVisible();

  // Paste many: one good line, one without a back.
  const pasted = unique("Escape velocity");
  await page.getByLabel("Cards, one per line").fill(`${pasted} | About 11.2 km/s\nA line with no back`);
  await page.getByRole("button", { name: "Add pasted cards" }).click();
  await expect(page.getByText("1 added, 1 skipped:")).toBeVisible();
  await expect(page.getByText("Line 2: Needs a front and a back, separated by | or a tab.")).toBeVisible();
  await expect(list.getByText(pasted)).toBeVisible();

  // Edit, then delete.
  await page.getByRole("button", { name: `Edit: ${front}` }).click();
  const newBack = unique("6.674 × 10⁻¹¹ N m² kg⁻²");
  await list.getByLabel("Back (English)").fill(newBack);
  await page.getByRole("button", { name: "Save" }).click();
  await expect(page.getByText("Card saved")).toBeVisible();
  await expect(list.getByText(newBack)).toBeVisible();
  await page.getByRole("button", { name: `Delete: ${pasted}` }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Delete" }).click();
  await expect(page.getByText("Card deleted")).toBeVisible();
  await expect(list.getByText(pasted)).toHaveCount(0);
});

test("the sign-in gate runs on every student area, flash cards and custom tests included", async ({ page }) => {
  // The gate only runs where its matcher says; the two lists must stay in step.
  for (const area of STUDENT_AREAS) expect(config.matcher, area).toContain(`${area}/:path*`);
  await page.goto("/flashcards");
  await page.waitForURL("**/login?next=%2Fflashcards");
  await page.goto("/tests/custom");
  await page.waitForURL("**/login?next=%2Ftests%2Fcustom");
});
