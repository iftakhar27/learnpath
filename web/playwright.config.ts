import { defineConfig } from "@playwright/test";

/**
 * End-to-end tests for the admin portal. They start a real API (against a separate e2e database) and the
 * production Next.js build, so run `npm run build` first. Existing servers on these ports are reused.
 */
export const API_URL = process.env.E2E_API_URL ?? "http://localhost:5180";
const WEB_URL = process.env.E2E_WEB_URL ?? "http://localhost:3100";
const DB = process.env.E2E_DATABASE ?? "Host=localhost;Port=5432;Database=learnpath_e2e;Username=postgres;Password=postgres";

export default defineConfig({
  testDir: "./e2e",
  workers: 1,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: [["list"]],
  use: { baseURL: WEB_URL, trace: "retain-on-failure", viewport: { width: 1366, height: 900 } },
  webServer: [
    {
      command: `dotnet run --project ../src/LearnPath.Api --no-launch-profile --urls ${API_URL}`,
      url: `${API_URL}/health`,
      env: { ASPNETCORE_ENVIRONMENT: "Development", ConnectionStrings__Default: DB, RateLimit__AuthPerMinute: "1000" },
      reuseExistingServer: true,
      timeout: 180_000,
    },
    {
      command: `npx next start -p ${new URL(WEB_URL).port}`,
      url: `${WEB_URL}/admin/login`,
      env: { API_BASE_URL: API_URL },
      reuseExistingServer: true,
      timeout: 60_000,
    },
  ],
});
