# NutriCare Pro implementation backlog

Prepared 14 October 2026 from `Nutrition_Consultation_SRS.docx` v1.0 and the current source. See [SRS gap analysis](SRS_GAP_ANALYSIS.md) for evidence and [extracted requirements](SRS_EXTRACTED.txt) for the source text.

This began as the audit delivery sequence. Implementation is now underway; checked items have demonstrated evidence, while unchecked items include incomplete engineering and pending owner/external approval. See [current implementation status](IMPLEMENTATION_STATUS.md) for the verification ledger and remaining launch gates. A numbered step is complete only when its full acceptance criteria are met.

## How we will work through this list

For each numbered step: confirm the relevant business decisions, implement the smallest complete workflow across API/database/UI, add meaningful tests, verify the migration and role boundaries, record results, and then mark it complete. Keep the same IDs when work is split into issues or pull requests.

Authorization, privacy, accessibility, tests, documentation, and observability belong in each feature. Their release checkpoints below verify the combined result; they are not reasons to postpone these requirements. Existing unsafe endpoints need fixing or disabling before any real patient data or payments are used, even when the feature belongs to a later release.

## Foundation: complete before product expansion

### NC-01 — Restore a reproducible build and development baseline

Maps to NFR-08 and SRS sections 6, 10, 11. No dependency.

- [x] Resolve the SignalR and IdentityModel dependency downgrade errors and incompatible AutoMapper registration/packages.
- [x] Review the chosen runtime and package support/compatibility before selecting versions; consolidate unnecessary framework references.
- [x] Complete a clean restore/build and address material compiler/runtime startup errors revealed afterward.
- [x] Standardize local API/portal URLs; document required SQL/storage/provider configuration with safe development substitutes.
- [x] Set up a real backend test project, SQL integration-test harness, and browser E2E harness; replace the nominal lint script with a working configured lint check.
- [x] Add build/type/lint/test checks to CI and preserve the existing successful frontend build baseline.

Done when: another developer can follow the documented setup, build both applications, run the initial tests, and start a development instance without relying on old generated binaries. No live provider credentials are needed for this baseline.

### NC-02 — Close existing authorization and payment security defects

Maps to FR-02/04/07/08/10, NFR-01/02, and cross-account acceptance criteria. Depends on NC-01 for executable verification.

- [x] Restrict public registration to approved self-service roles; provision the first Admin through a controlled mechanism.
- [x] Apply one explicit patient/clinician-assignment policy to individual and list reads, exports, clinical writes, and provider links. Administrative clinical access must follow NC-04 policy.
- [x] Require valid signatures/configuration for payment callbacks; reject missing/invalid/stale signatures and mismatched providers before any state mutation.
- [x] Enforce appointment participation for SignalR groups and remove unrestricted client ability to publish system notifications or message arbitrary groups.
- [x] Exclude video host URLs from patient detail/history DTOs, not only from the join response.
- [x] Stop logging reset/verification links and clinical message content; sanitize auth errors and return safe field validation errors.
- [x] Fix session-revocation enforcement and test logout, deactivation, and role-change behavior; finish full session management in NC-05.
- [ ] Add negative permission tests for Patient A/B, assigned/unassigned clinicians, Admin policy, REST, hubs, files, and host links.

Done when: the critical bypasses are closed and automated tests prove that altering a record/group/recipient ID cannot cross an authorized boundary. Unfinished later-release features remain unavailable to real users.

### NC-03 — Establish trustworthy persistence, concurrency, and audit behavior

Maps to NFR-07/08/10 and SRS section 7. Depends on NC-01.

- [ ] Choose one authoritative migration path; reconcile the old SQL design and active Identity/EF model without discarding existing data.
- [x] Replace `EnsureCreated` as the upgrade strategy with tracked, repeatable schema upgrades and predeployment migration checks.
- [x] Replace unmapped `IsDeleted` query expressions with a deliberate mapped-field/query-filter design and verify real SQL translation.
- [ ] Define soft deletion versus policy-authorized permanent deletion, decimal precision, constraints, FK deletion behavior, and required indexes.
- [x] Add optimistic concurrency/version protection where lost updates matter; implement atomic booking protections in NC-09.
- [x] Make unit-of-work commit/rollback behavior explicit when no transaction exists and after failures.
- [x] Isolate audit persistence from failed business change tracking; record correct outcomes, actor, entity IDs, and redacted changes. Add clinical read/access logging required by policy.
- [x] Test fresh tracked database creation, representative additive upgrades, rollback/failure behavior, and recovery from a backup. Adoption of an untracked populated legacy database still requires a separate inventory and rehearsal.

Done when: a clean database and an upgraded database both support the same workflows, failed work is not accidentally committed by logging, and meaningful audit/access records can be traced.

### NC-04 — Record product rules, consent policies, and launch decisions

Maps to FR-01/03/04/07/10/11, NFR-02/09, and SRS sections 9, 12, 13. Can progress alongside NC-01–03.

- [ ] Confirm approved professional title, credentials, clinic details, service modes, fees/packages, hours, and content ownership.
- [ ] Define clinician-to-patient assignment and permitted Admin/support clinical access; design least-privilege permissions from these rules.
- [ ] Define cancellation/reschedule cutoffs, no-show handling, refund eligibility, slot holds, and unpaid-booking expiry.
- [ ] Approve privacy, sensitive-data/teleconsultation consent, optional marketing/testimonial consent, retention periods, export/deletion process, and emergency disclaimer.
- [ ] Select contracted/eligible payment, email, SMS, and video providers; record required test/live credentials and provider responsibilities.
- [x] Confirm single-clinician launch scope, whether Urdu is required at launch, and accessibility/localization priorities.
- [ ] Define backup recovery objectives and the load/workloads used to assess the proposed uptime/performance targets.
- [ ] Record product-owner/clinician decisions and arrange the SRS's requested local legal/professional review before launch; do not invent credentials, legal rules, fees, or consent text.

Done when: these choices have named owners and versioned decisions. Unresolved choices may remain marked pending during engineering analysis, but dependent launch behavior cannot be signed off on assumptions.

## MVP: public site, profiles, availability, booking, intake, plans, notifications, privacy

### NC-05 — Finish authentication, sessions, and profile onboarding

Maps to FR-02, NFR-01/02, UI requirements. Depends on NC-02/03/04.

- [x] Create the appropriate patient/clinician profile transactionally with account provisioning; implement real profile reads/updates and controlled clinician onboarding.
- [x] Build registration, verification, login/logout, resend, forgot-password, reset-password, and profile screens; repair email links and the missing token-verification handler.
- [x] Introduce a shared session/API client, expiry handling, refresh/revocation, safe token storage, role-aware routing, and consistent 401/403 handling.
- [x] Enforce actual login lockout/rate limits; authenticate before user-based rate partitioning and configure trusted proxy handling.
- [x] Store revocable session/refresh records appropriate to the agreed device/session policy; honor configuration rather than hardcoded expiry rules.
- [ ] Treat privileged MFA as an explicit optional scope decision; implement it if selected and record the decision if deferred.
- [ ] Test duplicate/partial registration, verification expiry, reset, token reuse, logout, concurrent refresh, deactivation, and privilege changes.

Done when: a newly registered patient can complete onboarding and reach their own dashboard through a normal browser session without pasting a JWT or manually inserting SQL records.

### NC-06 — Add the service catalog, pricing, and core administrative settings

Maps to FR-01/03/07/10, section 7. Depends on NC-03/04/05.

- [x] Model services, modes, durations, prices/packages, currency, policy versions, and required configuration; map existing fee/duration fields deliberately.
- [x] Add authorized admin/clinician configuration screens and validated APIs, with audit history.
- [x] Publish approved service/fee data for discovery and the booking wizard; do not trust browser prices as payment authority.
- [x] Define how price changes affect existing bookings, invoices, refunds, and package use; preserve booking/purchase snapshots and exact reviewed package versions.

Package benefits, eligible categories, activation/expiry rules and audited Admin extensions are implemented. All proposals remain drafts. The 17 October suite passed 206 tests, including package review, credit races, ownership, expiry and extension HTTP checks. Package checkout and clinical plan-package selection remain gated work; see [PACKAGE_CONFIGURATION.md](PACKAGE_CONFIGURATION.md) and [PACKAGE_CREDITS.md](PACKAGE_CREDITS.md).

Done when: public discovery, slot calculations, bookings, and future checkout use one authoritative service definition and price snapshot.

### NC-07 — Implement availability and timezone handling

Maps to FR-03 and NFR-09. Depends on NC-06.

- [x] Implement recurring working hours, breaks, holidays/leave, exceptional openings/closures, and per-service durations/modes.
- [x] Use Pakistan Standard Time as the default presentation zone; store instants in UTC and preserve the timezone needed for scheduling rules.
- [x] Generate slots with full interval overlap checks, bounds, buffers/capacity if agreed, and no past/out-of-hours suggestions.
- [x] Add an availability editor and accessible patient date/time selection with explicit timezone labels.
- [ ] Test interval boundaries, duration changes, cancellations, exceptions, browser timezone differences, and any supported daylight-saving zones.

Done when: displayed slots represent genuinely bookable intervals under the configured clinician calendar.

### NC-08 — Implement patient intake, consent, assignment, and dated notes

Maps to FR-04/05/06, NFR-02, section 9. Depends on NC-03/04/05.

- [x] Collect goals, history, allergies, medications, and lifestyle with clear field validation and data minimization.
- [x] Persist versioned consent records with purpose, policy text/version, actor, timestamp, and withdrawal behavior; keep optional marketing consent separate.
- [x] Implement explicit patient assignment and clinician access, including rules for patients with a plan but no appointment.
- [x] Add dated consultation notes with authorship and revision history rather than an overwrite-only notes string.
- [ ] Implement patient data export, deletion-request handling, retention processing, and access logging under the agreed policy; include private files when introduced. Private-file ZIP export and request/audit handling are tested; approved retention/permanent-deletion processing remains pending.
- [x] Provide patient and clinician interfaces and test unassigned-clinician and restricted-Admin cases.

Done when: sensitive intake and notes are consented, attributable, visible only to permitted people, and covered by retention/export/deletion workflows.

### NC-09 — Complete atomic booking and the booking wizard

Maps to FR-03, section 4 booking/cancellation acceptance criteria and section 8 wizard. Depends on NC-05/06/07/08.

- [x] Implement concurrency-safe booking/rescheduling against SQL Server for all overlapping intervals; a pre-insert read alone is insufficient.
- [ ] Reconcile status names with `Pending`, `Confirmed`, `Cancelled`, `Completed`, `No-show`; define any internal approval/rejection states and migrate old values.
- [x] Apply cutoff/cancellation/no-show rules, preserve rescheduling history, and release or hold slots according to policy.
- [ ] Build service → slot/timezone → intake → applicable payment → confirmation with validation, progress, recovery, and duplicate-submit protection.
- [ ] Provide patient history/reschedule/cancel and clinician queue/confirm/complete/notes screens; show the booking in both dashboards.
- [x] Persist a stable booking event atomically with the reservation for NC-10 notification processing; stage approved-policy financial consequences through durable cancellation-refund instructions. Real provider acceptance remains NC-14/19.
- [x] Race multiple simultaneous overlapping requests and prove only the allowed booking succeeds; test retries and state-transition boundaries.

Done when: atomic reservation, permitted state changes, persisted event identity, and booking UI are demonstrated in browser and real-database integration checks. Full booking acceptance, including once-only confirmation delivery, is certified jointly with NC-10 at NC-13. Checkout stays disabled until NC-14 if payments are excluded from MVP; paid-cancellation acceptance is completed with NC-14.

### NC-10 — Make notifications reliable and preference-aware

Maps to FR-09 and NFR-07/10. Depends on NC-03/04/05 and booking events from NC-09.

- [x] Persist transactional notification events/outbox records and deliver through a worker with retry/backoff, deduplication, status, and diagnostics.
- [x] Schedule reminders automatically and cancel/update them on booking changes; reject manual reminders for ineligible/cancelled/past appointments and add reschedule/cancellation and plan-publication alerts.
- [x] Add editable preferences and distinguish essential service messages from optional marketing.
- [ ] Use approved versioned/localizable templates and minimal external content; remove sensitive free text from email/SMS/logs.
- [x] Connect persisted in-app delivery to live updates and read state; prevent clients from forging system events.
- [x] Make simulated delivery explicit in development and fail safely when required production providers are unconfigured.
- [ ] Test retries, provider failures, duplicate events, preference changes, timezone formatting, and expired/cancelled reminder jobs.

Done when: an event sends the permitted messages once, failures are visible/retryable, and reminders follow appointment changes without manual API calls.

### NC-11 — Finish diet-plan authoring, publication, history, and PDFs

Maps to FR-05/06 and plan-assignment acceptance criteria. Depends on NC-03/05/08/10.

- [x] Add a usable clinician plan/meal editor with schedules, portions, nutrition estimates, instructions, review dates, and validation.
- [x] Preserve immutable plan revisions and authorship; distinguish drafts from published versions and archive safely.
- [x] Restrict authoring/assignment to permitted clinicians; enforce patient visibility of published current/previous versions.
- [x] Add patient plan history, adherence entry, and notification after publication, using authorization-consistent assignment rules.
- [ ] Replace the 40-line ASCII PDF limit with complete paginated, accessible output; verify long plans, Unicode, and any required Urdu/RTL support.
- [ ] Test concurrent edits, historical revisions, reassignments, draft visibility, archive behavior, downloads, and duplicate publication.

Done when: a clinician can publish a plan, the patient can read/download the full correct version, and earlier revisions remain traceable.

### NC-12 — Complete the public website, support contact, and privacy information

Maps to FR-01/11, NFR-02/05/06, public UI requirements. Depends on NC-04/06/09; can be built alongside clinical UI.

- [ ] Replace placeholder credentials, contact details, claims, counts, pricing, and review content with owner-approved factual content.
- [ ] Add credential/service-specific/fee/FAQ views and connect every booking CTA to the actual wizard.
- [x] Implement contact submission, spam/rate protections, acknowledgement, error recovery, and a support-management workflow.
- [ ] Publish privacy/consent/cancellation information and the clear non-emergency-care disclaimer.
- [x] Publish testimonials only with provenance, consent, and approval; otherwise omit unverified content until NC-18 moderation is ready.
- [ ] Make forms/navigation/core screens keyboard and screen-reader usable from 320 px upward; connect metadata/sitemap/structured data to approved content.

Done when: a visitor can discover accurate services, contact support, and start booking, and no decorative button or unverifiable testimonial is presented as a working/trusted feature.

### NC-13 — Pass the MVP release gate and establish production operations

Maps to all MVP requirements and NFR-01–10, SRS sections 10/11. Depends on NC-01–12.

- [ ] Run unit, SQL integration, API contract, permission, booking-concurrency, and browser E2E checks for the MVP.
- [ ] Audit WCAG 2.2 AA core journeys, keyboard/contrast/labels, 320 px layouts, and current Chrome/Edge/Firefox/Safari desktop/mobile behavior.
- [ ] Measure routine-read p95 against the agreed load (proposed target under 500 ms), public Core Web Vitals, and the proposed 99.5% monthly uptime monitoring objective.
- [ ] Separate local/staging/production secrets and databases; use managed secrets and verified TLS/encryption-at-rest configuration.
- [ ] Fix frontend build-time public URLs, CORS/proxy settings, SQL connectivity, storage configuration when enabled, container log permissions, and runtime probes.
- [ ] Configure daily backups, restore exercises, recovery objectives, alerts, redaction, request correlation, and operational runbooks.
- [ ] Add migration validation, staging deploy, smoke tests, rollback, and explicit production-promotion approval to CI/CD.
- [ ] Resolve critical/high defects; obtain required privacy/content/clinician sign-off and complete penetration testing or independent security review, as specified by the SRS, before handling real medical records.

Done when: the MVP's real browser journey works in staging, release evidence is recorded, recovery is rehearsed, and production promotion has its required sign-offs. Code presence alone does not satisfy this gate.

## Release 1: payments, reports, progress, richer dashboards, publishing, analytics

### NC-14 — Implement trustworthy payments, invoices, and refunds

Maps to FR-07, cancellation/payment acceptance criteria. Depends on NC-02/03/04/06/09/10; extend NC-13 operations to payments.

- [ ] Select approved providers and implement their verified sandbox contracts; do not assume all three named gateways must launch simultaneously.
- [ ] Calculate totals/currency from authoritative service/package/booking data and show the price, policy, and checkout before charging. Appointment checkout is implemented; package checkout/entitlements remain pending.
- [x] Persist booking-linked payment attempts/intents, provider/session/payment-intent IDs separately, idempotency keys, and webhook event IDs.
- [x] Verify signed callbacks with replay protection and provider/amount/currency/booking matching; apply guarded state transitions exactly once.
- [ ] Mark a booking paid only after verified provider success; implement holds/expiry and retries without duplicate charges.
- [ ] Generate accessible invoices/receipts; keep card credentials outside the application.
- [x] Fix Stripe session-versus-payment-intent refunds; account for partial/pending/failed refunds only at the correct state, with concurrency protection and cancellation policy.
- [ ] Build patient checkout/history/receipt and admin reconciliation/refund views with currency-aware reporting.
- [ ] Test duplicate/out-of-order/invalid callbacks, retry after timeout, overpayment/underpayment, partial refunds, charge failures, and reconciliation.

Done when: the payment-confirmation and paid-cancellation acceptance cases pass against provider sandboxes with no duplicate financial or notification effects.

### NC-15 — Complete medical reports and secure document handling

Maps to FR-04, report-upload acceptance criteria, NFR-02/07. Depends on NC-02/03/08 and storage operations from NC-13.

- [x] Finish patient upload/list/download and authorized clinician review/dated notes interfaces.
- [x] Enforce assignment-based access consistently for appointment-only and plan-based relationships according to policy.
- [ ] Validate extension, declared type, actual signature, size, and unsafe file behavior consistently; define scanning/quarantine as required by the threat model.
- [x] Store files privately with authorized streaming or short-lived links; isolate host/internal storage details from general DTOs.
- [ ] Clean up failed uploads and deleted/expired records reliably; coordinate metadata/file lifecycle and backups.
- [ ] Include files in retention/export/deletion and access logs; test unauthorized direct access and all permitted/invalid file cases.

Done when: only permitted patients/clinicians can upload/review/download reports, and failure/retention paths do not leak or orphan sensitive files.

### NC-16 — Complete progress monitoring and clinician-selected metrics

Maps to FR-06 and NFR-09. Depends on NC-03/08/11.

- [x] Expose supported body measurements in commands/UI and add clinician selection of validated metrics, units, and date semantics. A fully custom metric builder is optional, not an extra SRS obligation.
- [x] Build patient entry/edit/history, adherence, clinician review, and dated chart/trend screens.
- [x] Fix historical entry handling so profile current weight reflects the latest measurement by date, not the last saved form.
- [x] Label derived/estimated BMI and nutrition calculations and avoid diagnostic statements; define handling of supplied versus calculated values.
- [ ] Preserve edits and plan-adjustment history; test missing metrics, duplicate dates, units, ranges, and timezone boundaries.

Done when: patients can record supported metrics accurately, clinicians see permitted trends, and historical corrections do not corrupt current summaries.

### NC-17 — Finish dashboards and correct operational analytics

Maps to FR-10 and section 8, NFR-04/10. Depends on NC-09/11/14/15/16.

- [ ] Add working patient next actions and clinician queue, notes, plan editor, follow-ups, and progress navigation.
- [ ] Make summary counts independent from display-list limits and include all policy-defined assigned patients.
- [x] Correct refund-aware revenue, currency grouping, booking/paid/settled date semantics, date ranges, and empty states.
- [ ] Apply database-side filtering/pagination/aggregation with appropriate indexes; avoid loading full audit/user/clinical histories for a page.
- [ ] Enforce least-privilege support/admin dashboards, configurable reporting access, and audited exports.
- [ ] Handle partial API failures without discarding all useful dashboard sections; test totals against known fixtures.

Done when: dashboards support actual role workflows and reconcile to authoritative booking, clinical, and financial records.

### NC-18 — Finish content management, feedback, and moderation

Maps to FR-01/10/11. Depends on NC-04/06/12 and relevant identity/audit controls.

- [x] Add admin service/fee/FAQ/settings management where not already completed in NC-06 and revision/approval history.
- [x] Connect public published-blog listing and article detail pages to stored approved content; finish draft/publish/archive and safe rendering.
- [x] Implement patient feedback/testimonial submission, explicit public-use consent, moderation, approval/withdrawal, and provenance.
- [ ] Show only approved authentic reviews/claims and make structured data match visible published content.
- [ ] Finish support inquiry status/acknowledgement handling, permissions, retention, spam control, and audit trails.

Done when: content and feedback can be managed through authorized workflows and public pages never expose drafts, unapproved claims, or unconsented feedback.

### NC-19 — Pass the Release 1 gate

Maps to SRS sections 4, 10, 11. Depends on NC-14–18 and the continuing NC-13 controls.

- [ ] Run real-database and browser journeys for paid booking, receipts/refunds, uploads/review, progress, dashboards, and public content.
- [ ] Smoke-test configured email/SMS, payment sandboxes, file storage, migrations, and restore in staging.
- [ ] Repeat permission/security/accessibility/performance checks affected by new features and resolve critical/high findings.
- [ ] Record clinician/product/privacy sign-offs, deployment and rollback evidence, then follow approved production promotion.

Done when: Release 1 features satisfy their acceptance cases and operational controls with recorded evidence.

## Release 2: chat, video, advanced reporting, localization

### NC-20 — Finish secure realtime messaging

Maps to FR-08/09 and NFR-01/02/07/10. Depends on NC-02/05/08/10/15.

- [ ] Build patient/clinician conversation UI, persistent history, pagination, unread/read state, and permitted participant discovery; distinguish saved messages from acknowledged delivery and reads.
- [ ] Add a browser SignalR client, correct browser token transport, reconnection/backfill, deduplicated delivery, and consistent REST/hub side effects.
- [x] Validate group membership, typing/presence/notification recipients, role/access changes, and patient assignment on every relevant operation.
- [ ] Validate attachments before upload and authorize before storing; clean up failures and enforce private download/retention policies.
- [ ] Add multi-replica messaging/presence coordination if production runs multiple instances; avoid exposing global patient presence lists.
- [ ] Test cross-account access, reconnects, duplicate sends, offline users, attachment failures, removed assignments, and replica behavior.

Done when: only authorized participants can communicate, history recovers after disconnects, and no unrestricted hub method can forge notifications or join another consultation.

### NC-21 — Finish protected video consultations

Maps to FR-08, teleconsultation consent, NFR-01/02/07. Depends on NC-02/05/08/09/10.

- [ ] Verify selected Zoom/Meet provider contracts and implement OAuth/token renewal or the agreed server authorization lifecycle.
- [x] Add role-aware scheduling/join/completion controls with consent, allowed appointment states, join windows, and access revocation.
- [ ] Protect provider host credentials/links in every response path and avoid storing unnecessary reusable secrets; configure provider-side admission/access controls since an API check cannot revoke an already shared meeting URL by itself.
- [ ] Synchronize meeting changes/cancellation with appointment rescheduling and validate completion/recording-link rules.
- [ ] Use real provider sandbox/test meetings; explicitly disable simulated example links in production.
- [ ] Test patient/clinician access, early/late/cancelled/completed joins, provider failures, token expiry, and schedule changes.

Done when: authorized appointment participants receive the correct protected links, lifecycle changes remain synchronized, and host access cannot leak to patients.

### NC-22 — Complete advanced reporting and the agreed localization scope

Maps to FR-06/10, NFR-04/05/09, SRS Release 2. Depends on the corresponding completed feature steps.

- [ ] Specify and implement the approved advanced reports/exports with least privilege, currency/timezone correctness, pagination, and audit coverage.
- [ ] Introduce English/Urdu-ready content/templates/formatting and implement Urdu/RTL if selected for this release; test Unicode PDFs and screen-reader behavior.
- [ ] Treat AI-assisted drafting as optional additional scope: only implement after a decision, with privacy controls, explicit clinician review, publication approval, and audit history. No automated clinical decision-making.
- [ ] Keep diagnosis, emergency triage, insurance claims, and a multi-clinic marketplace outside the implementation unless separately authorized.

Done when: the agreed reporting/localization scope is verified and optional/deferred features have an explicit recorded decision rather than an invented requirement.

### NC-23 — Final integrated release and operational handover

Maps to all applicable SRS requirements, sections 10/11/14. Depends on NC-20–22 and continuing earlier release controls.

- [ ] Reconcile every row in `SRS_GAP_ANALYSIS.md` with test evidence and the implemented release; resolve or explicitly defer optional scope.
- [ ] Run all four-role desktop/mobile E2E journeys, API contracts, concurrency/retry/security tests, accessibility, performance, and provider smoke tests.
- [ ] Verify backup restoration, migration rollback/recovery, alert routing, replica behavior, managed secrets, log redaction, retention jobs, and provider incident handling.
- [ ] Complete runbooks for lockout, missed reminders, failed callbacks, privacy/security incidents, and disaster recovery; name operational owners.
- [ ] Obtain required clinician, product, privacy, and release sign-offs; promote using the approved staging-to-production process.

Done when: every applicable requirement has a demonstrable outcome, the required sign-offs are recorded, and operations can support and recover the released system.

## Immediate next implementation item

The complete backend suite passes **206/206 tests without skips**, including package-credit races, eligible cancellation-refund recovery, private-file export and all-three-migration backup/restore. Portal lint/type/production build and **34/34 desktop/mobile browser cases** also pass. See [implementation status](IMPLEMENTATION_STATUS.md) for current evidence. Next engineering work includes gated package checkout/clinical plan-package selection, asynchronous ordering, unpaid holds/expiry, complete file lifecycle/retention, browser realtime transport and provider authorization/delivery workflows. Nutritionist approval of the proposed business terms, bilingual policies, credentials and provider contracts remains required before dependent publication or live use.
