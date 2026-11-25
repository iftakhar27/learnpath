import { expect, test } from "@playwright/test";
import { GUEST_AREAS, STUDENT_AREAS } from "../src/proxy";
import { onboardStudent, registerStudent, studentSignIn } from "./helpers";

const FREE = "NEET Physics Starter (Sample, Free)";
const CHAT = "https://wa.me/919876543210?text=Hi!%20I%20have%20a%20question%20about%20LearnPath.";

test("guests browse courses and NCERT subjects without an account, and sign in to study", async ({ page }) => {
  for (const area of GUEST_AREAS) expect(STUDENT_AREAS).not.toContain(area);

  // The landing page invites them in, with WhatsApp chat in the corner.
  await page.goto("/");
  await expect(page.getByRole("link", { name: "Chat with us on WhatsApp" })).toHaveAttribute("href", CHAT);
  await expect(page.getByRole("link", { name: "Join our WhatsApp channel" })).toHaveAttribute("href", "https://whatsapp.com/channel/0029VaExample");
  await page.getByRole("link", { name: "Explore without an account" }).click();
  await page.waitForURL("**/learn");
  await expect(page.getByRole("note")).toContainText("You're exploring as a guest");
  await expect(page.getByRole("region", { name: "Subjects" }).getByRole("link")).toHaveCount(6);

  // A course page: joining asks them to sign up and comes back; it can be shared on WhatsApp.
  await page.getByRole("link", { name: FREE }).click();
  await expect(page.getByRole("heading", { name: FREE, level: 1 })).toBeVisible();
  const courseUrl = page.url();
  const courseId = courseUrl.split("/").pop()!;
  await expect(page.getByRole("link", { name: "Sign up free to start" })).toHaveAttribute("href", `/register?next=${encodeURIComponent(`/learn/${courseId}`)}`);
  const share = await page.getByRole("link", { name: "Share on WhatsApp" }).getAttribute("href");
  expect(share).toMatch(/^https:\/\/wa\.me\/\?text=/);
  expect(decodeURIComponent(share!.split("text=")[1])).toContain(`/learn/${courseId}`);

  // Studying needs an account: a lesson sends them to sign in, then back.
  await page.getByRole("link", { name: /Dimensional Analysis/ }).first().click();
  await page.waitForURL(/\/login\?next=%2Ftopics%2F/);
  await expect(page.getByText("Your session has expired")).toHaveCount(0);

  // NCERT subject pages are public too.
  await page.goto("/library/11/physics");
  await expect(page.getByRole("heading", { name: "Physics · Class 11", level: 1 })).toBeVisible();
  await page.getByRole("link", { name: /Units and Measurement/ }).click();
  await expect(page.getByRole("heading", { name: "Units and Measurement", level: 1 })).toBeVisible();

  // The guest menu: browse, WhatsApp, settings and the way in.
  await page.getByRole("button", { name: "Open menu" }).click();
  const menu = page.getByRole("dialog", { name: "Menu" });
  await expect(menu.getByRole("link", { name: "Create free account" })).toBeVisible();
  await expect(menu.getByRole("link", { name: "Chat with us on WhatsApp" })).toHaveAttribute("href", CHAT);
  await expect(menu.getByRole("link", { name: "Invite friends on WhatsApp" })).toHaveAttribute("href", /^https:\/\/wa\.me\/\?text=/);
  await expect(menu.getByRole("switch", { name: "Night mode" })).toBeVisible();
  await menu.getByRole("link", { name: "Sign in" }).click();
  await page.waitForURL(/\/login\?next=%2Flibrary%2Fchapter%2F/);

  // Everything else still needs signing in.
  await page.goto("/home");
  await page.waitForURL(/\/login\?next=%2Fhome/);
});

test("signed-in students reach the team on WhatsApp from the menu and Help", async ({ page, request }) => {
  const s = await registerStudent(request, "WhatsApp Student");
  await onboardStudent(request, s.token);
  await studentSignIn(page, s.email, s.password);
  await page.waitForURL("**/home");
  await expect(page.getByRole("note")).toHaveCount(0); // no guest banner

  await page.getByRole("button", { name: "Open menu" }).click();
  const menu = page.getByRole("dialog", { name: "Menu" });
  await expect(menu.getByRole("link", { name: "Chat with us on WhatsApp" })).toHaveAttribute("href", CHAT);
  await expect(menu.getByRole("link", { name: "Join our WhatsApp channel" })).toBeVisible();
  await page.keyboard.press("Escape");

  await page.goto("/support");
  await expect(page.getByRole("link", { name: "Chat with us on WhatsApp" })).toHaveAttribute("href", CHAT);
});
