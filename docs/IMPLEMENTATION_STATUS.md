# Implementation status

Updated 17 October 2026. This file distinguishes implemented code from verified acceptance and launch approval. The original numbered requirements remain in [IMPLEMENTATION_BACKLOG.md](IMPLEMENTATION_BACKLOG.md). A feature being present does not complete its release gate.

## Confirmed product decisions

- One nutritionist at launch; English and Urdu, with Pakistan time (`Asia/Karachi`).
- All requested services, fees, packages, working hours and cancellation rules are proposed configuration. Seeding creates inactive, unpublished drafts and a seed audit record. Restarting never approves or resets them.
- Staff explicitly approve publication. Future effective dates do not make configuration publicly available early. Existing appointments retain their booked price and cancellation-policy snapshot.
- New service/package prices use PKR with at most two decimal places. The three package proposals include the supplied session/plan/revision quotas, service categories and timing rules. Their exact content version requires review by a verified nutritionist; editing invalidates that review. Approval does not publish or enable checkout.
- Package credit accounting reserves capacity atomically, consumes it on completion, checks ownership/service/duration/expiry and applies the booked cancellation-policy credit decision. Admin validity extensions require a reason and row version and preserve audit history. Public package checkout and clinical plan-package selection remain disabled/unintegrated.
- Administrative access to clinical data defaults to denied. Operational administration does not confer a clinical relationship. Assigned clinicians and patients use the shared clinical access policy.
- Consent wording, professional credentials, legal/retention decisions and live provider eligibility have not been invented. Dependent actions remain gated.

## Delivery map

| Step | Saved implementation | Acceptance still required |
|---|---|---|
| NC-01 | .NET 10, dependency cleanup, backend tests, lint/type/production builds, 34 browser cases, verification CI and documented setup | Hosted CI execution |
| NC-02 | Patient-only public registration, revocable sessions, scoped clinical access/hubs/files/video, signed callbacks; regression checks passed | Broader full-stack role/browser and independent security evidence |
| NC-03 | Three tracked migrations, row versions, serializable locks, independent audit writes; model consistency, fresh SQL and additive backup/restore passed | Adoption of an existing populated legacy database; production database/file recovery |
| NC-04 | Single clinician, Urdu, PKT and draft-only business settings recorded | Owner-approved policies, credentials, providers, retention, recovery targets |
| NC-05 | Auth forms, HttpOnly browser sessions, profile routes, clinician provisioning and explicit verification; seven SQL HTTP auth tests passed | Complete onboarding browser acceptance and privileged MFA decision |
| NC-06 | Services/package benefits, nutritionist review, revisions, effective dates, history, credit ledger and audited extensions; SQL/HTTP and browser checks passed | Nutritionist business approval and package checkout/fulfillment |
| NC-07 | Recurring hours, exceptions, slots, buffers and timezone validation; SQL reservation and calendar-conflict tests passed | Full-stack calendar acceptance and broader supported-timezone review |
| NC-08 | Versioned consent, intake, assignments, dated notes, private-file ZIP export and privacy requests; unit/HTTP export tests passed | Approved retention/deletion implementation and assisted-export operations |
| NC-09 | Booking idempotency, SQL locks, price/policy/buffer snapshots, package credits, state history and durable cancellation-refund instructions | Full-stack patient/provider booking and paid-cancellation sandbox acceptance; unpaid holds/expiry |
| NC-10 | Durable notification outbox, retries, preferences, reminders and delivery visibility | Real provider sandbox delivery; final delivery receipts where supported |
| NC-11 | Draft/publish plan snapshots, versions, adherence; actual Chromium Unicode PDF regression passed | Clinical review, accessible reading order and full authoring browser acceptance |
| NC-12 | Published public content, inquiry intake, consent-aware publication and privacy screens | Approved bilingual clinic content; accessible browser review |
| NC-13 | Health endpoint, CI and local-safe configuration | MVP acceptance, deployment/recovery/security/accessibility review |
| NC-14 | Server-priced Stripe checkout, signed event ledger, leased cancellation refunds, authenticated refund recovery and race-safe reservations; regression tests passed | Selected provider sandbox, package checkout, unpaid holds/expiry and contracted wallet adapters if selected |
| NC-15 | Authorized uploads/downloads, file checks and report note history | Storage/provider acceptance and complete malicious-file scanning workflow |
| NC-16 | Measurements, derived BMI, dated corrections/history and progress UI | Clinical validation and end-to-end correction evidence |
| NC-17 | Patient/clinician/admin dashboards, scoped queries and currency-aware revenue | SQL query and browser reconciliation against representative records |
| NC-18 | Draft content, approval/moderation, feedback consent withdrawal, support inbox | Content ownership and bilingual editorial sign-off |
| NC-19 | Regression infrastructure | Release 1 combined acceptance and operational sign-off |
| NC-20 | Scoped server SignalR and browser polling, attachments and idempotency | Browser SignalR transport/reconnection/backfill and multi-user acceptance |
| NC-21 | Scoped join window/host links, Zoom cancellation synchronization | Provider credentials/OAuth lifecycle; Google saved-link limitations |
| NC-22 | Urdu/RTL portal foundation, PDF support and operational reports | Translation/accessibility review; optional AI has not been enabled |
| NC-23 | Verification CI, container/runtime updates; disposable SQL recovery rehearsal passed | Container execution, production recovery, performance targets and final launch approval |

## Verification ledger

Test counts and commands will be recorded here only after they execute successfully. Earlier successful tests do not imply the entire product is release ready.

- Final backend regression suite: **206 passed, 0 failed, 0 skipped**. Evidence: `.artifacts/backend-tests/verified.trx` (17 October 2026).
- That run comprises **131 non-SQL and 75 isolated SQL tests**. Coverage includes authentication HTTP lifecycle, eight-way credit/booking races, publication/review gates, isolated auditing, consent withdrawal/reconsent, refund recovery/leases, private-file exports and persisted clinical revisions. The actual Chromium PDF test was enabled and passed.
- Recovery rehearsal passed in the same run: a checksummed backup of the first tracked schema was verified, restored and upgraded through all three migrations. It preserved a custom legacy Starter price of PKR 4,250, all old drafts, six working-hour rules and the cancellation policy, while adding three separate v2 package proposals. All six resulting packages remain inactive, unpublished and unapproved.
- Final backend build passed with **zero warnings and zero errors**.
- EF `has-pending-model-changes` passed: the finalized model matches the tracked migration snapshot. The additive migration is `20261016143850_ClinicalCommercialWorkflows`.
- Backend solution and test-project transitive NuGet vulnerability audits reported **no vulnerable packages from the configured sources** on 17 October 2026.
- Final published API startup smoke passed: SQL readiness/liveness 200; profile, package enrollments and private export reject anonymous requests with 401; public services/packages are empty and cancellation policy is null. The isolated development database has all three migrations, 10 services, six package drafts (three preserved plus three new proposals), six working-hour rules and one policy, with zero activated/published/approved proposals. Restart preserves the six packages without duplication.
- Fresh publish artifacts exclude private `.local` and log directories. The video synchronization worker now processes due work in a deterministic order; the final 206-test suite and SQL startup check passed after that correction. The temporary smoke server was stopped after verification.
- Final portal lint, TypeScript and production build passed, with **34/34 desktop/mobile Chromium browser cases and zero skips** on 17 October. Published-record management/unpublication, local-origin login, private ZIP downloads, package benefits/review and validity extensions are covered. The fresh production dependency audit reported **zero vulnerabilities**; five development-only ESLint/braces findings have no upstream patch and are documented in [PORTAL_IMPLEMENTATION.md](PORTAL_IMPLEMENTATION.md). These fixture-backed browser checks complement the 75 real SQL tests; full-stack staging/provider acceptance remains separate.
- No existing production or `NutriCarePro` database was reset, deleted or migrated.

See [INTEGRATIONS_IMPLEMENTATION.md](INTEGRATIONS_IMPLEMENTATION.md) for provider limitations, [SRS_GAP_ANALYSIS.md](SRS_GAP_ANALYSIS.md) for the original defects, and the setup/runbook documents for exact local commands.

## Remaining implementation sequence

1. Review the proposed business configuration and bilingual policies through the saved nutritionist approval/publication workflows when the owner is ready. Local build, backend/browser regression, migration, recovery and published startup checks are complete; all proposals remain unpublished.
2. Complete package checkout/provider fulfillment and explicit package selection for plan/revision credits after nutritionist approval. Complete asynchronous diet-plan ordering and its delivery workflow. Package terms and credit accounting are implemented; no sales endpoint is enabled.
3. Complete unpaid-reservation holds/expiry and prove callbacks, automatic eligible cancellation refunds, retries and authenticated reconciliation in the selected provider sandbox. Local refund orchestration and regression evidence are present.
4. Complete private-file scanning/quarantine and orphan-file reconciliation. Implement retention/deletion after policy approval; private-file ZIP export is implemented and tested, with an explicit assisted-export path for missing/oversized data.
5. Complete notification delivery receipts/approved localized templates, browser SignalR reconnect/backfill, video provider authorization renewal and real-user/provider acceptance for the chosen deployment.
6. Rehearse full-stack role journeys on staging, adopt any populated legacy database using an inventory-based migration, verify coordinated database/file/key recovery, and finish accessibility, bilingual clinical review, performance and release sign-offs.

Business approval still required includes confirmed clinician/clinic identity, English/Urdu consent and privacy wording, the supplied proposed package terms, retention/recovery targets and contracted provider accounts. The proposed business configuration is not approval to publish or launch. None of the numbered release gates is marked complete solely because its code exists.
