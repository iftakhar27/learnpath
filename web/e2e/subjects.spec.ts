import { expect, test } from "@playwright/test";
import { onboardStudent, registerStudent, studentSignIn } from "./helpers";

test("the subject grid leads to NCERT chapters, their lessons and chapter practice", async ({ page, request }) => {
  const s = await registerStudent(request, "Grid Student");
  await onboardStudent(request, s.token);
  await studentSignIn(page, s.email, s.password);
  await page.waitForURL("**/home");

  // Home: six books, Physics/Chemistry/Biology for Class 11 and 12.
  const grid = page.getByRole("region", { name: "Subjects" });
  await expect(grid.getByRole("link")).toHaveCount(6);
  await grid.getByRole("link", { name: /^Physics, Class 11: \d+ of 14 chapters ready$/ }).click();

  await expect(page.getByRole("heading", { name: "Physics · Class 11", level: 1 })).toBeVisible();
  await expect(page.getByRole("link", { name: /Waves/ })).toBeVisible(); // all 14 NCERT chapters, even those without lessons
  await expect(page.getByRole("link", { name: /Gravitation/ })).toContainText("Lessons coming soon");
  await expect(page.getByRole("link", { name: /Laws of Motion/ }).getByRole("img", { name: "Locked" })).toBeVisible();

  // A free chapter: videos, notes and practice from the course that teaches it.
  await page.getByRole("link", { name: /Units and Measurement/ }).click();
  await expect(page.getByRole("heading", { name: "Units and Measurement", level: 1 })).toBeVisible();
  await expect(page.getByText("Chapter 1")).toBeVisible();
  await expect(page.getByRole("tabpanel").getByRole("link", { name: /Dimensions explained/ })).toBeVisible();
  await page.getByRole("tab", { name: "Notes" }).click();
  await expect(page.getByRole("tabpanel").getByRole("button", { name: /summary notes/ })).toBeVisible();
  await page.getByRole("tab", { name: "Practice" }).click();
  await page.getByRole("link", { name: /^Practise this chapter \(\d+ questions\)$/ }).click();
  await expect(page.getByText(/^Question 1 of \d+$/).first()).toBeVisible();

  // A paid chapter explains how to unlock it.
  await page.goto("/library/11/physics");
  await page.getByRole("link", { name: /Laws of Motion/ }).click();
  await expect(page.getByText("This chapter is in a paid course. Buy it to unlock every lesson.")).toBeVisible();
  await expect(page.getByRole("link", { name: "View course" })).toBeVisible();

  // Switch books without going back.
  await page.goto("/library/11/physics");
  await page.getByRole("link", { name: "Class 12" }).click();
  await expect(page.getByRole("heading", { name: "Physics · Class 12", level: 1 })).toBeVisible();
  await page.getByRole("link", { name: "Biology", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Biology · Class 12", level: 1 })).toBeVisible();
  await expect(page.getByRole("link", { name: /Biodiversity and Conservation/ })).toBeVisible();
});
