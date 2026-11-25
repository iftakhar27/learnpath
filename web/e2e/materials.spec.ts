import { expect, test } from "@playwright/test";
import { onboardStudent, registerStudent, signIn, studentSignIn, unique } from "./helpers";

/** A tiny but valid one-page PDF. */
function pdf(text: string) {
  const stream = `BT /F1 12 Tf 50 750 Td (${text}) Tj ET\n`;
  const objects = [
    "<< /Type /Catalog /Pages 2 0 R >>",
    "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
    "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
    "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
    `<< /Length ${stream.length} >>\nstream\n${stream}endstream`,
  ];
  let out = "%PDF-1.4\n";
  const offsets: number[] = [];
  objects.forEach((o, i) => {
    offsets.push(out.length);
    out += `${i + 1} 0 obj\n${o}\nendobj\n`;
  });
  const xref = out.length;
  out += `xref\n0 ${objects.length + 1}\n0000000000 65535 f \n${offsets.map((o) => `${String(o).padStart(10, "0")} 00000 n \n`).join("")}`;
  out += `trailer\n<< /Size ${objects.length + 1} /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF\n`;
  return Buffer.from(out, "latin1");
}

test("staff upload material; students download it; guests are asked to sign up", async ({ browser, page, request }) => {
  const title = unique("Optics formula sheet");

  // Staff: add a PDF for Physics, Class 12.
  await signIn(page);
  await page.goto("/admin/materials");
  await page.getByRole("button", { name: "Add material" }).first().click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Title (English)").fill(title);
  await dialog.getByLabel("Type").selectOption("FormulaSheet");
  await dialog.getByLabel("Subject").selectOption("Physics");
  await dialog.getByLabel("Class").selectOption("12");
  await dialog.getByLabel("File", { exact: true }).setInputFiles({ name: "optics.pdf", mimeType: "application/pdf", buffer: pdf("Lens formula: 1/f = 1/v - 1/u") });
  await dialog.getByRole("button", { name: "Add material" }).click();
  await expect(page.getByText("Material added")).toBeVisible();
  const row = page.getByRole("row").filter({ hasText: title });
  await expect(row).toContainText("Formula sheet");
  await expect(row).toContainText("Physics · Class 12");
  await expect(row).toContainText(/PDF · \d+ KB/);

  // Something that isn't a PDF or image is refused with a reason.
  await page.getByRole("button", { name: "Add material" }).first().click();
  await dialog.getByLabel("Title (English)").fill(unique("Not a PDF"));
  await dialog.getByLabel("File", { exact: true }).setInputFiles({ name: "fake.pdf", mimeType: "application/pdf", buffer: Buffer.from("<html>nope</html>") });
  await dialog.getByRole("button", { name: "Add material" }).click();
  await expect(dialog.getByText("Upload a PDF, PNG or JPEG file.")).toBeVisible();
  await dialog.getByRole("button", { name: "Cancel" }).click();

  // A student finds it by subject and downloads it (a signed link that opens the PDF).
  const s = await registerStudent(request, "Material Student");
  await onboardStudent(request, s.token);
  const studentContext = await browser.newContext();
  const student = await studentContext.newPage();
  await studentSignIn(student, s.email, s.password);
  await student.waitForURL("**/home");
  await student.getByRole("button", { name: "Open menu" }).click();
  await student.getByRole("dialog", { name: "Menu" }).getByRole("link", { name: "Free study material" }).click();
  await expect(student.getByRole("heading", { name: "Free study material", level: 1 })).toBeVisible();
  await student.getByRole("group", { name: "All subjects" }).getByRole("button", { name: "Physics" }).click();
  const card = student.getByRole("listitem").filter({ hasText: title });
  await expect(card).toContainText("Physics");
  await expect(card).toContainText("Class 12");
  const file = studentContext.waitForEvent("response", (r) => r.url().includes("/file?expires="));
  await card.getByRole("button", { name: `Download: ${title}` }).click();
  const response = await file;
  expect(response.status()).toBe(200);
  expect(response.headers()["content-type"]).toBe("application/pdf");
  expect(response.headers()["content-disposition"]).toContain("optics.pdf");
  await studentContext.close();

  // Guests see the list and are asked to sign up to download.
  const guestContext = await browser.newContext();
  const guest = await guestContext.newPage();
  await guest.goto("/materials");
  await expect(guest.getByRole("note")).toContainText("You're exploring as a guest");
  await expect(guest.getByRole("listitem").filter({ hasText: title }).getByRole("link", { name: "Sign up free to download" })).toHaveAttribute("href", "/register?next=%2Fmaterials");
  await guestContext.close();

  // The download was counted; then staff delete it.
  await page.reload();
  await expect(page.getByRole("row").filter({ hasText: title })).toContainText("1");
  await page.getByRole("button", { name: `Delete: ${title}` }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Delete" }).click();
  await expect(page.getByText("Material deleted")).toBeVisible();
  await expect(page.getByRole("row").filter({ hasText: title })).toHaveCount(0);
});
