# API Summary - Epic 2998 (v1)

All endpoints are versioned under `/api/v1` and return JSON. Errors use the shape
`{ "code": "<DOMAIN_CODE>", "message": "<human readable>" }`.

| Method | Path | Work item | Description |
| --- | --- | --- | --- |
| GET | `/health` | - | Liveness probe |
| GET | `/health/ready` | - | Readiness probe including database connectivity |
| GET | `/api/v1/trains/search?source=&destination=&date=` | 3000 | Search trains with schedules and fares |
| GET | `/api/v1/trains/schedules/{scheduleId}/availability` | 3001 | Seat availability per travel class |
| GET | `/api/v1/passengers?userId=` | 3004 | List saved passenger profiles |
| POST | `/api/v1/passengers` | 3004 | Create a passenger profile (identity document stored masked) |
| PUT | `/api/v1/passengers/{id}` | 3004 | Update a passenger profile (owner only) |
| DELETE | `/api/v1/passengers/{id}` | 3004 | Deactivate a passenger profile |
| POST | `/api/v1/bookings` | 3003 | Create a booking; honours the `Idempotency-Key` header |
| GET | `/api/v1/bookings?userId=` | 3003 | List the passenger's bookings |
| GET | `/api/v1/bookings/{bookingReference}` | 3003 | Booking detail including seats and ticket number |
| GET | `/api/v1/bookings/{bookingReference}/refund-quote` | 3007 | Refund percentage and amount under current policy |
| POST | `/api/v1/bookings/{bookingReference}/cancel` | 3007 | Cancel a booking, release seats and process the refund |
| POST | `/api/v1/payments/intents` | 3006 | Create a payment intent for a held booking |
| POST | `/api/v1/payments/webhook` | 3006 | Signed PSP settlement callback (HMAC-SHA256, 401 when unverified) |
| GET | `/api/v1/notifications?bookingReference=` | 3009 | Notification history and delivery receipts |
| POST | `/api/v1/support-requests` | 3010 | Raise a support request |
| GET | `/api/v1/support-requests?userId=` | 3010 | List the passenger's support requests |
| GET | `/api/v1/support-requests/{reference}` | 3010 | Support request detail with the full update trail |
| PATCH | `/api/v1/support-requests/{reference}` | 3010 | Update status, add a note and record the resolution |

## Status codes

| Status | When |
| --- | --- |
| 200 | Successful read or state transition |
| 201 | Booking, passenger profile or support request created |
| 204 | Passenger profile deactivated |
| 400 | Domain validation failure (`INVALID_BOOKING`, `TOO_MANY_PASSENGERS`, `INVALID_PROFILE`) |
| 401 | `INVALID_SIGNATURE` on the payment webhook |
| 403 | `FORBIDDEN` - resource owned by another passenger |
| 404 | `BOOKING_NOT_FOUND`, `SCHEDULE_NOT_FOUND`, `PROFILE_NOT_FOUND`, `SUPPORT_REQUEST_NOT_FOUND` |
| 409 | `INSUFFICIENT_SEATS`, `HOLD_EXPIRED` |
| 500 | Unhandled error (`INTERNAL_ERROR`) |
