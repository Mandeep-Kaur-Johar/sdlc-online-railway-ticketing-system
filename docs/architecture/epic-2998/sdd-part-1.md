# **1. Introduction**

## **1.1 Purpose**

Passengers today lack a single, reliable online channel to search trains, reserve seats, pay, and manage the full ticket lifecycle for the Online Railway Ticketing System (Azure DevOps Epic 2998). This System Design Document defines the implementation-ready design for a platform that supports train search, seat availability, booking, passenger profile management, online payment, cancellation and refund, notifications, and customer support, as described in Features 2999, 3002, 3005, and 3008.

**Business Impact & Key Decisions:** Delivering a self-service digital booking channel reduces counter load, increases confirmed-booking conversion, and gives the railway operator auditable records of every reservation, payment, refund, and support interaction. The key architectural decision is a layered React + ASP.NET Core (.NET 8) solution on Azure App Service backed by a single transactional Azure SQL Database, chosen because the booking, payment, and refund flows require strong consistency on seat inventory rather than eventual consistency.

## **1.2 Scope**

- **Functional Scope**: Train search between source and destination stations with schedules and fares (Story 3000), seat availability by travel class refreshed against current inventory (Story 3001), multi-passenger booking with seat reservation and booking reference generation (Story 3003), saved passenger profiles with create/update/select (Story 3004), online payment where only successful payments confirm tickets (Story 3006), cancellation with policy-based refund calculation and visible cancellation status (Story 3007), booking/cancellation/schedule-change notifications over email and SMS (Story 3009), and logged, tracked support requests through closure (Story 3010).
- **Non-Functional Scope**: HTTPS-only transport using TLS 1.2 or higher, JSON Web Token (JWT) authentication, role-based authorization for Passenger and Support Agent roles, secret storage in Azure Key Vault, telemetry in Application Insights and Log Analytics, PCI DSS-aligned payment handling in which no raw card data is stored by this system, and personally identifiable information (PII) protection for passenger name, age, gender, email, and phone number.
- **In-Scope Integrations**: External Payment Gateway over REST/HTTPS for authorization, capture, and refund; external Email/SMS Provider over REST/HTTPS for notification delivery; Azure Key Vault for secrets; Application Insights for telemetry.
- **Out-of-Scope Items**: Native iOS and Android mobile applications, railway operations and train scheduling master-data administration consoles, dynamic fare/yield pricing engines, loyalty programs, seat-map graphical selection, and offline or counter-based ticket sales. These are excluded because no Feature or User Story under Epic 2998 requires them.
- **Assumptions & Constraints**: Train, station, schedule, travel-class, and fare master data is loaded into the Azure SQL Database by an upstream railway master-data process (assumption, because no story defines master-data maintenance). A merchant account with a PCI DSS compliant payment gateway supporting hosted payment pages and refund APIs is available. The refund policy slab table is supplied by the business and is configuration-driven rather than hard-coded.

## **1.3 Methodology**

The program follows an Agile Scrum delivery model with two-week sprints, where Epic 2998 decomposes into four Features and eight User Stories that are sized, refined, and pulled into sprints with their acceptance criteria as the definition of done. Design work is governed by a lightweight review process: this System Design Document is reviewed by the architecture, security, and QA leads before the first implementation sprint, and every material change is captured as an Architecture Decision Record (ADR) stored alongside this document in the repository. Tooling consists of Azure DevOps for the Epic/Feature/Story hierarchy and traceability, GitHub for source code, Mermaid-rendered PNG diagrams and this document, GitHub Actions for continuous integration and deployment, and Azure Monitor for post-release validation. Diagrams are generated as Mermaid source (`.mmd`) and rendered to PNG so that both human-readable images and version-controlled, diff-able source are retained.

# **2. System Architecture**

## **2.1 Software Architecture**

The solution uses a classic layered architecture with four logical tiers. The presentation tier is a React with TypeScript single-page application hosted on Azure Static Web Apps, containing the Train Search UI, Booking UI, Passenger Profile UI, Payment UI, and Support Request UI. The security tier sits in front of every backend call: JWT Authentication validates the bearer token issued at sign-in, and Role-Based Authorization enforces that Passengers may only act on their own bookings while Support Agents may read bookings linked to support requests. The application tier is an ASP.NET Core (.NET 8) Web API exposing six functional API groups - Train Search API, Booking API, Passenger Profile API, Payment API, Notification API, and Support API - each delegating to domain services that own business rules and to repositories that own persistence. The data tier is a single Azure SQL Database holding stations, trains, schedules, travel classes, seat inventory, user accounts, passenger profiles, bookings, booking passengers, payments, cancellations, notifications, and support requests. All browser-to-API and API-to-external traffic is HTTPS/JSON; API-to-database traffic uses the TDS protocol over an encrypted connection. The Payment API integrates with the external Payment Gateway and the Notification API integrates with the external Email/SMS Provider, both over REST/HTTPS with secrets retrieved from Azure Key Vault.

`- !Solution Architecture`

![Solution Architecture](solution-architecture.png)

*Caption - Solution Architecture:* Shows how a passenger request flows from the React UI through the JWT authentication and role-based authorization layer into the six .NET 8 API groups, down to the Azure SQL Database, and outward to the Payment Gateway and Email/SMS Provider. Its business meaning is that no booking, payment, or refund can occur without an authenticated and authorized identity, and that every money-moving action is isolated in the Payment API where gateway credentials from Key Vault are used.

*Textual description (fallback):* The passenger reaches a React UI over HTTPS; every API call carries a JWT that is validated and role-checked before reaching any of the six API groups; five API groups persist to one Azure SQL Database; the Payment API alone talks to the Payment Gateway and the Notification API alone talks to the Email/SMS Provider. The Mermaid source is version controlled at `docs/architecture/epic-2998/solution-architecture.mmd`.

**System Context:** The system's external boundary consists of two human actors (Passenger, Support Agent) and two external systems (Payment Gateway, Email/SMS Provider).

![System Context](system-context.png)

*Caption - System Context:* Establishes that the Online Railway Ticketing System is the single system of record for bookings while money movement and message delivery are delegated to external providers, which bounds the compliance surface for PCI DSS and for telecom/email deliverability. Mermaid source: `docs/architecture/epic-2998/system-context.mmd`.

# **3. Detailed Design**

## **3.1 Software Detailed Design**

The backend is organized as Controllers to Domain Services to Repositories to Azure SQL Database, which keeps HTTP concerns, business rules, and persistence independently testable. The controller layer contains `TrainSearchController`, `BookingController`, `PassengerProfileController`, `PaymentController`, `NotificationController`, and `SupportController`, each performing model validation and mapping to data transfer objects (DTOs). The domain service layer contains `TrainSearchService` (matches trains by source station, destination station, and travel date and returns schedules and fares per Story 3000), `SeatAvailabilityService` (returns available seats per travel class from `SEAT_INVENTORY`, per Story 3001), `BookingService` (validates passengers, reserves seats, generates a booking reference, per Story 3003), `PassengerService` (create/update/select saved profiles, per Story 3004), `PaymentService` (orchestrates gateway authorization and confirms or releases the booking, per Story 3006), `RefundService` (applies the configurable refund-policy slab and records refund status, per Story 3007), `NotificationService` (composes and dispatches email and SMS for booking, cancellation, and schedule change, per Story 3009), and `SupportService` (logs, tracks, and closes support requests, per Story 3010). The repository layer uses Entity Framework Core 8 with explicit transactions.

Key algorithms and contracts: seat reservation uses a pessimistic concurrency pattern - a single SQL transaction performs `UPDATE SEAT_INVENTORY SET AvailableSeats = AvailableSeats - @n WHERE SeatInventoryId = @id AND AvailableSeats >= @n`, and a zero-row result raises a `SeatUnavailableException` that returns HTTP 409 Conflict, which prevents overbooking without distributed locks. The booking reference is a 10-character uppercase alphanumeric code generated from a cryptographically secure random source and persisted under a unique index. The refund amount is computed as `RefundAmount = TotalFare - CancellationCharge(slab)` where the slab is selected by hours-to-departure from configuration. Representative API contracts are `GET /api/trains/search?source={code}&destination={code}&date={yyyy-MM-dd}`, `GET /api/schedules/{scheduleId}/availability`, `POST /api/bookings`, `GET /api/bookings/{bookingReference}`, `POST /api/payments`, `POST /api/bookings/{bookingReference}/cancel`, `GET|POST|PUT /api/passenger-profiles`, and `POST /api/support-requests`. Error handling is centralized in an exception-handling middleware that emits RFC 7807 `application/problem+json` responses and never leaks stack traces or gateway credentials. Idempotency is enforced by requiring an `Idempotency-Key` header on `POST /api/bookings` and `POST /api/payments`; a replayed key returns the original result instead of double-charging. Outbound gateway and notification calls use Polly with three retries and exponential backoff (2s, 4s, 8s) plus a circuit breaker that opens after five consecutive failures. Caching applies only to slow-changing reference data - station and train master data is cached in-memory for 60 minutes and invalidated on master-data refresh - while seat availability is never cached because Story 3001 requires current inventory.

`- !Critical Workflow Sequence`

![Critical Workflow Sequence](sequence-booking-payment.png)

*Caption - Critical Workflow Sequence:* Traces the end-to-end search, book, pay, confirm and notify flow, including the failure branch. It matters commercially because it encodes the rule from Story 3006 that failed payments must never produce a confirmed ticket and must release held seats back to inventory. Mermaid source: `docs/architecture/epic-2998/sequence-booking-payment.mmd`.

**Component Decomposition:**

![Component Diagram](component-diagram.png)

*Caption - Component Diagram:* Maps each UI module to its controller, domain service, and repository so that a developer can pick up any User Story and know exactly which classes to create or modify. Mermaid source: `docs/architecture/epic-2998/component-diagram.mmd`.

**Data Model:**

![Data Model](data-model.png)

*Caption - Data Model:* Defines thirteen entities - `STATION`, `TRAIN`, `TRAIN_SCHEDULE`, `TRAVEL_CLASS`, `SEAT_INVENTORY`, `USER_ACCOUNT`, `PASSENGER_PROFILE`, `BOOKING`, `BOOKING_PASSENGER`, `PAYMENT`, `CANCELLATION`, `NOTIFICATION`, and `SUPPORT_REQUEST` - which together cover every data requirement stated in the eight User Stories. `SEAT_INVENTORY` is keyed by schedule and travel class so availability is answerable in one indexed read, and `BOOKING_PASSENGER` resolves the many-to-many relationship that makes multi-passenger booking possible. Mermaid source: `docs/architecture/epic-2998/data-model.mmd`.
