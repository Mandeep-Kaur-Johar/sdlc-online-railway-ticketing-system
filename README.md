# Online Railway Ticketing System

Implementation of Azure DevOps **Epic 2998 - Online Railway Ticketing System**.

| Item | Value |
| --- | --- |
| Epic | 2998 - Online Railway Ticketing System |
| Architecture | `docs/architecture/epic-2998/System-Design-Document.md` |
| Backend | .NET 8, ASP.NET Core Web API, Entity Framework Core 8 |
| Frontend | React 18, TypeScript 5.5, Vite 5, Vitest |
| API version | `/api/v1` |

## Repository layout

```text
RailwayTicketing.sln
src/backend/RailwayTicketing.Api      # ASP.NET Core 8 Web API
src/frontend                          # React 18 + TypeScript client
tests/RailwayTicketing.Tests          # xUnit unit and integration-style tests
docs/architecture/epic-2998           # Approved System Design Document and diagrams
.github/workflows/ci.yml              # Build, test and type-check pipeline
```

## Backlog traceability

| Work item | Requirement | Implementation | Tests |
| --- | --- | --- | --- |
| 3000 | Search trains between stations | `TrainSearchService.SearchAsync`, `GET /api/v1/trains/search`, `SearchPage` | `TrainSearchServiceTests` |
| 3001 | View seat availability by class | `TrainSearchService.GetAvailabilityAsync`, `GET /api/v1/trains/schedules/{id}/availability`, `AvailabilityTable` | `TrainSearchServiceTests`, `format.test.ts` |
| 3003 | Book railway tickets | `BookingService.CreateAsync`, `POST /api/v1/bookings` (Idempotency-Key), `PassengerFields` | `BookingServiceTests` |
| 3004 | Manage passenger profiles | `PassengerProfileService`, `/api/v1/passengers`, `ProfilesPage` | `PassengerProfileServiceTests` |
| 3006 | Pay for bookings | `PaymentService`, `/api/v1/payments/intents`, signed `/api/v1/payments/webhook` | `PaymentServiceTests` |
| 3007 | Cancel tickets and receive refunds | `BookingService.CancelAsync`, `RefundCalculator`, `BookingsPage` | `RefundAndCancellationTests` |
| 3009 | Receive booking notifications | `NotificationService`, `/api/v1/notifications`, `NotificationsPage` | `NotificationServiceTests` |
| 3010 | Raise support requests | `SupportService`, `/api/v1/support-requests`, `SupportPage` | `SupportServiceTests` |

## Business rules enforced

- Seat hold timeout of **15 minutes**, released by the `SeatHoldExpiryJob` every **5 minutes**.
- Maximum of **6 passengers** per booking.
- Refund policy bands: **48h+ = 100%**, **24h+ = 75%**, **12h+ = 50%**, **4h+ = 25%**, otherwise **0%**.
- A booking is confirmed and a ticket issued **only** after a payment intent settles successfully.
- Payment webhooks are verified with **HMAC-SHA256**; unverified callbacks are rejected with **HTTP 401**.
- Booking creation is idempotent through the `Idempotency-Key` request header.

## Running locally

Backend (defaults to the in-memory provider when `ConnectionStrings:RailwayDb` is empty):

```bash
dotnet restore RailwayTicketing.sln
dotnet run --project src/backend/RailwayTicketing.Api   # http://localhost:5080, Swagger at /swagger
```

Frontend:

```bash
cd src/frontend
npm install
npm run dev      # http://localhost:5173
```

## Tests

```bash
dotnet test RailwayTicketing.sln
cd src/frontend && npm test
```

No secrets are committed. `Payments:WebhookSecret`, `ConnectionStrings:RailwayDb` and provider URLs are supplied
at runtime from Azure Key Vault references as described in Section 4.2 of the System Design Document.
