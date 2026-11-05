import { readFileSync } from "node:fs";
import { join } from "node:path";
import { expect, test, type APIRequestContext } from "@playwright/test";
import { API_URL } from "../playwright.config";
import { ADMIN, apiGet, apiLogin, apiPost, onboardStudent, registerStudent, studentSignIn, unique } from "./helpers";

const FREE = "NEET Physics Starter (Sample, Free)";
const PAID = "NEET Physics: Mechanics (Sample)";

async function signedInStudent(page: import("@playwright/test").Page, request: APIRequestContext, name = "Learn Student") {
  const s = await registerStudent(request, name);
  await onboardStudent(request, s.token);
  await studentSignIn(page, s.email, s.password);
  await page.waitForURL("**/home");
  return s;
}

test("catalog filters by price and search, and a free course can be joined and studied", async ({ page, request }) => {
  await signedInStudent(page, request);
  await page.getByRole("navigation", { name: "Main" }).first().getByRole("link", { name: "Learn" }).click();
  await expect(page.getByRole("link", { name: new RegExp(FREE.replace(/[()]/g, "\\$&")) })).toBeVisible();
  await expect(page.getByRole("link", { name: new RegExp(PAID.replace(/[()]/g, "\\$&")) })).toBeVisible();

  await page.getByRole("radio", { name: "Free" }).click();
  await expect(page.getByRole("link", { name: /Mechanics \(Sample\)/ })).toHaveCount(0);
  await page.getByRole("radio", { name: "All" }).click();
  await page.getByRole("searchbox", { name: "Search" }).fill("Mechanics");
  await expect(page.getByRole("link", { name: /Starter/ })).toHaveCount(0);
  await page.getByRole("searchbox", { name: "Search" }).fill("");

  await page.getByRole("link", { name: /Starter/ }).click();
  await page.getByRole("button", { name: "Enroll for free" }).click();
  await expect(page.getByText("You have access to this course")).toBeVisible();
  await expect(page.getByRole("heading", { name: "Syllabus" })).toBeVisible();

  // Topic: notes open in a new tab and count as done.
  await page.getByRole("link", { name: /Dimensional Analysis/ }).first().click();
  await expect(page.getByRole("heading", { name: "Dimensional Analysis", level: 1 })).toBeVisible();
  const notes = page.getByRole("button", { name: /summary notes/ });
  const popup = page.waitForEvent("popup");
  await notes.click();
  await (await popup).close();
  await expect(notes).toContainText("Completed");

  // Save for revision.
  await page.getByRole("button", { name: "Save for revision" }).click();
  await expect(page.getByRole("button", { name: "Saved for revision" })).toBeVisible();

  // Home now lists the course with its progress.
  await page.getByRole("link", { name: "Home" }).first().click();
  await expect(page.getByRole("link", { name: new RegExp(`${FREE.replace(/[()]/g, "\\$&")}.*% complete`) })).toBeVisible();
});

test("paid course: locked lessons explain how to unlock; buying on a test server explains simulated payments", async ({ page, request }) => {
  await signedInStudent(page, request);
  await page.goto("/learn");
  await page.getByRole("link", { name: /Mechanics \(Sample\)/ }).click();
  await expect(page.getByText(/days access|Lifetime access/).filter({ visible: true })).toBeVisible();
  await page.getByRole("button", { name: /^Buy for ₹/ }).click();
  await expect(page.getByRole("status").filter({ hasText: "Payments are simulated on this test server" })).toBeVisible();

  await page.getByText("Friction").first().click();
  await expect(page.getByText("Buy the course to unlock this lesson.").first()).toBeVisible();
  await expect(page.getByText("Locked").first()).toBeVisible();
});

test("a video lesson resumes where the student stopped and completes at the end", async ({ page, request }) => {
  // A short lesson served from a local file, so the player can be tested without the video CDN.
  const admin = await apiLogin(request, ADMIN.email, ADMIN.password);
  const courses = await apiGet(request, "/api/v1/courses?free=true", admin);
  const free = courses.items.find((c: { title: string }) => c.title === FREE);
  const syllabus = await apiGet(request, `/api/v1/courses/${free.id}/syllabus`, admin);
  const topicId = syllabus.subjects[0].chapters[0].topics[0].id;
  const title = unique("Lab demo");
  const created = await apiPost(request, "/api/v1/admin/resources", admin, {
    topicId,
    type: "Video",
    titleEn: title,
    language: "en",
    sortOrder: 99,
    isFreePreview: false,
    videoProviderId: "https://video.test/sample.webm",
    durationSeconds: 20,
    allowDownload: false,
  });
  await apiPost(request, `/api/v1/admin/resources/${created.id}/status`, admin, { status: "Published" });
  const webm = readFileSync(join(__dirname, "fixtures", "sample.webm"));
  // Browsers only seek in media served with byte ranges, as a CDN does.
  await page.route("https://video.test/**", (route) => {
    const range = /bytes=(\d+)-(\d*)/.exec(route.request().headers()["range"] ?? "");
    if (!range) return route.fulfill({ status: 200, contentType: "video/webm", headers: { "accept-ranges": "bytes" }, body: webm });
    const start = Number(range[1]);
    const end = range[2] ? Math.min(Number(range[2]), webm.length - 1) : webm.length - 1;
    return route.fulfill({
      status: 206,
      contentType: "video/webm",
      headers: { "accept-ranges": "bytes", "content-range": `bytes ${start}-${end}/${webm.length}` },
      body: webm.subarray(start, end + 1),
    });
  });

  const s = await signedInStudent(page, request, "Video Student");
  await apiPost(request, `/api/v1/courses/${free.id}/enroll`, s.token);
  await page.goto(`/watch/${created.id}`);
  await expect(page.getByRole("heading", { name: title })).toBeVisible();
  const video = page.locator("video");
  await video.evaluate((v: HTMLVideoElement) => new Promise((r) => (v.readyState >= 1 ? r(null) : v.addEventListener("loadedmetadata", () => r(null), { once: true }))));
  await video.evaluate((v: HTMLVideoElement) => {
    v.muted = true;
    v.currentTime = 7;
    return v.play();
  });
  await page.waitForTimeout(1500);
  await video.evaluate((v: HTMLVideoElement) => v.pause());
  await expect.poll(async () => (await apiGet(request, `/api/v1/resources/${created.id}/playback`, s.token)).resumeAtSeconds).toBeGreaterThanOrEqual(7);

  await page.reload();
  await expect(page.getByText(/Resumed from 0:0[7-9]/)).toBeVisible();

  await video.evaluate((v: HTMLVideoElement) => {
    v.muted = true;
    v.currentTime = 18.5;
    return v.play();
  });
  await expect(page.getByText("Completed")).toBeVisible({ timeout: 15_000 });
  void API_URL;
});
