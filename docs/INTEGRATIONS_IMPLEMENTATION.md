# Integration implementation and verification

Updated 17 October 2026. This records implemented behavior and remaining provider/release work; it does not certify production readiness.

## Payments

Checkout accepts an owned appointment with an approved `PriceSnapshot` and currency. The browser's expected amount must match that snapshot. An idempotency key is mandatory for payment and refund requests (`Idempotency-Key`: 16–100 ASCII letters, digits, `_` or `-`). Only Stripe is enabled. The unverified generic JazzCash/EasyPaisa contracts were removed and those adapters fail closed.

Appointments covered by package credits cannot be charged again through standalone checkout. Public package checkout and package selection during clinical plan authoring are not wired. Their integration and acceptance remain required before package sales are enabled.

Payment/invoice intent is committed before contacting Stripe, using a stable provider idempotency key. Checkout session and payment-intent IDs are separate. Initial checkout never marks a booking paid. Provider failures leave a durable pending intent that can be retried with the same key. Approved portal origins constrain return URLs.

Callbacks require a configured webhook secret and signature, a valid SHA-256 MAC, and a signature timestamp within five minutes. Multiple `v1` signatures support provider key rotation. Supported events are `checkout.session.completed`, `checkout.session.async_payment_succeeded`, `checkout.session.async_payment_failed`, `checkout.session.expired`, `refund.created`, `refund.updated`, and `refund.failed`. Validly signed unrelated events are acknowledged without business changes.

Callback processing matches provider, currency, amount and checkout identity. A unique provider event ledger stores event ID and payload hash. Replays do not repeat payment fulfillment or receipt enqueueing; a reused event ID with different content is rejected. Settled/refunded payments cannot regress to pending/failed. Payment success updates invoice and appointment payment state and stages the receipt notification within the same database transaction. A late payment for a cancelled/rejected booking becomes `RefundReviewRequired` and does not reactivate the booking.

Refund requests reserve the refundable balance in a durable Pending refund before contacting the provider. Concurrent reservations share a payment lock. Pending/failed refunds do not increase refunded money; successful final states recompute the refunded total. Stripe refunds use the verified `pi_` ID, never the `cs_` session ID. A callback that races ahead of refund-ID persistence is retried by the provider; retrying the original request with its original key also recovers the provider result.

The application never receives or stores card numbers/CVCs. Raw provider payloads and clinical payment descriptions are not persisted as gateway diagnostics.

### Payment recovery

- A checkout timeout: locate the existing payment and retry the same request with its original idempotency key; do not create another key to force another charge.
- A pending refund after timeout: retry the same refund key. It continues to reserve its balance until the provider gives a final state. Do not manually increase `RefundedAmount`.
- A failed/missing callback: review the provider event delivery, correct configuration/signature/amount mismatches, then replay the provider event. Duplicate event IDs are safe. No unsigned local override exists.
- A cancelled booking with `RefundReviewRequired`: review its cancellation instruction and approved policy evidence. Automatic refunds use only the policy snapshot that was approved when the booking was made. Legacy or incomplete evidence requires review; it never authorizes an automatic refund.

Cancellation now stages a durable refund instruction and decision audit in the same transaction as the appointment change. The background worker reserves eligible refunds and contacts Stripe after the SQL transaction commits. Package entitlements are handled separately and are not inferred by this cancellation workflow. Checkout holds and expiry remain separate release work.

Appointments retain their booked cancellation-policy snapshot. Self-service rescheduling at least 24 hours before the current appointment does not consume the short-notice quota. Within the approved 6–24 hour window, the configured quota counts prior patient reschedules in that same window using their recorded previous start time and change timestamp. The total reschedule counter continues to record every change. Below the approved cutoff, the patient must contact the clinic.

### Durable cancellation refunds

Eligibility uses the recorded cancellation time and actor, the booked price/currency, and the exact booked policy snapshot, including publication, explicit approval, version and effective dates. An eligible patient cancellation at least the approved full-refund cutoff before the appointment queues a full refund. The approved provider-cancellation rule also covers provider rejection and short-notice provider cancellation. Patient cancellations in the reschedule-only window or below its cutoff and all no-shows never trigger automatic refunds. Administrator cancellations and missing or changed evidence require review. No proposed configuration is published or activated by the worker.

An instruction has a unique appointment key and a five-minute recoverable worker lease. Eligible cancellations before capture wait in `AwaitingSettlement`; authenticated payment/refund callbacks wake the same instruction and invalidate stale worker summaries. Every captured payment on the appointment is considered, including a later capture after an earlier refund completed. Existing manual refunds and reservations share the payment lock; automatic work reserves only the uncovered balance and never treats a pending reservation as refunded money. A failed automatic refund requires review rather than another automatic operation key.

Refund processing uses authenticated provider reads. A known refund ID is retrieved with `GET /v1/refunds/{id}`. A lost creation response is recovered through a complete, paginated refund listing for the verified payment intent and exact `nutricare_refund_key` metadata. Amount, currency, payment intent and operation identity must match before money is marked refunded. Provider-pending refunds are read again without another creation request. Signed completion callbacks take precedence over stale pending reads. Ledger mismatches preserve reservations and require review.

An unknown submission more than 23 hours old is never posted again automatically: Stripe may prune idempotency keys after 24 hours. It stays reserved in `NeedsReview`; read-only reconciliation may still recover an existing provider refund. Provider/configuration failures retry with bounded backoff and then require review. Refund statuses, attempts, safe error codes and decision/status audit entries persist without raw payloads or clinical notes. Completion notices are enqueued only after every captured amount is confirmed refunded; provider acceptance or a pending refund is never described as completion.

Administrative operations:

- `GET /api/payments/cancellation-refunds?status=NeedsReview`: recent instruction decisions and retry status.
- `GET /api/payments/cancellation-refunds/{id}`: captured payments and refund status/error metadata.
- `POST /api/payments/cancellation-refunds/{id}/reconcile`: authenticated provider reads only; this cannot issue a new refund, clear an uncertain reservation or bypass an expired idempotency window.
- Existing manual refund requests remain available with their original idempotency key and report pending versus confirmed provider status explicitly.

`Payments:CancellationRefunds:WorkerEnabled` controls the worker (default enabled); it acts only on staged, approved evidence. No real refunds or provider calls were made during implementation. The cancellation/refund and gateway regression tests passed in the final verification run below; real provider sandbox acceptance remains outstanding.

The provider contracts were checked against [Stripe refund retrieval](https://docs.stripe.com/api/refunds/retrieve), [refund listing](https://docs.stripe.com/api/refunds/list), [refund creation metadata](https://docs.stripe.com/api/refunds/create), and [idempotent request retention](https://docs.stripe.com/api/idempotent_requests).

## Messaging and presence

`GET /api/Chat/contacts` returns authorized participant IDs and names. Patient–nutritionist access uses active accounts and exact care relationships. Explicit expired/revoked assignment history overrides older appointment/plan associations. Without explicit assignment history, confirmed/approved/completed appointments or a published plan establish the legacy relationship. History, unread summaries, read changes, attachment downloads, typing and online-status calls apply the same policy.

Messages persist before broadcasts. An optional text-message idempotency key prevents duplicate storage after retries; a stable notification event is staged in the message transaction. `DeliveredAt` remains empty until receiver acknowledgement through the read operation. The frontend must deduplicate broadcasts/history by message ID. Existing direct hub and REST send paths now share persistence and notification behavior.

Authenticated hub clients cannot publish system notifications or arbitrary group messages. Appointment group joins verify actual participation; presence is sent only to authorized contacts. Presence tracking handles multiple connections for one user atomically, but remains process-local. Multi-replica presence/backplane operation requires a configured distributed solution before scaling out.

Uploads authorize the relationship before blob storage, reject unsupported size/MIME/extension/signatures, store privately, and clean up when persistence fails. Failed duplicate concurrent upload blobs are cleaned up. Files are limited to 10 MiB; the multipart envelope has an 11 MiB request limit. Malware/quarantine integration and complete retention/export/file lifecycle must be verified with the broader document subsystem.

## Video

General video detail/history DTOs expose neither direct meeting URLs nor host URLs. The join endpoint checks shared clinical access, current teleconsultation consent, confirmed appointment state and a window from 15 minutes before scheduled start to 15 minutes after scheduled end. The clinician host URL is returned only to the actual appointment clinician. Arbitrary recording URLs are disabled pending a consented private recording workflow; completion requires an active session.

Booking resolves an `Either` service to an explicit `Online` or `InPerson` choice. `Video` is accepted as an input alias for `Online`. Remote choices require current teleconsultation consent, and the saved appointment retains its concrete mode. An in-person appointment cannot be scheduled as a provider video meeting. Professional verification and an active, email-confirmed, nondeleted clinician account are both required for new bookings.

Google Meet creation uses its documented Space configuration with restricted admission. Zoom meetings use a waiting room and disable joining before the host. Missing provider configuration fails closed except for explicitly enabled Development simulation. Provider payloads and reusable host URLs are not returned in general diagnostics.

The lifecycle worker finds locally cancelled/rescheduled consultations and attempts Zoom meeting deletion with bounded retries. A documented Zoom missing-meeting response is idempotent. A replacement cannot overwrite the old provider ID until cancellation is confirmed. Google Meet has no Space deletion endpoint in the supported API: its saved external URLs cannot be claimed revoked by clearing the application's URL, and cancellation is recorded as `ManualActionRequired`. Operator procedures/admission settings and provider selection must resolve that limitation before the protected-cancellation acceptance gate is signed off. Static configured provider access tokens still need an approved renewal/server-authorization lifecycle. No live provider meetings were created or deleted during this work.

## Notifications

`Notification` is a durable per-channel outbox. Clinical workflows can stage rows with `INotificationOutbox.EnqueueAsync` and a stable event key before committing the business transaction. The delivery worker claims rows with a database lock and a five-minute lease, retries failures with exponential backoff up to five attempts, and persists attempts, next attempt, status and safe errors. Reminder generation deduplicates 24-hour/one-hour events and skips cancelled/past appointments at send time. In-app persistence is published through the server notification hub.

The worker checks current channel preferences again before delivery. External content uses minimal generic messages; message bodies, plan titles and clinical notes do not enter email/SMS or diagnostic logs. External status `Accepted` means provider acceptance, not confirmed delivery. External delivery webhooks/provider IDs, versioned approved/localized template management, and the provider's response to a crash after acceptance still require follow-up. The outbox prevents duplicate business event enqueueing, but no exactly-once email/SMS claim is made across that crash boundary.

New authenticated endpoints:

- `GET/PUT /api/Notifications/preferences`: `{ email, sms, inApp }`.
- `GET /api/Notifications/delivery-status`: own recent delivery/attempt metadata.
- `POST /api/Notifications/{id}/retry`: Admin-only retry of a failed delivery.

Email/SMS providers no longer report simulated success in production. Development email requires `Integrations:DevelopmentMailDirectory`; it writes unique `.eml` files under that configured local mailbox without logging recipient addresses or links/tokens. That directory must be excluded from version control, access restricted, and never exposed by the web server. SMS without a phone number or provider configuration is unsuccessful. `Integrations:AllowSimulation` is honored only in Development for payment/video simulations; signed callbacks remain mandatory in every environment.

## Verification and CI

The final .NET 10 build passed with **zero warnings and zero errors**. On **17 October 2026**, the complete backend suite passed **206/206 tests with no skips**: **131 non-SQL tests and 75 SQL tests**. Evidence is in [verified.trx](../.artifacts/backend-tests/verified.trx).

The integration unit subset passed **46 tests**: the original 22 security/integration cases, 20 cancellation-refund cases and four gateway-reconciliation cases. The verified per-file results relevant to these integrations are:

| Test file | Type | Passed |
| --- | --- | ---: |
| `PaymentSignatureTests.cs` | Non-SQL | 8 |
| `ProviderSafetyTests.cs` | Non-SQL | 4 |
| `PaymentIntegrityTests.cs` | Non-SQL | 4 |
| `CommunicationAuthorizationTests.cs` | Non-SQL | 4 |
| `NotificationOutboxTests.cs` | Non-SQL | 2 |
| `CancellationRefundTests.cs` | Non-SQL | 20 |
| `RefundGatewayReconciliationTests.cs` | Non-SQL | 4 |
| `SqlPaymentSafetyTests.cs` | SQL | 2 |
| `SqlCancellationRefundTests.cs` | SQL | 3 |
| `SqlPackageCreditTests.cs` | SQL | 12 |
| `SqlAuthenticationHttpTests.cs` | SQL | 7 |

These passed cases cover signed callback validation, fail-closed providers, price/currency checks, refund reservations, replay protection, assignment revocation, host URL masking, cancelled joins, notification preferences, presence, local-mailbox redaction and mocked Zoom deletion. Cancellation-refund coverage verifies approved snapshot eligibility, lost provider responses, pending-refund reads, expired idempotency review, callback state preservation, recoverable leases, late/multiple captures and manual-refund reservations. SQL cases verify payment/refund concurrency, the cancellation-worker lease, late capture after cancellation and the eight-way package-credit reservation race.

The run also passed all **20 `AppointmentPolicyRegressionTests`** cases, including concrete appointment modes, remote consent, clinician account checks, mode-safe retries and rescheduling cutoffs/quotas. The 75 SQL tests additionally cover configuration publication, account and clinician-review gates, independent failed-transaction auditing, clinical workflows, package enrollment/extension authorization, and checksummed backup/restore with an additive upgrade. Authentication HTTP tests demonstrate Patient-only registration and profile creation, verified login, own-account 200 versus cross-account 403, and immediate 401 rejection after logout, refresh replay, password reset, deactivation and role change. The test host supplies its signing key and isolated connection before application service registration; production JWT validation remains unchanged.

These cases are marked `Category=SqlIntegration` and use only fixture-generated `NutriCareTest_*` databases. Configure `NUTRICARE_TEST_SQL_CONNECTION` for an isolated test SQL Server. The fixture validates its generated database name before cleanup.

Run the isolated unit subset with:

```powershell
dotnet test tests/NutriCare.Tests/NutriCare.Tests.csproj --filter "FullyQualifiedName~NutriCare.Tests.Integrations&Category!=SqlIntegration"
```

The existing `.github/workflows/ci-cd.yml` now runs verification only: .NET 10 build/security/clinical/PDF tests, migration-model checks, an isolated SQL Server 2022 test service, and frontend type/lint/build/browser checks. YAML parsing and job presence were checked locally; the hosted workflow has not been executed. Automatic main-branch deployment was removed. Production promotion, real provider sandbox journeys, production database/file recovery, replica behavior, penetration/accessibility/performance reviews and business/clinical sign-offs remain release evidence, not consequences of passing local tests. Both the backend solution and test-project transitive NuGet vulnerability audits reported no vulnerable packages from the configured sources on 15 October 2026.

Provider references checked while implementing: [Stripe Checkout session fields](https://docs.stripe.com/api/checkout/sessions/object), [Google Meet Space schema](https://developers.google.com/workspace/meet/api/reference/rest/v2/spaces), [Google Meet space creation](https://developers.google.com/workspace/meet/api/reference/rest/v2/spaces/create), and [Zoom meeting APIs](https://developers.zoom.us/docs/api/meetings/).
