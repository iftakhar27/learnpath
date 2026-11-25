import { expect, test, type Page } from "@playwright/test";
import { onboardStudent, registerStudent, studentSignIn } from "./helpers";

const background = (page: Page) => page.evaluate(() => getComputedStyle(document.body).backgroundColor);
const LIGHT_BG = "rgb(247, 247, 251)";
const DARK_BG = "rgb(15, 16, 24)";

test("the side menu reaches every section, and night mode is the student's choice", async ({ page, request }) => {
  const s = await registerStudent(request, "Menu Student");
  await onboardStudent(request, s.token);
  await studentSignIn(page, s.email, s.password);
  await page.waitForURL("**/home");
  const html = page.locator("html");
  await expect(html).not.toHaveAttribute("data-theme");
  expect(await background(page)).toBe(LIGHT_BG);

  // The menu shows who is signed in, marks where they are, and closes once a page is chosen.
  const openMenu = page.getByRole("button", { name: "Open menu" });
  const menu = page.getByRole("dialog", { name: "Menu" });
  await openMenu.click();
  await expect(menu).toBeVisible();
  await expect(menu.getByText("Menu Student")).toBeVisible();
  await expect(menu.getByText(/^Preparing for /)).toBeVisible();
  await expect(menu.getByRole("link", { name: "Home" })).toHaveAttribute("aria-current", "page");
  for (const name of ["Subjects & courses", "Practice", "Tests", "Study plan", "My progress", "Ask a doubt", "Notifications", "My purchases", "Help & support"])
    await expect(menu.getByRole("link", { name, exact: true })).toBeVisible();
  await menu.getByRole("link", { name: "Study plan" }).click();
  await page.waitForURL("**/plan");
  await expect(menu).toBeHidden();

  // Night mode from the menu switches the whole site at once.
  await openMenu.click();
  const night = menu.getByRole("switch", { name: "Night mode" });
  await expect(night).toHaveAttribute("aria-checked", "false");
  await night.click();
  await expect(html).toHaveAttribute("data-theme", "dark");
  await expect(night).toHaveAttribute("aria-checked", "true");
  expect(await background(page)).toBe(DARK_BG);
  await page.keyboard.press("Escape");
  await expect(menu).toBeHidden();

  // Remembered in this browser and applied before the page paints.
  await page.reload();
  await expect(html).toHaveAttribute("data-theme", "dark");
  expect(await background(page)).toBe(DARK_BG);

  // Profile offers Auto / Light / Dark; Auto follows the device.
  await page.goto("/profile");
  const appearance = page.getByRole("group", { name: "Appearance" });
  await expect(appearance.getByRole("button", { name: "Dark" })).toHaveAttribute("aria-pressed", "true");
  await appearance.getByRole("button", { name: "Auto" }).click();
  await expect(html).not.toHaveAttribute("data-theme");
  expect(await background(page)).toBe(LIGHT_BG);
  await page.emulateMedia({ colorScheme: "dark" });
  expect(await background(page)).toBe(DARK_BG);
  await appearance.getByRole("button", { name: "Light" }).click();
  await expect(html).toHaveAttribute("data-theme", "light");
  expect(await background(page), "light wins over a dark device").toBe(LIGHT_BG);

  // Signing out from the menu.
  await openMenu.click();
  await menu.getByRole("button", { name: "Sign out" }).click();
  await page.waitForURL("**/login");
});
