# Clinical implementation and acceptance evidence

Updated 17 October 2026. This records the clinical/API work against FR-03 through FR-06 and associated booking, privacy and record-access requirements. It is implementation evidence, not clinical, legal or production approval. The SRS release gates are not marked complete by this document.

## Delivered behavior

- A shared clinical-access service checks patient ownership, author/appointment participation, active clinician assignments and explicit assignment revocation. Administrative roles alone do not permit clinical record access; the separately controlled `clinical_access` permission is required. Patient-facing plan reads return published snapshots, never a later unpublished edit.
- Published service/calendar rules determine availability. Draft services, hours and exceptions do not become live automatically. Slots use UTC instants and explicit Pakistan time presentation, complete interval overlap checks and one configured buffer between appointments. Query ranges are bounded to 31 days. Working-hour rules, service effective dates, closures and exceptional openings are checked again during booking. A rule or exceptional opening must be effective for the entire appointment interval; UTC activation/expiry instants are not compared to Pakistan calendar dates. Closures apply to the intersection of their scheduled and effective intervals, including a closure starting during an appointment.
- Booking and rescheduling use the same SQL transaction-owned application lock as calendar publication. The lock is also acquired when an outer transaction already exists. Booking request keys make equivalent retries return the original reservation; a reused key with different booking details is rejected. Reservation, assignment, history and per-channel notification outbox writes commit together.
- A booking requires a verified clinician, published service/calendar/cancellation policy, owned intake and current explicit health-data consent. Video/online/phone consultations additionally require teleconsultation consent. No approved policy text or publication is invented from the proposed configuration.
- The reservation preserves its price/currency and exact cancellation-policy snapshot. Self-service rescheduling uses the booked policy's cutoff and count. Changes retain appointment history and invalidate old API video links; provider-side revocation is handled by the integration lifecycle. Confirmed, cancellation, completion and no-show transitions are guarded.
- Reservations also preserve the effective service/calendar booking buffer. Pairwise conflict checks use the larger buffer of the requested and existing reservations, in both directions. Rescheduling excludes the moved row while retaining at least its original buffer against other reservations. Older rows without a snapshot use the current service buffer and largest published calendar buffer as a conservative fallback; this cannot reconstruct a historical buffer after past configuration changes.
- Intake submissions append a dated version. Consent acceptance records the policy version/title, both approved English and Urdu texts, presented language and timestamp. Grant receipt reuse requires matching current text and language; invalid languages and missing approved Urdu translations are rejected. Health-data withdrawal uses the same patient transaction/application lock as intake submission, ends applicable clinician assignments and records ended assignment markers for legacy appointment/published-plan relationships without assignment history. These markers prevent historical records from restoring inferred clinician access. Repeating withdrawal does not duplicate those markers. New consent-dependent collection is blocked after withdrawal.
- Consultation notes and report reviews append author/date/history records. Report uploads compare MIME type, extension and actual signature; enforce limits; store privately; and attempt compensating blob deletion if SQL persistence fails. Authorized report streaming uses the same clinical-access service.
- Plans have draft creation, expected-version edits, publication, archive, review dates and immutable content snapshots. An author cannot reassign a plan to another patient by changing an ID. Meal replacements retain prior rows as deleted records; published snapshots preserve the exact previous plan. Publication stages a generic alert without including clinical details externally.
- PDF generation uses a local, isolated, network-disabled Chromium process with JavaScript disabled. The printable document contains all meals, times, portions, instructions, review dates and estimated nutrition, with Unicode text, Urdu shaping, pagination, headings/table semantics and tagged output. Patient text is HTML-encoded. PDF tags alone do not certify assistive-technology or PDF/UA conformance.
- Progress supports body measurements, dated charts, clinician-selected metrics, calculated-BMI provenance and correction history. Historical entries do not replace a newer current weight. Corrections require the current row-version token and a reason; prior values are preserved.
- Patient summaries choose the latest eligible published plan rather than a future/expired plan or unpublished draft. Clinician day/month boundaries use the configured time zone. Operational revenue summaries explicitly report PKR and disclose excluded currencies rather than adding unlike currencies together.

## Endpoint contracts

Existing appointment, diet-plan, progress and medical-report routes remain available with ownership checks. New or extended contracts include:

| Route | Purpose / required inputs |
| --- | --- |
| `GET /api/availability/{nutritionistId}/slots` | `serviceId`, offset-bearing `fromDate` and `toDate`; UTC slot results; no cache of stale availability. |
| `POST /api/appointments` | Existing fields plus `serviceId`, `intakeFormId`, `timeZoneId`, and a stable `idempotencyKey`. |
| `GET /api/clinical/patients` | Active assigned-patient queue for the signed-in clinician. |
| `GET/POST /api/clinical/consents` | Ledger or acceptance with `purpose`, `policyVersion`, `accepted: true`, and `language: en/ur`. |
| `POST /api/clinical/consents/{id}/withdraw` | Withdraw the caller's own consent record. |
| `GET/POST /api/clinical/intake` | Own intake history/submission; clinician reads require an authorized `patientId`. Submission includes `consentRecordId`. |
| `GET /api/clinical/appointments/{id}/history` | Authorized appointment state/schedule history. |
| `GET /api/clinical/appointments/{id}/notes` | Authorized dated consultation-note history. |
| `POST /api/clinical/appointments/{id}/no-show` | Assigned clinician; only after a confirmed appointment has ended. |
| `POST /api/clinical/plans/{id}/publish` | Assigned author; `expectedVersion` of the reviewed draft. |
| `POST /api/clinical/plans/{id}/archive` | Assigned author; `expectedVersion`. |
| `GET /api/clinical/plans/{id}/versions` | Authorized history; patients receive published revisions only. |
| `GET /api/clinical/plans/{id}/adherence` | Authorized patient/author adherence review. |
| `PUT /api/clinical/patients/{patientId}/metrics` | Assigned clinician chooses supported `metrics`. |
| `GET /api/clinical/progress/{id}/history` | Authorized measurement-correction snapshots. |
| `GET /api/clinical/reports/{id}/notes` | Authorized dated report-review history. |

Plan create/update requests include `reviewDate`; updates also require integer `expectedVersion`. Progress corrections use the base64 SQL `rowVersion` from the response as `expectedVersion` and supply `changeReason`.

## Additional administrator hardening

The delegated administrator controller uses mapped soft-delete predicates and denies its clinical patient list without explicit permission. The operational user list remains the appropriate account-support interface. The obsolete verification toggle returns a conflict directing callers to the evidence/approval workflow. Blog creation/edits save drafts, require concurrency checks for edits, and use a separate explicit publication action. Saved article changes/publication retain configuration history. PKR revenue analytics recognize gross payments on verified payment dates and settled refunds on refund dates, using Pakistan calendar days; other currencies are excluded and counted.

## Verification

The backend build passed with zero warnings and zero errors on 17 October 2026. The complete regression suite passed **206/206 tests with no skips: 131 non-SQL and 75 SQL**. Evidence is in [verified.trx](../.artifacts/backend-tests/verified.trx).

Clinical coverage includes **20 clinical unit cases, five mixed-service-buffer unit cases, 17 clinical reservation SQL cases and 13 calendar-publication SQL cases**, plus the actual local tagged, multi-page Unicode PDF check within the clinical unit group. These checks cover draft gating, full interval/buffer rules, Pakistan conversion and effective-date boundaries, ownership/revocation/Admin policy, consent evidence, progress backfill/corrections, immutable plan visibility, publication races and HTML encoding/completeness.

The mixed-service-buffer cases passed in `MixedServiceBufferTests.cs` and `SqlMixedServiceBufferTests.cs` (the latter extends the clinical SQL fixture). They verify larger buffers before/after an existing reservation, exact gap boundaries, cancelled/ignored reservations, preserved buffers after service unpublication/edits, calendar-buffer snapshotting and rescheduling against other reservations. The tracked migration containing `BookingBufferMinutesSnapshot` was exercised by the SQL run.

The SQL suite in `tests/NutriCare.Tests/Clinical/SqlClinicalReservationTests.cs` covers concurrent reservations/outbox, idempotent retries, full interval/buffer checks, closure-publication races, an existing transaction's application lock, immutable booked policy, cross-account/revoked reads, Urdu consent evidence, persisted plan revisions/meal retention, and persisted progress corrections.

The final run also passed these regressions:

- `PakistanMidnightRuleUsesUtcEffectiveInstantsAcrossDateBoundary`: a 00:00 Pakistan rule whose UTC effective window falls on the preceding date remains bookable.
- `MiddayRuleActivationAndExpiryContainTheWholeAppointment`: generation and booking reject appointments before activation or ending after expiry, while accepting an appointment ending exactly at expiry.
- `ExceptionalOpeningRequiresTheWholeAppointmentInsideItsEffectiveWindow`: exceptional openings enforce the same interval bounds.
- `ClosureBecomingEffectiveDuringAppointmentBlocksTheInterval`: an overlapping closure effective after appointment start still blocks that appointment.
- `UrduConsentRequiresTheExactCurrentlyApprovedTranslation`: clinical consent checks reject an outdated or missing Urdu translation even when the English text and version match. The same comparison applies to booking and intake.
- `Health_data_withdrawal_revokes_legacy_relationships_without_assignment_history`: two SQL cases, one for a completed legacy appointment and one for a published legacy plan, verify durable denial and idempotent ended markers after withdrawal.
- `Withdrawal_and_intake_share_the_patient_lock_and_reject_collection_after_withdrawal`: a held SQL withdrawal lock blocks a competing intake submission; after withdrawal commits, the submission is rejected without creating another intake version.
- `Retrying_an_old_withdrawal_does_not_revoke_newly_consented_care`: after fresh consent, intake and a new booking establish a new assignment, retrying the old withdrawal remains a no-op.

The actual-browser PDF case was enabled with a local Chromium executable; it was not skipped. The same full run passed the checksummed SQL backup/restore and upgrade through all three tracked migrations. The recovery rehearsal preserved an owner-edited legacy Starter price of PKR 4250 while adding three separate proposed v2 package drafts, all unpublished. These disposable-database checks do not establish production recovery objectives or approve adoption of an existing populated application database.

Run only against a designated development SQL instance using `NUTRICARE_TEST_SQL_CONNECTION`; the fixture creates and removes its own `NutriCareTest_...` database. Do not point this variable at an application database. The fixture's cleanup validates its generated database name.

For the actual PDF test, set `NUTRICARE_PDF_BROWSER` to an installed compatible local Chromium executable. `NUTRICARE_PDF_ARTIFACT` optionally saves the synthetic test PDF. The application renderer otherwise uses the installed Playwright browser or `ClinicalPdf:ExecutablePath`. Docker/runtime provisioning must include the matching browser and Urdu-capable fonts.

## Remaining acceptance boundaries

- Proposed services, fees, hours and cancellation rules remain draft until the authorized owner explicitly publishes reviewed, effective configuration. Real credential evidence and approved bilingual privacy/teleconsultation text are external inputs.
- Paid cancellation records policy eligibility and notifications, but automatic provider refund/reconciliation must be proven through the integration workflow; a recorded eligibility decision is not evidence that money was refunded.
- Real provider delivery, existing provider-side meeting-link revocation, production storage privacy, file lifecycle reconciliation/retention, and data-rights operations need their respective integration/operations acceptance.
- Adoption of an existing populated legacy database, production database/file recovery objectives, all-role full-stack browser journeys, screen-reader/keyboard/mobile review, Urdu proofreading, accessible PDF reading order, independent security assessment, production load/uptime and clinician/privacy sign-off remain release gates. Successful local regression and disposable-database recovery tests do not certify them.
- Legacy bookings lacking traceable service/intake/consent/policy snapshots need an explicit migration/reconciliation decision; the application does not invent policy history for them.
