# Operations and release gates

The product is not approved for launch merely because builds pass. Track the remaining gates in [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md). CI performs verification and does not deploy automatically.

## Configuration and publication

All seeded commercial settings are proposed drafts. Review the clinician, English/Urdu names and text, duration/mode, fees, timezone, working intervals, buffer, and effective dates. Verify the active clinician's professional evidence. Approve and publish the intended versions explicitly. Publish approved HealthData and Teleconsultation policies before enabling dependent patient collection/bookings. An empty public catalog or unbookable calendar is the expected safe state before these decisions.

Existing bookings retain the original price/currency and policy JSON snapshot. A draft revision does not replace its published source. Plan edits create a new draft while patients continue seeing the published snapshot. Calendar closures conflicting with reservations must be resolved through the appointment workflow before publication.

## Database deployment

1. Back up the target and verify the backup. Rehearse the migration on a restored copy. Never use `EnsureCreated` as an upgrade.
2. Generate and review the SQL script with `dotnet ef migrations script --idempotent` using the persistence and API projects. Confirm the target database name independently before applying.
3. Deploy additive schema changes before code that requires them. Keep production `Database__ApplyMigrations=false` and `Database__SeedProposedConfiguration=false` unless a specifically reviewed release task authorizes them.
4. Verify readiness, authentication, isolated role checks, catalog effective dates, a synthetic appointment workflow, and financial/clinical record counts.
5. Keep backward-compatible code available. Do not roll back migrations that would discard clinical or financial records. If recovery needs a restore, stop writers, follow the agreed recovery point and time objectives, and reconcile provider events accepted after that backup.

The new baseline targets a fresh database. An old `EnsureCreated`/historical SQL database requires an inventory and tailored adoption migration before deployment; see [DEVELOPMENT_SETUP.md](DEVELOPMENT_SETUP.md).

## Backups and recovery

Set owner-approved RPO/RTO before production. Use encrypted backups with checksums, restricted access and an off-host copy; configure transaction-log backups if the chosen recovery model requires point-in-time recovery. Store database, private blob storage and encryption/Data Protection keys under a coordinated recovery plan. Monitor backup age and failures. Schedule restores to an isolated database and compare representative accounts, appointments, plan versions, reports, audit and financial balances. A successful backup command alone does not demonstrate recovery.

No retention window or permanent deletion job is enabled without an approved policy. Data-rights requests can be reviewed; the API refuses to mark a deletion completed while the retention process is absent. Patients can download records plus their retained private report/message files through `/api/privacy/export/bundle`. The bounded private temporary archive is completed before headers and includes a checksum manifest; a missing, unauthorized or oversized file produces an audited assisted-export error instead of an incomplete success. Follow [PATIENT_DATA_EXPORT.md](PATIENT_DATA_EXPORT.md) for limits and private assisted delivery.

The local recovery rehearsal passed through all three tracked migrations on 17 October 2026, preserving older draft prices and adding the new package proposals separately. That disposable SQL rehearsal does not replace coordinated production database, blob and key recovery or agreed RPO/RTO.

## Packages and cancellation refunds

Package proposals require explicit review of the exact content version by a verified nutritionist. Editing/revising clears that review; publication is a separate action. Checkout remains disabled. Existing enrollments retain purchased snapshots, and Admin validity extensions require the current row version and a recorded reason. Before activation an extension changes the first-consultation deadline; afterward it changes credit expiry. It does not restore consumed credits or change clinical access. See [PACKAGE_CREDITS.md](PACKAGE_CREDITS.md).

Eligible cash cancellation decisions stage durable refund instructions in the cancellation transaction. The worker uses `Payments:CancellationRefunds:WorkerEnabled` (default true); provider I/O occurs after durable reservations are committed. Signed callbacks and authenticated provider queries establish final money states. A recorded eligibility decision, queued job or pending provider refund is not a completed refund. Individual package appointments use credit policy and do not refund the entire package purchase.

Administrators inspect `/api/payments/cancellation-refunds` and its detail route. The `/{id}/reconcile` action retrieves provider state without creating a new refund. Reuse original idempotency keys; never create a new request to escape a pending balance reservation. An unknown submission beyond the safe provider idempotency window stops for review. Confirm provider records and use the integration recovery procedure; do not clear reserved balances or mark money returned manually. Missing approval/purchase evidence and unresolved credit decisions stay in review. See [INTEGRATIONS_IMPLEMENTATION.md](INTEGRATIONS_IMPLEMENTATION.md).

## Secrets, proxies and storage

Use secure configuration for a strong JWT key and provider credentials. Persist ASP.NET Data Protection keys in a protected shared location when running multiple API instances. Configure `ReverseProxy__KnownProxies__0` etc. for the exact immediate proxies; forwarded headers are otherwise ignored. Use TLS at the proxy and verify HTTPS redirects and real client-IP rate limiting. Do not trust arbitrary forwarded headers.

Run containers as the supplied unprivileged user. Persist only approved storage/key/log volumes; never expose `.local` or private blobs through static hosting. The local file/mail adapters are Development-only. The Dockerfile includes the PDF browser and Urdu fonts; a container runtime smoke test is still required on the deployment platform.

## Monitoring and incident response

`/api/health/live` checks the process; `/api/health` checks SQL readiness. Monitor readiness, error rate, API latency, auth failures, audit-write failures, outbox age/retries, provider synchronization failures, duplicate provider events and refund reservations. Do not log bearer tokens, reset links, uploaded files or clinical message bodies.

For an incident: restrict affected access, preserve redacted audit/provider event identifiers, revoke impacted sessions/security stamps, investigate scope, and follow the approved notification procedure. Reconcile payments with provider records before retrying failed actions. Provider acceptance is not proof of final email/SMS delivery; avoid bulk manual retries without checking delivery history.

Zoom revocation retries and Google manual-action requirements appear in provider synchronization state. Removing an application link cannot invalidate a patient-saved provider URL. Resolve external access in the provider console where necessary.

## Acceptance before launch

Complete the numbered MVP/R1/final release gates with named owners: clinician/product review; bilingual content and consent approval; keyboard/screen-reader/mobile checks; cross-account REST/hub/file tests; real provider sandbox flows including cancellation/refunds; representative SQL migration and restore rehearsals; capacity tests on an agreed workload; alerting and support exercises. Record dates, environment, exact versions and evidence rather than marking a gate complete from code inspection.
