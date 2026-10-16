# Portal implementation

Updated 17 October 2026. The application is in `frontend/nutricare-pro-portal`.

## Implemented behavior

- Patient registration, verification, password recovery, login/logout and session expiry use same-origin Next.js account routes. Upstream access/refresh tokens remain in HttpOnly, SameSite Strict cookies. Browser code does not request pasted tokens or store them in local storage. Account and proxy mutations validate the request origin. Return URLs accept local paths only.
- Protected portal layouts obtain the account and role from the API. API authorization remains authoritative. The browser proxy forwards protected API requests, file downloads and idempotency keys, and coordinates refresh calls in one server process.
- Patient, nutritionist and administrator views cover booking, appointments, intake, plans, progress, reports, notifications, payments, messaging, video access, profile, feedback, privacy and operational administration. Provider availability and server permission checks determine which actions succeed.
- Configuration covers services, packages, working hours, calendar exceptions, cancellation rules and bilingual policy text. Saving a draft does not publish or activate it. Publication requires a separate approval checkbox, effective dates and server validation. Updates send row/version information; conflicts preserve the displayed form and require reload. Published rows have separate **View / manage** and **Create revision** actions. Their fields and Save button are disabled; management permits a reasoned, version-checked unpublish after a reload. Audit history remains visible.
- Administrators explicitly select a professionally verified, active clinician with a confirmed email before assigning service/calendar configuration. Unassigned seeded drafts do not silently select a clinician. Nutritionist ownership is assigned and enforced by the server. Working hours serialize as `HH:mm:ss`; local dates use Pakistan time. New prices use PKR and cent precision.
- Package configuration edits authoritative days, consultation quantities/durations, diet-plan/revision quantities, tracking frequency, expiry/first-visit windows, eligible care-category arrays, activation and cancellation-credit rules. Weeks are derived by the server. Review identity, date and content version are displayed without editable form fields. Only a verified nutritionist can explicitly review the saved version; unsaved edits block review/publication, and saved edits or revisions require a new review. Publication remains separate and checkout remains disabled. Service package-category tags and unconfirmed cancellation credit decisions are configurable.
- Administrators can select an existing paid package enrollment, inspect its usage/history and extend validity by an explicit positive day count and reason. The request includes the selected enrollment's row version. A conflict preserves the reason and requires loading the current expiry before retrying. Nutritionist configuration does not query enrollment billing. Empty lists show the actual empty state; no enrollment or purchase is invented.
- Booking displays the currently published health/video consent text and sends its exact version plus the displayed `en`/`ur` language. Appointment details render the cancellation policy snapshot saved with that booking. Historical appointments with a missing price cannot initiate checkout.
- English/Urdu switching persists in a language cookie, sets HTML language/direction and formats dates in `Asia/Karachi`. RTL layout, core navigation and form labels are present. Public content and policies use approved English/Urdu fields from the API. An Urdu-speaking reviewer must still approve translations and professional wording before launch.
- Public pages load published data. An empty catalog or missing approved content remains empty; prices, testimonials and credentials are not invented.
- The patient privacy page offers JSON export and a separate ZIP export containing available records and report files. ZIP uses the authenticated same-origin proxy without a patient ID parameter. The browser shows server errors, including a safe assisted-export response, instead of downloading error JSON as a ZIP.
- Tailwind and shadcn-style button/input foundations coexist with the existing application styles. The container uses Node 22 and standalone Next.js output. Build workers and browser tests are limited to one on low-memory machines.

## Verification

Verified on 17 October 2026, including package entitlement/review and Admin validity screens:

- `npm run lint`: passed with zero warnings.
- `npm run typecheck`: passed.
- `npm run build`: passed on Next.js 15.5.28, including lint/type validation, 44-page generation and standalone traces. The final build uses the corrected PostCSS 8.5.30 dependency graph.
- `npm run test:e2e -- --workers=1`: **34 passed, zero skipped** on the final production build: 17 desktop and 17 mobile viewport cases. Playwright reports a duration of 1.1 minutes.
- `npm ls postcss --all`: passed; Next, Tailwind and Autoprefixer all resolve 8.5.30.
- Production dependency audit: zero vulnerabilities. The full development audit has the outstanding findings described below.

The build emits an Edge-runtime/static-generation warning. The harness runs production `next start` from the full workspace and emits a warning about standalone output; the container uses its standalone server entry point. Passing this harness does not establish container execution.

Commands, run serially from the portal directory:

```powershell
npm run lint
npm run typecheck
npm run build
npm run test:e2e -- --workers=1
```

Set server-only `API_BASE_URL` to the API address. Set `APP_ORIGIN` to the exact public origin (for example, `https://clinic.example`) in canonical deployments, particularly behind TLS termination. It has no path. When omitted, mutation-origin checks use the incoming Host header and request protocol; Next's internal URL may normalize to localhost. Arbitrary forwarded-host headers never establish a trusted origin. Configure the reverse proxy to preserve the intended Host, block direct application access and supply the correct protocol. Production cookies require HTTPS; the local browser harness uses Chromium's loopback exception.

The browser suite uses Chromium desktop and an iPhone-sized Chromium viewport. It starts a production `next start` server and a deterministic test API. The fixtures are isolated under `tests/`; they are never imported by application code. The suite covers anonymous access, HttpOnly sessions, local redirects, cross-origin/forwarded-host rejection, Urdu/RTL persistence, empty published catalogs, explicit configuration approval, clinician assignment, stale-version conflicts, TimeOnly serialization, validation details, published management after reload, Urdu consent, booked-policy/legacy-price behavior and ZIP export/error feedback. Package cases verify draft IDs/benefit summaries, quantity/category-array serialization, protected review fields, explicit nutritionist review, separate publication, revision invalidation, stale review, nutritionist exclusion from billing, and reasoned Admin validity extension with stale-version reload and audit feedback. Checkout remains disabled throughout.

## Limits of this evidence

The dependency graph applies the compatible PostCSS 8.5.30 override to every consumer, including Next.js. `npm ls postcss --all` verifies a single patched version. On 17 October 2026, a fresh `npm audit --omit=dev --json` reported **zero production dependency vulnerabilities**. The complete audit still reports **five high findings** through the development-only `eslint-config-next → @next/eslint-plugin-next → fast-glob → micromatch → braces` chain. These are propagated findings from one upstream issue; the [reviewed braces advisory](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm) lists no patched version. The registry check on 16 October found 3.0.3 as latest; the 17 October audit still proposes a tooling downgrade. Upstream remediation or a reviewed tooling replacement remains outstanding. No forced framework/tooling downgrade was applied. PostCSS source-map issues are fixed in newer releases; see the [PostCSS advisory](https://github.com/advisories/GHSA-fxqj-rqcc-2cmp).

Browser contract tests use deterministic fixtures. They do not replace the SQL/API integration suite or real provider sandbox acceptance. Reconnect behavior between real users, live payments/refunds, notification delivery, video provider configuration, approved content/consent, full accessibility review and complete bilingual editorial review remain release acceptance work. Desktop/mobile Chromium evidence does not establish Safari, Firefox or assistive-technology compatibility. Container source has been updated; container execution must be verified on a machine with Docker.
