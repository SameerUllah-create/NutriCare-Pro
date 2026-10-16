# Notification System Architecture

## Goals

NutriCare Pro supports multi-channel notifications for patient, nutritionist, and admin workflows.

Channels:
- In-App
- Email
- SMS

Events:
- Appointment Booked
- Appointment Reminder
- Diet Plan Assigned
- Payment Received
- New Message

## Core Components

`INotificationService`
- Central orchestration contract.
- Builds event-specific messages.
- Dispatches through configured channels.
- Persists in-app notifications.

`ISmsService`
- SMS provider abstraction.
- Current implementation supports Twilio through configuration.
- Logs simulated delivery when Twilio settings are empty.

`IEmailService`
- Existing SendGrid-backed email abstraction.
- Logs simulated delivery when SendGrid API key is empty.

`NotificationsController`
- Lists in-app notifications.
- Returns unread counts.
- Marks one or all notifications as read.
- Triggers appointment reminders manually for admin/nutritionist workflows.

## Event Producers

Current producers:
- `AppointmentsController`: sends `AppointmentBooked`.
- `DietPlansController`: sends `DietPlanAssigned`.
- `PaymentsController`: sends `PaymentReceived` when status is `Completed`.
- `ChatController`: sends `NewMessage`.
- `NotificationsController`: triggers `AppointmentReminder`.

## Configuration

Channels are controlled per event in `appsettings.json`.

Example:

```json
"Notifications": {
  "Events": {
    "AppointmentReminder": {
      "Channels": [ "InApp", "Email", "SMS" ]
    }
  }
}
```

## Scaling Plan

Current implementation dispatches synchronously after business actions. For production scale:

1. Add a `NotificationOutbox` table with event payload, status, retry count, and next attempt time.
2. Write notification events to the outbox inside the same transaction as the business action.
3. Process outbox records using a hosted background worker or Azure Service Bus worker.
4. Add idempotency keys per event recipient/channel to prevent duplicate sends.
5. Store channel delivery attempts separately for audit, retries, and provider diagnostics.
6. Move template rendering into a versioned template store for localization and A/B testing.

## Reliability Notes

- In-app notifications are stored in SQL and indexed by user/channel/read state.
- Email and SMS providers are optional in development.
- Failed provider sends are returned as `NotificationDispatchResult` and logged.
- Private dashboards consume in-app notification counts through authenticated APIs.
