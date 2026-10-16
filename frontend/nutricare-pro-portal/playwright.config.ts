import { defineConfig, devices } from "@playwright/test";
export default defineConfig({
  testDir: "./tests/e2e", workers: 1, fullyParallel: false, forbidOnly: !!process.env.CI, retries: process.env.CI ? 1 : 0,
  reporter: [["list"], ["html", { open: "never" }]], use: { baseURL: "http://127.0.0.1:3100", trace: "retain-on-failure" },
  webServer: [
    { command: "node tests/mock-api.mjs", url: "http://127.0.0.1:7111/health", reuseExistingServer: false, timeout: 30000 },
    { command: "npm run start -- --hostname 127.0.0.1 --port 3100", url: "http://127.0.0.1:3100/login", env: { API_BASE_URL: "http://127.0.0.1:7111" }, reuseExistingServer: false, timeout: 60000 }
  ],
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }, { name: "mobile", use: { ...devices["iPhone 13"], defaultBrowserType: "chromium" } }]
});
