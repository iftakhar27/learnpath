import { expect, test } from "@playwright/test";
import { onboardStudent, registerStudent, studentSignIn } from "./helpers";

const ADMISSION = `https://wa.me/919876543210?text=${encodeURIComponent("Hi! I'd like to know about admission to “Class 8 Foundation”.")}`;

test("the brochure courses are listed by class with their fees, and can't be bought before classes start", async ({ page, request }) => {
  // Guests see the line-up, Class 8 first, each marked "Classes start soon".
  await page.goto("/");
  await page.getByRole("link", { name: "Explore without an account" }).click();
  await page.waitForURL("**/learn");
  const card = page.getByRole("link", { name: /Class 8 Foundation/ });
  await expect(card).toBeVisible();
  const titles = await page.getByRole("list").getByRole("link").locator("span.text-lg").allTextContents();
  const ours = titles.filter((t) => /^(Class \d+ |NEET Dropper|JEE Dropper)/.test(t));
  expect(ours).toEqual([
    "Class 8 Foundation",
    "Class 9 Foundation",
    "Class 10 Foundation + Board",
    "Class 11 JEE",
    "Class 11 NEET",
    "Class 12 JEE",
    "Class 12 NEET",
    "JEE Dropper Batch",
    "NEET Dropper Batch",
  ]);
  await expect(card).toContainText("Classes start soon");
  await expect(card).toContainText("₹999");

  // The course page: description on separate lines, fees, the syllabus, and a WhatsApp enquiry instead of Buy.
  await card.click();
  await expect(page.getByRole("heading", { name: "Class 8 Foundation", level: 1 })).toBeVisible();
  await expect(page.getByText("Goal: strong basics + Olympiad / ITSE preparation.", { exact: false }).filter({ visible: true })).toBeVisible();
  await expect(page.getByText("Classes start soon", { exact: true }).filter({ visible: true })).toBeVisible();
  await expect(page.getByText("₹999 · 30 days access").filter({ visible: true })).toBeVisible();
  await expect(page.getByRole("link", { name: "Ask about admission on WhatsApp" })).toHaveAttribute("href", ADMISSION);
  await expect(page.getByRole("link", { name: /^Buy for/ })).toHaveCount(0);
  const science = page.locator("div.rounded-2xl", { has: page.getByRole("heading", { name: "Science", level: 3 }) });
  await expect(science.getByText("Crop Production and Management")).toBeVisible();
  await expect(science.getByText("Lessons coming soon")).toHaveCount(7);
  const courseUrl = new URL(page.url()).pathname;

  // Signed in: the same, with no Buy button even when checkout is asked for.
  const s = await registerStudent(request, "Brochure Student");
  await onboardStudent(request, s.token);
  await studentSignIn(page, s.email, s.password);
  await page.waitForURL("**/home");
  await page.goto(`${courseUrl}?buy=1`);
  await expect(page.getByText("Classes start soon", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: /^Buy for/ })).toHaveCount(0);
  await expect(page.getByRole("dialog")).toHaveCount(0);
});
