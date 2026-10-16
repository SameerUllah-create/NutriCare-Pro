# NutriCare Pro: SRS gap analysis and defect register

Reviewed 14 October 2026 against `C:\Users\Samee\Downloads\Nutrition_Consultation_SRS.docx`, version 1.0, dated 14 October 2026, marked draft for stakeholder approval. The working text is preserved in [SRS_EXTRACTED.txt](SRS_EXTRACTED.txt). The original DOCX was read without modification.

The delivery checklist is [IMPLEMENTATION_BACKLOG.md](IMPLEMENTATION_BACKLOG.md). The architecture reference is [PROJECT_UNDERSTANDING.md](PROJECT_UNDERSTANDING.md).

This is the preserved **14 October audit baseline**, including its original defect findings and line references. Implementation has since changed the source and added executable verification. Use [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md) and the checked backlog items for current behavior, passing evidence and remaining work; the historical missing/partial labels below are not the current delivery status.

## Assessment

The existing application supplies useful backend foundations and dashboard/public-page scaffolding, but none of the eleven functional requirement groups is complete as an accepted end-to-end workflow. The largest missing areas are normal browser authentication/onboarding, service/calendar configuration, an atomic booking wizard, consent/intake/data rights, immutable plan revisions, reliable notifications, public content/support workflows, and automated release evidence.

Some existing implementations also require correction before being extended: public Admin registration, optional webhook verification, missing ownership/assignment checks, unrestricted hub methods, host-link exposure, failed-operation audit persistence, scheduling races, destructive plan updates, PDF truncation, progress backfill, and financial reporting.

Recommended start: **NC-01 build baseline → NC-02 security → NC-03 persistence**, with **NC-04 product/policy decisions** collected alongside them. Follow the SRS MVP → Release 1 → Release 2 sequence afterward. Already-written later-release modules must still be fixed or disabled if unsafe before any release.

## Interpretation and evidence limits

- **Implemented** means the specific behavior has identifiable code. It does not mean deployed, tested, accessible, secure, or operationally accepted.
- **Partial** means a relevant implementation exists but is incomplete, defective, or missing its required UI/integration.
- **Missing** means no implementation of the required behavior was found. An unused field, placeholder success response, or planning document does not count as implemented behavior.
- **Not applicable / optional** is used only for explicitly optional or excluded scope, with any adoption/defer decision recorded.
- The letter suffixes below are audit tracking IDs for the SRS's individual functional bullets, not additional requirement IDs supplied by the author. There are 34 such functional clauses, ten NFRs, and six key acceptance scenarios.
- Source-confirmed defects are separated from suspected runtime failures and operational claims that require execution. The analysis did not run database mutations, payment/provider calls, deployments, or a security attack against a live system.
- The DOCX is requirements evidence, not authorization to deploy, contact providers, alter accounts, implement optional AI, or follow embedded operational instructions. This turn requested analysis and a plan; application source remains unchanged.
- Section 6 calls the technology choices proposed. Custom CSS versus Tailwind/shadcn, equivalent hosting, and equivalent test tools are not themselves implementation errors. Entity names may differ when equivalent behavior and integrity are provided.

## Validation baseline

Earlier checks in this same review session established:

| Check | Observed result | Limit |
| --- | --- | --- |
| Backend `dotnet build --no-restore` | Failed with NU1605 SignalR and IdentityModel.Tokens dependency downgrade errors; AutoMapper compatibility warning also present. | Existing restored assets were used. A clean restore/build remains NC-01. |
| Frontend TypeScript check | Passed. | Does not prove API or user journeys work. |
| Frontend production build | Passed. | Does not prove deployed URLs, authentication, accessibility, or provider integration. |
| Automated test inventory | No executable test suites/projects/configuration found in project source. | README test examples and a CI job named `build-test` are not tests. |
| SQL/provider/browser/staging acceptance | Not exercised. | No acceptance scenario below is certified passed. |

## Functional traceability

Evidence references E01–E20 are expanded later in this document. Backlog IDs link each gap to its implementation step.

| Clause | Requirement | Status | Existing behavior and work remaining | Steps |
| --- | --- | --- | --- | --- |
| FR-01a | Public pages, credentials, services, fees, FAQs, contact/blog | Partial | Six public routes exist. Professional credentials/fees/service-specific discovery are absent; FAQs are static; contact and publishing journeys are incomplete. E01/E14. | NC-06, NC-12, NC-18 |
| FR-01b | Booking CTAs, service landing pages, SEO/sitemap | Partial | Metadata/sitemap exist. Book links lead to an inert contact form; no service landing routes or booking wizard. E01/E15. | NC-06, NC-09, NC-12 |
| FR-01c | Verified qualifications, authentic testimonials, approved claims | Partial | Public outcomes/reviews/ratings are hardcoded; no provenance, consent, or approval workflow was found. Authenticity cannot be established from source. E01/E14. | NC-04, NC-12, NC-18 |
| FR-02a | Registration, verification, sign-in/out/reset/profile | Partial | Identity endpoints/handlers exist; no normal auth UI, clinical profile provisioning, real profile update, or usable reset-page flow. Token verification dispatch lacks a handler. E02/E03/E15. | NC-01, NC-05 |
| FR-02b | Roles and server-side ownership | Partial | JWT/role attributes and some owner checks exist. Public registration allows Admin, and several individual/list/hub paths lack ownership or assignment enforcement. E02–E06/E11. | NC-02, NC-04, NC-05 |
| FR-02c | Expiry, revocation, login rate limits, optional privileged MFA | Partial | Expiring JWTs, hashed rotating refresh token, and generic rate limits exist. Access-token revocation/account-change enforcement, effective lockout, scoped login controls, and browser session handling are incomplete; MFA is optional. E03/E04. | NC-02, NC-05 |
| FR-03a | Hours, breaks, holidays, service durations/modes | Partial | One clinician duration/type fields exist. Working-hours metadata is unused; no rules/exceptions/calendar editor or service-specific schedule. E05/E06. | NC-06, NC-07 |
| FR-03b | Service/slot/timezone/intake/confirmation | Partial | Basic booking and notifications exist. Service selection, timezone normalization, intake, valid free-slot rules, and browser wizard are missing. E06/E13/E15. | NC-07–NC-10 |
| FR-03c | Atomic overlap prevention and configurable cutoffs | Partial | Overlap check is followed by a separate insert; no serialized reservation. Reschedule/cancel APIs lack cutoff policy and financial/notification coordination. E06/E18. | NC-03, NC-04, NC-09, NC-14 |
| FR-03d | Pending, Confirmed, Cancelled, Completed, No-show | Partial | Existing flow uses Pending/Approved/Rejected/Completed/Cancelled. Confirmed terminology needs reconciliation; No-show has no executable workflow. E05/E06. | NC-04, NC-09 |
| FR-04a | Goals/history/allergies/medications/lifestyle with consent | Missing | Some patient fields exist but profile actions are placeholders. No structured intake, consent record, or usable form exists. E02/E05. | NC-05, NC-08 |
| FR-04b | Reports and dated clinician consultation notes | Partial | Private-report upload/download/review backend exists; portal absent. Appointment notes are an overwrite-only string; report notes overwrite their previous value. E06/E09/E15. | NC-08, NC-15 |
| FR-04c | Health-record access, retention and deletion rules | Partial | Report checks exist for patient ownership and appointment/plan-related clinicians. Retention/export/deletion and restricted Admin clinical-access policies are missing. E09/E18. | NC-02–NC-04, NC-08, NC-15 |
| FR-05a | Create/version/edit/assign/archive plans | Partial | Create/edit/assign handlers exist. Revisions overwrite/delete prior content; authorized-patient assignment, publication, archive and editor workflow are incomplete. E07. | NC-02, NC-08, NC-11 |
| FR-05b | Meals/portions/nutrition/instructions/review dates | Partial | Most meal/nutrition fields exist. No clinician editor or review-date workflow; exports omit meal time. E05/E07. | NC-11 |
| FR-05c | Current/prior plans and accessible PDFs | Partial | List/download APIs and dashboard summary exist. No immutable revision history or full patient view; current-plan date selection and PDF completeness/accessibility are defective. E07/E15. | NC-11 |
| FR-06a | Dated weight, measurements, adherence, clinician-selected metrics | Partial | Weight/habits and adherence APIs exist. Measurement fields are not exposed through the tracking contract; no clinician metric selection; no entry UI or correction history. E05/E08. | NC-11, NC-16 |
| FR-06b | Charts/trends and estimated-calculation labels | Partial | Chart queries and weight display exist. Other metric views and estimated/observed provenance labels are missing; BMI is presented as a reading. E08/E15. | NC-16, NC-17 |
| FR-06c | Clinician review and plan adjustment with history | Partial | Clinician progress summary exists. Full review/adherence UI, dated review entries, and traceable plan/progress changes are absent. E07/E08/E15. | NC-08, NC-11, NC-16 |
| FR-07a | Consultation/package prices and checkout | Partial | Checkout adapter exists. Public authoritative pricing/packages and checkout UI are missing; the API trusts a client-supplied amount. E10/E15. | NC-06, NC-14 |
| FR-07b | Intent/status/refund records and receipts | Partial | Payment/invoice/refund entities exist. Booking-linked payment authority, receipts, provider ID separation, guarded refund settlement, and reconciliation need work. E05/E10. | NC-03, NC-14 |
| FR-07c | Signed callbacks, idempotency, no stored card credentials | Partial | Hosted provider calls avoid card fields in the observed model. Missing signatures bypass verification; event/request idempotency and guarded transitions are absent. E10. | NC-02, NC-14 |
| FR-08a | Authorized persistent conversations | Partial | SQL messages and association checks exist for direct sends. UI is absent, hub destinations are unrestricted, and REST/hub side effects differ. E11/E15. | NC-02, NC-20 |
| FR-08b | Unread, realtime, reconnect and restricted attachments | Partial | Counts/history/read events exist. No browser SignalR/reconnect/backfill; attachment checks are weaker than reports and storage precedes authorization. E11/E15. | NC-15, NC-20 |
| FR-08c | Protected video links and appointment policies | Partial | Meeting adapters/schedule/join/history exist. Host URLs leak in ordinary DTOs; join windows/cancelled-appointment checks and provider token lifecycle are incomplete. E12. | NC-02, NC-21 |
| FR-09a | Confirmations/reminders/reschedules/plan alerts | Partial | Booking/plan/payment/REST-message dispatch exists. Reminder trigger is manual, reschedule/cancel orchestration absent, and production delivery unverified. E06/E13. | NC-09–NC-11 |
| FR-09b | Preferences/status/retry/templates | Partial | Channel configuration and in-app status exist. User preferences unused; no durable queue/outbox, retries, scheduled jobs, or managed/localized templates. E05/E13. | NC-10 |
| FR-09c | Minimal sensitive content in external notifications | Partial | Some message alerts are generic. Email/SMS fallback logs contain bodies/recipients and email links include auth tokens; no enforced redaction/content policy. E13/E17. | NC-02, NC-10 |
| FR-10a | Services/pricing/availability/FAQs/blog/moderation admin | Partial | Blog CRUD, user status and clinician verification exist. Remaining content/settings/moderation resources and public publishing integration are missing. E14. | NC-06, NC-07, NC-18 |
| FR-10b | Least-privilege operational dashboards | Partial | Three dashboards and summary queries exist. Clinical Admin permissions, accurate currency/refund/date totals, database pagination, and working role actions remain. E14/E15. | NC-02, NC-04, NC-17 |
| FR-10c | Audit account/appointment/plan/document/payment actions | Partial | HTTP mutation logger exists. Entity IDs/history/read access/hub changes are incomplete; shared-context finally-save risks persisting failed business work. E17. | NC-03, NC-08, NC-13 |
| FR-11a | Contact, spam protection and acknowledgement | Partial | Form markup only; no handler, storage/delivery, anti-spam or acknowledgement. E01. | NC-12, NC-18 |
| FR-11b | Consented, moderated patient feedback/testimonials | Missing | Review entity/status and hardcoded public reviews do not provide submission/eligibility/consent/moderation/withdrawal. E01/E05/E14. | NC-04, NC-12, NC-18 |
| FR-11c | Clear non-emergency disclaimer | Missing | No urgent/emergency-care disclaimer found in public or portal source. E01. | NC-04, NC-12 |

## Non-functional requirements

| ID | Status | Evidence and remaining acceptance work | Steps |
| --- | --- | --- | --- |
| NFR-01 Security | Partial | HTTPS/security headers, RBAC and private-storage intent exist; security defects remain. Managed secrets, trusted proxy/TLS behavior, encryption at rest, rotation and permissions need deployment verification. Azure default encryption is not presumed absent. E04/E16/E17. | NC-02, NC-03, NC-05, NC-13 |
| NFR-02 Privacy | Missing product workflows | No privacy/consent/export/deletion/retention workflow. Clinical access policy is unresolved. Blob deletion alone is not a data-rights implementation. E05/E09/E18. | NC-04, NC-08, NC-12, NC-15 |
| NFR-03 Availability | Partial; target unverified | SQL/self health and container infrastructure exist. Add uptime monitoring, probes, incident ownership and response evidence. SRS proposed target is **99.5% monthly**, not the older README's 99.9%. E16. | NC-04, NC-13, NC-23 |
| NFR-04 Performance | Partial; target unverified | Indexes/cache/static pages exist, but some queries load whole datasets. Agree workload/load; measure **p95 under 500 ms** for routine reads and monitor public Core Web Vitals. No measured pass/fail is claimed. E04/E14/E18. | NC-04, NC-13, NC-17 |
| NFR-05 Accessibility | Partial; unaudited | Labels/native controls and semantic markup exist. WCAG **2.2 AA** core-journey conformance, keyboard/screen-reader/contrast checks and accessible PDFs are unverified/incomplete. E01/E07/E15. | NC-05–NC-13, NC-19, NC-22 |
| NFR-06 Compatibility | Partial; unverified | Responsive CSS exists. No recorded **320 px** and current Chrome/Edge/Firefox/Safari desktop/mobile validation. E15. | NC-12, NC-13, NC-19, NC-23 |
| NFR-07 Reliability | Partial; unverified operations | No notification retry/outbox or payment idempotency; migrations/recovery need repair. Verify daily backup configuration and a measured restore; managed SQL backup defaults may exist but were not audited. E10/E13/E16/E18. | NC-03, NC-10, NC-13, NC-14 |
| NFR-08 Maintainability | Partial | Layers/Swagger/types/logs/CI builds exist. Backend fails; schema upgrades/test/lint/migration gates and updated implementation documentation remain. E04/E16/E18/E19. | NC-01, NC-03, NC-13 |
| NFR-09 Localization | Partial | UTC timestamps exist. Pakistan default/day boundaries and explicit input conversion are missing; browser timezone is implicit; English/Urdu-ready catalogs/RTL/PDF support absent. E05/E07/E15. | NC-04, NC-07, NC-16, NC-22 |
| NFR-10 Observability | Partial | Serilog, exception trace IDs, mutation audit and Log Analytics definitions exist. Add consistent request correlation, safe event/access history, error/payment alerts, and redaction. E16/E17. | NC-02, NC-03, NC-10, NC-13 |

## Core data-model comparison

Section 7's names should be mapped to working behavior rather than mechanically creating duplicate tables.

| Area | Present or equivalent | Missing/inadequate behavior |
| --- | --- | --- |
| Identity | User, Role, Patient, Nutritionist; refresh hash/expiry on User | ConsentRecord behavior; revocable session/refresh history; reliable profile onboarding. |
| Scheduling | Appointment; fee/duration/availability metadata on Nutritionist; one notes string | Service, AvailabilityRule/Exception, policy/price snapshots, append-only dated ConsultationNote behavior. |
| Care | MedicalReport, DietPlan, DietPlanItem, DietPlanCompliance, WeightTracking | IntakeForm/snapshots, immutable DietPlanVersion, review dates, selected metrics and correction history. |
| Commerce | Payment, PaymentRefund, Invoice | Price/package authority, booking payment linkage/state, ProviderWebhookEvent/request idempotency and reconciliation. |
| Engagement | Message with deterministic pair conversation ID; Notification; Blog; Review | Managed conversation authorization/reconnect, consented moderated Testimonial publication, public content integration. A Conversation table is one possible design, not the only correct implementation. |
| Governance | AuditLog and deletion/timestamp fields | Clinical AccessLog behavior, RetentionPolicy, SystemSetting/configuration workflows and safe audit isolation. |
| Integrity | `long` IDs and several indexes/uniqueness rules | Booking serialization, material-update concurrency, defined decimal precision/constraints, consistent soft deletion and a unified migration history. |

## Defect register

All defects below are identified in source. Live behavior was not exercised; the control-flow defects remain relevant even though the current backend build is blocked. C = critical, H = high, M = medium. Priority is for planning, not a penetration-test severity certification.

| ID | Priority | Existing defect and impact | Evidence | Fix |
| --- | --- | --- | --- | --- |
| D01 | C | Anonymous registration accepts and assigns Admin, enabling unauthorized privileged account provisioning once the flow runs. | `AuthController.cs:51`; `CommandValidators.cs:63`; `AuthenticationHandlers.cs:103`. | NC-02 |
| D02 | C | Missing webhook header skips verification even when a secret exists; anonymous callback can update payment state. | `PaymentGatewayService.cs:203`, `:206`, `:219`, `:222`; `PaymentsController.cs:165`. | NC-02, NC-14 |
| D03 | H | Individual appointment reads do not check caller ownership and include consultation notes; any supplied ID is loaded. | `AppointmentsController.cs:239`; `AppointmentHandlers.cs:285`, `:548`. | NC-02 |
| D04 | H | Clinicians can query arbitrary patients' appointment/progress/plan lists; plan creation/reassignment does not enforce assigned-patient access. | `AppointmentsController.cs:114`; `ProgressController.cs:65`; `AdditionalControllers.cs:298`; `DietPlanHandlers.cs:33`, `:355`. | NC-02, NC-08, NC-11 |
| D05 | H | Any signed-in user can request another user's profile DTO including personal/contact fields. | `AuthController.cs:220`; `AuthenticationHandlers.cs:351`, `:390`. | NC-02 |
| D06 | H | Hub group membership and notification publication trust caller-selected targets; authenticated users can invoke methods beyond their appointment. | `CommunicationHubs.cs:73`, `:85`, `:145`, `:184`. | NC-02, NC-20 |
| D07 | H | Ordinary video detail/history DTOs contain HostUrl even though the join response hides it for patients; join omits appointment status/time policy. | `VideoConsultationHandlers.cs:114`, `:131`, `:365`, `:375`. | NC-02, NC-21 |
| D08 | H | Audit finally-block saves the same DbContext after business exceptions; pending business entities may be committed as part of audit persistence. | `AuditLoggingMiddleware.cs:26`, `:45`, `:63`; invoice added before gateway call in `PaymentHandlers.cs:63`, `:80`. | NC-03 |
| D09 | H | Backend build fails on package downgrades; successful runtime behavior is not established. | Recorded build; `NutriCare.API.csproj:16`; `NutriCare.Infrastructure.csproj:14`. | NC-01 |
| D10 | H | `EnsureCreatedAsync` does not apply upgrades, and SQL script versions differ from the Identity/EF schema. | `Program.cs:109`; old initial SQL versus `NutriCareDbContext.cs:11`. | NC-03 |
| D11 | H | Overlap checking and insertion are separate operations; concurrent bookings/reschedules can pass the same check. | `AppointmentHandlers.cs:35`, `:52`, `:443`; `NutriCareDbContext.cs:132`. | NC-09 |
| D12 | H | Free-slot query excludes exact starts rather than overlapping intervals and ignores business calendar rules. | `AppointmentHandlers.cs:397`, `:405`, `:410`. | NC-07, NC-09 |
| D13 | H | Plan revision deletes old meal rows and overwrites fields; integer Version does not preserve historical content. Reassignment can leave prior patient compliance attached. | `DietPlanHandlers.cs:72`, `:355`; `Repository.cs:61`. | NC-11 |
| D14 | H | Financial amount/related record comes from client, callbacks lack dedup/guarded transitions, and duplicate callbacks can repeat notifications. | `PaymentsController.cs:43`; `PaymentHandlers.cs:37`, `:189`; provider callback handling. | NC-14 |
| D15 | H | Refund balance/status advance even on provider pending/failed refund; Stripe checkout-session ID is later used as a payment-intent refund identifier. | `PaymentHandlers.cs:140`, `:156`; `PaymentGatewayService.cs:98`, `:153`. | NC-14 |
| D16 | H | Browser API/site values are not supplied during Docker `next build`; runtime-only public env values do not configure already-built client bundles. | Portal `Dockerfile:6`; `docker-compose.yml:38`; `main.bicep:184`; `patientDashboard.ts:3`. | NC-01, NC-13 |
| D17 | M | Password reset email opens a POST-only JSON endpoint, with no reset page; token-verification action has no handler. | `AuthController.cs:103`, `:116`, `:243`; `AuthenticationQueries.cs:34`. | NC-05 |
| D18 | M | Login configures lockout but checks passwords without enforcing lockout/failed-count logic; JWT validity outlives logout/deactivation/role changes. | `AuthenticationHandlers.cs:28`; `ServiceCollectionExtensions.cs:50`; JWT bearer configuration. | NC-02, NC-05 |
| D19 | M | PDF renderer silently takes only 40 lines on one page, drops Unicode and omits meal times. | `DietPlanHandlers.cs:527`, `:540`, `:558`, `:589`. | NC-11 |
| D20 | M | Historical progress backfill changes the profile's current weight regardless of measurement date. | `ProgressHandlers.cs:51`. | NC-16 |
| D21 | M | Rescheduling stores a self-reference and overwrites old time while retaining approval metadata; cancellation/reschedule do not coordinate notifications, video or refunds. | `AppointmentHandlers.cs:93`, `:136`; video schedule copied in `VideoConsultationHandlers.cs:72`. | NC-09, NC-10, NC-14, NC-21 |
| D22 | M | Future/expired Active plans can be displayed as current because the dashboard selects highest StartDate without date-range eligibility. | `DietPlanHandlers.cs:38`; `PatientDashboardController.cs:48`. | NC-11 |
| D23 | M | Revenue omits refunded-status gross payments yet subtracts refunds and mixes currencies; portal labels all money USD. | `AdminDashboardController.cs:27`, `:65`; frontend `admin/page.tsx:487`. | NC-14, NC-17 |
| D24 | M | UTC calendar days and browser-default display disagree with Pakistan-default Today/month and booking semantics. | `NutritionistDashboardController.cs:35`; `patient/page.tsx:166`; `AppointmentHandlers.cs:45`. | NC-07, NC-17 |
| D25 | M | Fallback logs include reset/verification links and SMS/message contents; generic auth catches expose exception messages. | `EmailService.cs:30`, `:60`; `SmsService.cs:29`, `:39`; `AuthController.cs:40`. | NC-02, NC-10 |
| D26 | M | Medical reports resolve patient ownership before upload, but storage precedes metadata commit without compensating cleanup. Chat additionally uploads before conversation authorization and lacks medical-report-equivalent signature checks. | `MedicalReportHandlers.cs:34`, `:58`; `ChatController.cs:72`, `:82`, `:160`; `AzureBlobStorageService.cs:54`. | NC-15, NC-20 |
| D27 | M | User-based rate partitioning occurs before authentication; supplied uid usually unavailable at partition time. Generic IP limits do exist. | `Program.cs:66`, `:69`; limiter in `ServiceCollectionExtensions.cs:103`. | NC-05 |
| D28 | M | Public Book/contact controls do not submit, blog cards are disconnected from stored articles, and structured data advertises unverified reviews/search capability. | `contact/page.tsx:74`, `:96`; `blogs/page.tsx:16`; `page.tsx:56`; `testimonials/page.tsx:32`. | NC-09, NC-12, NC-18 |
| D29 | M | Declared validation field errors are lost by global error serialization; the UI cannot reliably show useful field-specific errors. | `ValidationBehavior.cs:33`; `GlobalExceptionHandlingMiddleware.cs:51`, `:83`. | NC-03, NC-05 |
| D30 | M | Messages are marked DeliveredAt when saved, before receiver acknowledgement and even when offline; direct hub sends also omit the REST notification dispatch. | `ChatHandlers.cs:62`; `CommunicationHubs.cs:55`; `ChatController.cs:63`. | NC-20 |

### Suspected runtime defect to reproduce first

**S01 — get-only soft-delete property in SQL predicates (high priority).** `BaseEntity.cs:51` defines `IsDeleted => DeletedAt.HasValue`; `Repository.cs:31` passes `IQueryable` expressions directly to EF. Many queries use `!entity.IsDeleted`, but no property mapping or query translation exists in `NutriCareDbContext`. These expressions are expected to fail SQL translation. Confirm with the actual SQL provider/query generation or an integration test after NC-01; do not label this an observed live failure or move filtering to client memory as a shortcut. Fix in NC-03.

### Operational/integration concerns requiring validation

- Empty provider credentials silently produce simulated payment/video results or successful logged email/SMS delivery. A production fail-safe mode/configuration check is absent; actual configured providers were not tested.
- Bicep creates storage but does not wire API access, does not configure the deployed portal's CORS origin, and contains no SQL firewall/private-network setup. Existing external Azure settings could differ; deployment was not attempted.
- Container log-directory write permissions, proxy forwarding, health probes, session/data-protection key persistence across instances, and distributed SignalR/presence behavior need explicit environment validation.
- No repository test/lint/migration/staging/smoke/promotion gates establish the SRS release process. External GitHub protection/environment settings were not inspected.
- Backup/at-rest-encryption service defaults may exist; neither are certified by code inspection. Retention, restored data consistency, recovery objectives and alert ownership remain evidence to collect.
- Public testimonial/qualification authenticity is unverified, not proven false. Replace/withhold unapproved claims and structured ratings until provenance and consent are established.

## Six SRS acceptance scenarios

| Scenario | Current assessment | Required proof | Steps |
| --- | --- | --- | --- |
| New-patient booking | Partial, not passed | Normal onboarding; genuinely free interval; one reservation under concurrent attempts; one confirmation; both dashboards updated. | NC-05–NC-10, NC-13 |
| Diet-plan assignment | Partial, not passed | Only assigned clinician publishes; patient receives latest eligible published revision; old revision stays unchanged and traceable. | NC-02, NC-08, NC-11 |
| Report upload | Backend partial, not passed | Invalid/oversized/spoofed rejected; valid file private; patient and authorized clinician only; deployed storage and UI verified. | NC-02, NC-08, NC-15 |
| Payment confirmation | Partial with security defects, not passed | Booking paid only after verified matching provider confirmation; duplicates/retries cannot create extra charges/state/notifications. | NC-02, NC-14, NC-19 |
| Cross-account access | Source guards missing, not passed | Automated negative tests for changed IDs, lists, exports, chat/hubs, plans, files and host links, plus positive assigned-user cases. | NC-02 and each feature gate |
| Cancellation | Partial, not passed | Policy visible/enforced at cutoffs; authorized state change; slot release, refund and notification once; video/holds stay consistent. | NC-04, NC-09, NC-10, NC-14, NC-21 |

## UI, security/governance, testing and operations coverage

| SRS section | Existing state | Remaining checklist coverage |
| --- | --- | --- |
| §8 Public visual direction | White/teal custom CSS, layout, hero/services/FAQ/CTA scaffold exist; visual/accessibility review unperformed. | Approved professional copy and working discovery/booking/contact in NC-06/09/12; WCAG/browser evidence in NC-13. |
| §8 Booking wizard | Missing; public CTA terminates at inactive form. | NC-09 with service/timezone/intake/optional checkout/confirmation and recovery. |
| §8 Patient/nutritionist workspace | Summary displays exist; manual token entry; most clinical action screens absent. | NC-05/08/09/11/15/16/17/20/21. |
| §8 Administrator console | Metrics/user activation/clinician verification/blog editor/audit display exist; configuration/support/moderation incomplete. | NC-06/07/17/18 plus least-privilege controls. |
| §9 Consent, ethical claims and clinical governance | No explicit consent/workflow/provenance records. Admin clinical scope unresolved. No AI drafting feature exists. | NC-04/08/12/18; optional AI only in NC-22 with clinician review if selected. No unsupported diagnosis/emergency automation. |
| §10 Automated validation | No executable unit/integration/API contract/E2E suite found. | NC-01 test foundation, meaningful tests within each feature, NC-13/19/23 release evidence. |
| §10 Security/accessibility/performance/sign-off | Unverified; no completed review reports/signatures found. | Scoped assessments, critical/high defect closure, privacy/clinician sign-offs and measured targets in NC-13/19/23. |
| §11 Environment/deployment | Docker, dev/prod config, CI image/deploy steps and Bicep exist. | Independent staging/prod resources/secrets, migrations, URL/CORS/storage/SQL fixes, smoke tests, explicit promotion in NC-13/19/23. |
| §11 Recovery/monitoring/runbooks | Health/logging foundation exists; backup/restore/alerts/objectives/runbooks not demonstrated. | Daily backup and restore evidence, incident drills and named owners in NC-04/13/23. |
| §12 Release priorities | Earlier implementation spans features from several releases but does not complete the core patient journey. | Complete MVP NC-05–13; Release 1 NC-14–19; Release 2 NC-20–23 after foundation NC-01–04. |
| §13 Open decisions; §14 Sign-off | SRS is explicitly a draft with blank signatures. | Record decisions in NC-04; collect actual sign-off at release gates, not as a precondition to this analysis. |

## Decisions needed before dependent implementation or launch

These do not block preparing this analysis. They must not be silently invented when their implementation step begins.

1. Approved clinician name/title/credentials, location/contact details, exact services and supported consultation modes.
2. Consultation/package fees, currency, working hours, buffers, capacity, and holiday/break rules.
3. Meaning of Confirmed versus existing Approved, approval/payment timing, no-show handling, slot holds and unpaid expiry.
4. Cancellation/reschedule cutoffs and refund rules, including partial/pending refund behavior.
5. Patient assignment criteria, duration of historical clinician access, clinical-note visibility and restricted Admin/support access.
6. Privacy/health/teleconsultation/marketing/testimonial consent text, withdrawal rules, retention periods, export/deletion ownership and audit preservation.
7. Eligible contracted providers and whether one or multiple payment/video providers launch; sandbox/live account access when testing is due.
8. Single-clinician scope, whether Urdu is mandatory at launch, optional privileged MFA and optional AI drafting decision.
9. Proposed uptime/load targets, daily backup policy, acceptable data loss/recovery time, monitoring and incident owners.
10. Stakeholder/clinician approvals and the SRS's requested local professional/legal review before launch. This audit makes no legal compliance determination.

## Source evidence index

Paths are relative to the project root. Line numbers identify the inspected code snapshot.

| Ref | Primary evidence |
| --- | --- |
| E01 | `frontend/nutricare-pro-portal/src/app/page.tsx:23`, `:56`, `:107`; `about/page.tsx:15`; `services/page.tsx:16`; `testimonials/page.tsx:16`, `:32`; `contact/page.tsx:74`, `:96`; `blogs/page.tsx:16`, `:84`; `seo.ts:35`; `sitemap.ts:4`. |
| E02 | `src/backend/NutriCare.API/Controllers/AuthController.cs:51`, `:103`, `:116`, `:220`, `:243`; `Controllers/AdditionalControllers.cs:39`, `:57`, `:112`, `:169`. |
| E03 | `src/backend/NutriCare.Application/Handlers/AuthenticationHandlers.cs:28`, `:88`, `:103`, `:137`, `:176`, `:351`, `:390`; `Queries/AuthenticationQueries.cs:34`; `src/backend/NutriCare.Infrastructure/Authentication/JwtTokenService.cs:30`. |
| E04 | `src/backend/NutriCare.Application/Validators/CommandValidators.cs:63`; `src/backend/NutriCare.API/Extensions/ServiceCollectionExtensions.cs:50`, `:96`, `:103`, `:181`; `Program.cs:66`, `:69`. |
| E05 | `src/backend/NutriCare.Domain/Entities/DomainEntities.cs:53`, `:59`, `:97`, `:98`, `:112`, `:143`, `:232`, `:279`, `:412`, `:446`; `Entities/BaseEntity.cs:51`. |
| E06 | `src/backend/NutriCare.API/Controllers/AppointmentsController.cs:54`, `:114`, `:239`; `src/backend/NutriCare.Application/Handlers/AppointmentHandlers.cs:35`, `:52`, `:93`, `:136`, `:285`, `:397`, `:443`, `:548`. |
| E07 | `src/backend/NutriCare.Application/Handlers/DietPlanHandlers.cs:33`, `:38`, `:72`, `:355`, `:527`, `:540`, `:589`; `src/backend/NutriCare.API/Controllers/AdditionalControllers.cs:298`; `Controllers/PatientDashboardController.cs:48`. |
| E08 | `src/backend/NutriCare.Application/Handlers/ProgressHandlers.cs:43`, `:51`, `:148`; `src/backend/NutriCare.API/Controllers/ProgressController.cs:65`, `:101`, `:137`; `src/backend/NutriCare.Application/DTOs/ApplicationDtos.cs` tracking request/response contracts. |
| E09 | `src/backend/NutriCare.Application/Handlers/MedicalReportHandlers.cs:34`, `:58`, `:107`, `:265`, `:301`, `:311`; `src/backend/NutriCare.API/Controllers/MedicalReportsController.cs:144`; `src/backend/NutriCare.Infrastructure/Storage/AzureBlobStorageService.cs:30`. |
| E10 | `src/backend/NutriCare.API/Controllers/PaymentsController.cs:43`, `:165`; `src/backend/NutriCare.Application/Handlers/PaymentHandlers.cs:37`, `:63`, `:80`, `:140`, `:156`, `:189`; `src/backend/NutriCare.Infrastructure/Payments/PaymentGatewayService.cs:98`, `:153`, `:203`, `:222`. |
| E11 | `src/backend/NutriCare.API/Controllers/ChatController.cs:72`, `:82`, `:160`; `Hubs/CommunicationHubs.cs:73`, `:85`, `:145`, `:184`; `Hubs/ChatPresenceTracker.cs`; `src/backend/NutriCare.Application/Handlers/ChatHandlers.cs:236`. |
| E12 | `src/backend/NutriCare.Application/Handlers/VideoConsultationHandlers.cs:72`, `:114`, `:131`, `:365`; `src/backend/NutriCare.Infrastructure/Video/VideoMeetingService.cs:35`; `src/backend/NutriCare.API/Controllers/VideoConsultationsController.cs:27`. |
| E13 | `src/backend/NutriCare.Infrastructure/Notifications/NotificationService.cs:43`, `:163`, `:207`; `Notifications/SmsService.cs:29`, `:39`; `Email/EmailService.cs:30`, `:60`; `src/backend/NutriCare.API/Controllers/NotificationsController.cs:105`. |
| E14 | `src/backend/NutriCare.API/Controllers/AdminDashboardController.cs:14`, `:27`, `:65`, `:181`, `:298`, `:321`, `:387`; `frontend/nutricare-pro-portal/src/app/admin/page.tsx:93`, `:231`, `:487`. |
| E15 | `frontend/nutricare-pro-portal/src/app/patient/page.tsx:32`, `:83`, `:114`, `:166`; `nutritionist/page.tsx:78`; `video-consultations/page.tsx:125`, `:213`; `globals.css:1023`; `src/services/patientDashboard.ts:3`; `package.json:5`. |
| E16 | `.github/workflows/ci-cd.yml:18`, `:50`; `infra/azure/main.bicep:72`, `:124`, `:184`; `docker-compose.yml:38`; portal `Dockerfile:6`; `src/backend/NutriCare.API/Program.cs:38`, `:74`, `:109`; `Properties/launchSettings.json`. |
| E17 | `src/backend/NutriCare.API/Middleware/AuditLoggingMiddleware.cs:26`, `:45`, `:54`, `:63`, `:74`; `GlobalExceptionHandlingMiddleware.cs:40`, `:51`, `:83`; `SecurityHeadersMiddleware.cs:16`; `src/backend/NutriCare.Infrastructure/Email/EmailService.cs:30`. |
| E18 | `src/backend/NutriCare.Persistence/Contexts/NutriCareDbContext.cs:11`, `:132`; `Repositories/Repository.cs:31`, `:61`; `Repositories/UnitOfWork.cs:31`, `:50`; active SQL files and `backend/NutriCare.Persistence/Migrations/001_CreateInitialSchema.sql`. |
| E19 | Backend `.csproj` files, frontend `package.json`, complete source/test-config inventory, and CI workflow; validation results recorded in this session and `PROJECT_UNDERSTANDING.md`. |
| E20 | `src/backend/NutriCare.API/Controllers/NutritionistDashboardController.cs:35`, `:70`, `:131`; `PatientDashboardController.cs:48`, `:103`; browser date and revenue formatters. |

## Correction to the earlier engineering guide

The earlier guide said clinician report access required a diet plan and did not use appointments. That was incorrect: `MedicalReportHandlers.cs:301` checks appointments and `:311` checks diet plans. The guide has been corrected. The remaining task is to define which relationships/states authorize ongoing clinical access and enforce the agreed policy, not to add an appointment branch that already exists.

Other useful implementation is retained in this assessment: generic rate limiting, verification/reset API handlers, private upload/signature validation, some ownership checks, plan/compliance/progress APIs, dashboard summaries, SEO, admin blog CRUD, logs and health checks. Their presence is credited without calling the unfinished workflows complete.
