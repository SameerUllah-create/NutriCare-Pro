# NutriCare Pro

Nutrition consultation application with an ASP.NET Core Web API, Entity Framework Core/SQL Server, and a Next.js TypeScript portal. The current implementation targets .NET 10 and supports a single nutritionist at launch with English/Urdu and Pakistan time.

Implementation is in progress against the 23-step SRS backlog. Passing tests do not constitute launch approval. See the evidence and remaining gates in [Implementation status](docs/IMPLEMENTATION_STATUS.md).

## Start here

- [Development setup](docs/DEVELOPMENT_SETUP.md): build, SQL configuration, local file/email substitutes, account provisioning and tests.
- [Implementation status](docs/IMPLEMENTATION_STATUS.md): saved workflows, verification evidence and remaining work by step.
- [Implementation backlog](docs/IMPLEMENTATION_BACKLOG.md): original 23-step sequence and acceptance criteria.
- [SRS gap analysis](docs/SRS_GAP_ANALYSIS.md): original missing features and defects.
- [Project understanding](docs/PROJECT_UNDERSTANDING.md): architecture audit; historical observations may be superseded by the implementation status.
- [Operations runbook](docs/OPERATIONS_RUNBOOK.md): migration, recovery, deployment and launch gates.
- [Integration implementation](docs/INTEGRATIONS_IMPLEMENTATION.md): provider behavior, tests and external limitations.
- [Package configuration](docs/PACKAGE_CONFIGURATION.md): proposed benefits, eligibility, expiry and nutritionist review.
- [Package credits](docs/PACKAGE_CREDITS.md): reservation/consumption safeguards and audited validity extensions.
- [Patient data export](docs/PATIENT_DATA_EXPORT.md): private-file bundles, ownership and resource limits.

## Run locally

Prerequisites: .NET 10 SDK, Node.js 22+, SQL Server and a local login allowed to create the isolated development database.

```powershell
dotnet restore tests/NutriCare.Tests/NutriCare.Tests.csproj
dotnet build tests/NutriCare.Tests/NutriCare.Tests.csproj -m:1 --no-restore
dotnet run --project src/backend/NutriCare.API
```

The Development profile uses `NutriCareImplementationDev` on localhost, migrates that database, and seeds proposed configuration as drafts. It does not reset the historical `NutriCarePro` database. API: `http://localhost:7001`; Swagger: `/swagger`; readiness: `/api/health`.

In another terminal:

```powershell
Set-Location frontend/nutricare-pro-portal
npm ci
npm run dev
```

Portal: `http://localhost:3000`. Runtime server configuration `API_BASE_URL` selects the upstream API. Authentication uses the portal's same-origin backend and HttpOnly cookies. No administrator, nutritionist, clinical credentials or password is fabricated; follow the controlled provisioning instructions in the setup guide.

## Draft configuration

The 10 services, 3 packages, 6 working-hour rules and cancellation policy requested by the owner are seeded as unpublished drafts. Nothing becomes bookable or payable from these proposals until a staff member explicitly reviews and publishes the relevant versions and effective dates. Published consent text is required for dependent health-data collection and online consultations. Restarting preserves edits and never approves configuration.

Package proposals now include the supplied consultation/diet-plan allowances and expiry rules. A fresh database receives three full package drafts. Upgrading an earlier v1 seed preserves its existing package rows and adds three separate proposal revisions, so that database can contain six drafts. Only a verified nutritionist can approve the exact package content version; approval remains separate from publication. Package checkout and public sales remain disabled.

## Verification

```powershell
dotnet test tests/NutriCare.Tests/NutriCare.Tests.csproj --no-build --filter 'Category!=SqlIntegration'
dotnet test tests/NutriCare.Tests/NutriCare.Tests.csproj --no-build --filter 'Category=SqlIntegration'
```

SQL tests generate and clean up only validated `NutriCareTest_<guid>` databases. See setup for `NUTRICARE_TEST_SQL_CONNECTION`, the opt-in backup/restore rehearsal, and the local PDF browser. In the portal run `npm run lint`, `npm run typecheck`, `npm run build`, and `npm run test:e2e`.

The GitHub Actions workflow verifies builds/tests and does not deploy automatically. Docker Compose describes a separate local development environment with isolated volumes and mandatory private secrets. Container execution still needs validation on a Docker-enabled machine.

## Source layout

- `src/backend/NutriCare.Domain`: entities and shared service contracts.
- `src/backend/NutriCare.Application`: commands, queries, validation and clinical workflows.
- `src/backend/NutriCare.Persistence`: EF model, tracked migrations, repositories and configuration storage.
- `src/backend/NutriCare.Infrastructure`: sessions, notifications, private storage, payment/video adapters and local PDF rendering.
- `src/backend/NutriCare.API`: controllers, authorization, hubs, workers and startup.
- `frontend/nutricare-pro-portal`: public site and role-aware portals.
- `tests/NutriCare.Tests`: regression, SQL and HTTP tests.

The separate `backend` directory and old numbered SQL scripts are historical designs. They are not the active migration system. Existing databases created from those designs need a reviewed adoption migration; never apply a fresh-schema baseline blindly to populated data.
