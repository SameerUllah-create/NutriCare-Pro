# Development and verification

Use .NET SDK 10 (selected by root `global.json`), Node.js 22 or later, and SQL Server. Run commands from the repository root unless another directory is stated. No live provider credentials are needed for local configuration and portal development.

## Database safety

The active model is `src/backend/NutriCare.Persistence/Contexts/NutriCareDbContext.cs`. Tracked EF migrations are the upgrade mechanism. Historical numbered `.sql` files describe earlier implementations and are not executed by startup. Do not apply the new fresh-schema baseline directly to a populated database created by `EnsureCreated` or the historical scripts.

Development uses the separate database `NutriCareImplementationDev` on `localhost` with Windows authentication. Override `ConnectionStrings__DefaultConnection` for your environment. `appsettings.Development.json` explicitly enables migration and proposed-draft seeding for this development database; production defaults to neither. Never point these development startup flags at an existing production database.

For a populated old database: take a backup; restore to an isolated rehearsal database; inventory its actual tables, columns, constraints, user/profile relationships and data; compare with the generated baseline; prepare and review additive/backfill migrations and a baseline-history adoption script for that exact schema. Validate row counts, clinical relationships, financial balances and sign-in before adopting. This reconciliation is intentionally not automated against unknown existing data.

## Build and start

```powershell
dotnet restore tests/NutriCare.Tests/NutriCare.Tests.csproj
dotnet build tests/NutriCare.Tests/NutriCare.Tests.csproj -m:1 --no-restore
dotnet tool install --tool-path .tools dotnet-ef --version 10.0.12
.\.tools\dotnet-ef.exe database update --project src/backend/NutriCare.Persistence --startup-project src/backend/NutriCare.API
dotnet run --project src/backend/NutriCare.API
```

API: `http://localhost:7001`, Swagger: `/swagger`, health: `/api/health`.

```powershell
Set-Location frontend/nutricare-pro-portal
npm ci
npm run dev
```

Portal: `http://localhost:3000`. The server-side `API_BASE_URL` defaults to `http://localhost:7001`; it can be changed at runtime. Browser requests use the portal's same-origin proxy and HttpOnly session cookies. Do not put access/refresh tokens in local storage or a public environment variable.

Development verification/reset emails are private `.eml` files under the API's `.local/mail` directory. Links are not printed in logs. Local private file storage uses the API's `.local/private-storage`; no static file server exposes this directory. Production requires configured email and private storage providers.

## Initial administrator and clinician

There is no seeded administrator password or invented nutritionist. For a fresh database, opt into `Bootstrap__Enabled=true`, `Bootstrap__AdminEmail` and `Bootstrap__AdminPassword` through secure local environment configuration. Choose a unique password meeting Identity rules. The one-time bootstrap refuses to promote an existing account, creates an unconfirmed administrator, and sends an email verification link. Disable bootstrap and remove its password configuration after provisioning. Use resend verification if initial delivery was unavailable.

The administrator provisions the single clinician with professional identity supplied by the owner. The account must verify its email; professional verification separately requires explicit approval, evidence reference and a current row version. Draft configuration can then be assigned to the verified clinician and reviewed before publication.

## Tests

```powershell
dotnet test tests/NutriCare.Tests/NutriCare.Tests.csproj --no-build --filter 'Category!=SqlIntegration'
dotnet test tests/NutriCare.Tests/NutriCare.Tests.csproj --no-build --filter 'Category=SqlIntegration'
```

SQL integration tests use `NUTRICARE_TEST_SQL_CONNECTION`, defaulting locally to Windows authentication on localhost. Each fixture generates a separate `NutriCareTest_<guid>` database, applies EF migrations and validates its exact generated name before cleanup. The login needs permission to create/drop these disposable databases. Tests never reset the configured application database.

For the optional recovery rehearsal, set `NUTRICARE_TEST_BACKUP_DIRECTORY` to a private directory writable by the SQL Server service, then run the SQL tests. The test creates a checksummed backup, verifies and restores it to another validated disposable database, applies the additive upgrade and checks the preserved draft records. It cleans up its backup and generated databases. The local Windows rehearsal passed; CI skips this case unless its SQL container has a corresponding mounted backup directory.

The verified 17 October run passed 206 tests without skips with both the PDF browser and recovery directory configured. All three migrations are tracked, including `20261016143850_ClinicalCommercialWorkflows`. The rehearsal preserves an older edited package price and creates the new package terms as separate drafts. A v1 development seed therefore contains six package rows after upgrading/seeding; a fresh seed contains three. Neither path publishes or approves them.

```powershell
dotnet run --project tools/BrowserInstaller -- install chromium
```

For local PDF tests set `NUTRICARE_PDF_BROWSER` to the installed Chromium executable. API PDF rendering uses `ClinicalPdf__ExecutablePath` or Playwright's `PLAYWRIGHT_BROWSERS_PATH`. Linux requires Chromium system dependencies and an Urdu-capable font, such as Noto fonts. PDFs render locally in an offline browser context.

In the portal directory run `npm run lint`, `npm run typecheck`, `npm run build`, `npx playwright install chromium`, then `npm run test:e2e`. Browser contract tests use an isolated test upstream; those tests complement, and do not replace, full-stack journeys against SQL and provider sandboxes.

Production needs a strong secret JWT key, persisted Data Protection keys, TLS and explicitly configured trusted reverse proxies, private storage, approved consent and business settings, real notification/payment/video providers, and backup/recovery monitoring. Do not enable provider development substitutes in production.

Package review, credit accounting and extension contracts are documented in [PACKAGE_CONFIGURATION.md](PACKAGE_CONFIGURATION.md) and [PACKAGE_CREDITS.md](PACKAGE_CREDITS.md). The private-file export route is `GET /api/privacy/export/bundle`; bounded temporary ZIPs complete before response headers, and missing/oversized files require assisted export rather than silently incomplete downloads. See [PATIENT_DATA_EXPORT.md](PATIENT_DATA_EXPORT.md).
