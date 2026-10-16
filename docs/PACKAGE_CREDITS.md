# Package credit foundation

Updated 17 October 2026. Package checkout and public sales remain disabled. Proposed configuration and clinician approval are described in [PACKAGE_CONFIGURATION.md](PACKAGE_CONFIGURATION.md). This foundation does not create a purchase, charge a patient or approve the proposed terms.

The internal entitlement service issues at most one enrollment for an existing verified package payment. It checks the payment event evidence, amount/currency, reviewed offer and purchase dates, then saves an immutable package-definition snapshot. Missing or changed purchase evidence requires reconciliation. The application does not expose a public purchase or enrollment-creation endpoint.

Consultation reservations use the existing clinician booking transaction and a package-specific SQL application lock. Unique resource indexes prevent an appointment or plan version using another credit twice. Reserving holds capacity; completing a consultation consumes it. The first completed initial consultation activates the package, and its recorded completion time starts the configured credit-expiry period. The first consultation must be scheduled inside the configured purchase window. Follow-ups require activation; full consultation intervals must fit within validity. Booking prices and package benefits remain snapshots when later configuration changes.

Eligibility is enforced using configured service codes and session durations. The package's approving clinician must match the booking clinician. Provider cancellations release credits according to the purchased restoration setting. Patient cancellation and no-show handling use the approved cancellation-policy snapshot. Missing or unconfirmed credit decisions hold the credit for review; the proposed policies do not automatically consume or release it. Cancelling one package appointment never refunds the entire package payment. Pending or settled package refunds prevent new spending while previously assigned plans remain stored.

The ledger supports separate diet-plan and revision quotas using unique published plan-version IDs. Complete package ordering, patient selection of a package during plan authoring, and provider sandbox fulfillment must be integrated and accepted before sales are enabled. Existing plan publication does not silently choose a patient's package or deduct its plan credits.

`GET /api/package-enrollments` and `GET /api/package-enrollments/{id}` show existing enrollments: patients receive only their own, while administrators receive operational billing records. The list is paginated. `POST /api/package-enrollments/{id}/extend` is Admin-only and requires positive days, an explicit reason and the current SQL row version. Before activation it extends the first-consultation deadline; after activation it extends credit expiry. Every extension records the actor, reason, previous date and new date. An extension does not restore already consumed credits, change the purchased definition or grant clinical access.

The .NET 10 build completed with zero warnings and zero errors. On 17 October 2026, the complete backend suite passed **206/206 tests with no skips**, including **131 non-SQL tests and 75 SQL tests**. The package-specific per-file results in [verified.trx](../.artifacts/backend-tests/verified.trx) are:

| Test file | Type | Passed |
| --- | --- | ---: |
| `PackageConfigurationRulesTests.cs` | Non-SQL | 14 |
| `SqlPackageConfigurationTests.cs` | SQL | 7 |
| `SqlPackageCreditTests.cs` | SQL | 12 |
| `SqlPackageEnrollmentHttpTests.cs` | SQL | 4 |

The 12 credit-ledger cases passed the eight-way reservation race, idempotent retries and credit-kind conflicts, purchase deadlines and full-session expiry, reschedule and enclosing-booking rollback, completion-only activation using purchased terms, three explicit no-show decisions, provider restoration without a whole-package refund, ownership/service/actor checks, pending-refund spending restrictions, and separate plan/revision quotas. The four enrollment HTTP cases passed patient/Admin scoping and authorized extension checks, including the required reason and row version.

Public package checkout, package selection during clinical plan authoring, and real provider sandbox fulfillment remain unwired or unaccepted before sales can be enabled. Unpaid booking holds/expiry and notification provider delivery receipts remain separate integration work. Passing these local tests does not establish live payment/refund or notification delivery acceptance. See [INTEGRATIONS_IMPLEMENTATION.md](INTEGRATIONS_IMPLEMENTATION.md) and [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md) for integration and release status.
