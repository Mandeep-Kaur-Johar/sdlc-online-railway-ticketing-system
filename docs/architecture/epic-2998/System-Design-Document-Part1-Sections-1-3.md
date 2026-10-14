# **System Design Document - Online Railway Ticketing System**

**Source Requirement:** Azure DevOps Epic 2998 - Online Railway Ticketing System
**Source ID:** 2998
**Repository:** Mandeep-Kaur-Johar/sdlc-online-railway-ticketing-system
**Architecture Folder:** docs/architecture/epic-2998/

# **1. Introduction**

## **1.1 Purpose**
Passengers today lack a single reliable digital channel to search trains, reserve seats, pay securely, manage ticket lifecycle events, and obtain support, which drives counter queues, abandoned bookings and high support cost. Epic 2998 - Online Railway Ticketing System delivers a platform for searching trains, booking railway tickets, payments, ticket management, notifications and customer support, so that passengers complete an end-to-end journey purchase online without manual intervention.

The system consolidates four business capabilities defined in the backlog: Train Search and Availability (Feature 2999), Booking and Passenger Management (Feature 3002), Payments and Ticket Lifecycle (Feature 3005), and Notifications and Support (Feature 3008). Each capability is realised by a dedicated service boundary so that search traffic peaks do not degrade payment or booking reliability. The design keeps seat inventory authoritative in a single transactional store while serving read-heavy search traffic from a cache, which is the principal performance decision of this document.

**Business Impact & Key Decisions:** Moving booking online reduces counter dependency and shortens time-to-ticket; separating payment settlement from seat reservation through an idempotent, event-driven confirmation flow prevents the most expensive failure mode in ticketing systems - money captured without a confirmed seat.

## **1.2 Scope**
**Functional Scope:** Train search by source, destination and date with schedules and fares (User Story 3000); seat availability by travel class refreshed against current inventory (User Story 3001); ticket booking for one or more passengers with seat reservation and booking reference generation (User Story 3003); saved passenger profiles for faster booking (User Story 3004); online payment through supported payment methods where only successful payments confirm bookings (User Story 3006); ticket cancellation with policy-based refund calculation and visible cancellation status (User Story 3007); booking, cancellation and schedule-change notifications by email and SMS (User Story 3009); and logged, tracked support requests maintained until closure (User Story 3010).

**Non-Functional Scope:** HTTPS-only transport with Transport Layer Security (TLS) 1.2 or higher, token-based authentication for the frontend using JSON Web Tokens (JWT), role and permission checks enforced in the backend, encryption at rest for all passenger and payment-reference data, Payment Card Industry Data Security Standard (PCI DSS) scope minimisation by never storing raw card data, personally identifiable information (PII) tagging and retention controls, and full audit logging of booking, payment and refund state transitions.

**In-Scope Integrations:** An external payment service provider (PSP) invoked over HTTPS with signed webhook callbacks for settlement confirmation; an email and Short Message Service (SMS) provider for passenger notifications; Azure Service Bus for asynchronous booking-event propagation; Azure Key Vault for secrets; and Application Insights with Log Analytics for observability.

**Out-of-Scope Items:** Native mobile applications (the responsive React web client is the only delivered front end), railway operator timetable authoring tools, loyalty or fare-discount engines, third-party travel-agent reseller APIs, and offline or counter point-of-sale terminals. None of these appear in any Feature or User Story under Epic 2998 and they are therefore explicitly excluded.

**Assumptions & Constraints:** The backlog does not name a specific payment provider, so the design assumes a tokenised, redirect-or-iframe PSP integration with webhook settlement (assumption). Timetable and route master data are assumed to be loaded into the platform's own schedule store from an operator feed (assumption). Azure is assumed as the hosting platform per baseline governance. Refund percentages are policy-driven and configurable because the backlog states only that refunds are calculated per policy.

## **1.3 Methodology**
Delivery follows Agile Scrum with two-week sprints aligned to the four Features of Epic 2998, with each sprint producing a deployable increment behind feature flags. Design review is performed as an architecture review board checkpoint before each Feature enters development, and every significant choice recorded in Section 4 and Section 7 is captured as an Architecture Decision Record (ADR) stored alongside this document in docs/architecture/epic-2998/. Work items remain the system of record in Azure DevOps; code, diagrams and this document are the system of record in GitHub. Diagrams are authored as Mermaid sources, rendered to Portable Network Graphics (PNG) and committed with the document so that every reviewer sees the same version-controlled picture.

Traceability is maintained bidirectionally: each component in Section 2 names the Feature or User Story it satisfies, and Section 8 restates the mapping as a matrix. Changes to acceptance criteria trigger a document revision rather than an informal update, keeping the design deterministic and auditable across the Business Analyst, Architect, Developer and Quality Assurance (QA) handoffs.

# **2. System Architecture**

## **2.1 Software Architecture**
The solution is a layered, service-oriented architecture hosted on Azure. A React 18 single-page application written in TypeScript is delivered through Azure Static Web Apps and communicates only over HTTPS with a JWT bearer token. All traffic enters through Azure Application Gateway with Web Application Firewall (WAF) and is routed by an API gateway layer that applies rate limiting, request validation and correlation-identifier injection before dispatching to backend services.

Five .NET 8 ASP.NET Core services form the backend. The Search Service answers station-to-station queries and class-level availability (Features 2999); the Booking Service owns passenger profiles, seat reservation and booking references (Feature 3002); the Payment Service orchestrates payment intents, settlement webhooks, cancellation and refund calculation (Feature 3005); the Notification Service consumes booking events and dispatches email and SMS messages (Feature 3008); and the Support Service records and tracks support requests through to closure (User Story 3010).

Azure SQL Database is the transactional system of record for trains, schedules, seat inventory, bookings, tickets, payments, refunds and support tickets. Azure Cache for Redis fronts search and availability reads with a short time-to-live so that availability remains near-current while absorbing read peaks. Azure Service Bus carries booking, cancellation and schedule-change events to the Notification Service, decoupling passenger-facing latency from provider delivery latency. Secrets are held in Azure Key Vault and referenced by managed identity; telemetry flows to Application Insights and Log Analytics.

- !Solution Architecture

**Caption:** The solution architecture diagram shows the end-to-end logical view - browser to gateway, gateway to the five domain services, services to the transactional database, cache and message bus, and outward integration to the payment and communications providers. Its business meaning is that search load, booking transactions and payment settlement are isolated from one another so a failure or spike in one capability cannot block ticket issuance in another.

- !Component Diagram

**Caption:** The component diagram decomposes each service into controllers, application services, domain logic and repositories, making the implementation boundaries explicit for the Developer handoff.

**Fallback:** If images cannot be rendered, the architecture is: Browser (React/TypeScript) to Application Gateway (WAF, TLS 1.2+) to API Gateway (JWT validation, 100 requests per second limit) to {Search, Booking, Payment, Notification, Support} services; Search and Booking read/write Azure SQL; Search reads Azure Cache for Redis; Booking publishes to Azure Service Bus topic booking-events consumed by Notification; Payment calls the external PSP over HTTPS and receives signed webhooks; all services emit telemetry to Application Insights.

# **3. Detailed Design**

## **3.1 Software Detailed Design**
Each backend service uses a consistent layering of API controllers, application services, domain entities and Entity Framework Core repositories, which keeps business rules out of transport code and makes unit testing straightforward. The Search Service exposes GET /api/v1/trains/search with source, destination and travel-date parameters returning train number, schedule, stops, class-wise fare and availability, satisfying the acceptance criterion that matching trains with schedules and fares are displayed. Results are cached in Redis under a composite key of origin, destination and date with a 60-second time-to-live, and availability counters are invalidated immediately on any reservation or cancellation commit so displayed availability reflects current inventory as required by User Story 3001.

The Booking Service exposes POST /api/v1/bookings, which validates passenger payloads (one or more passengers per booking), takes a row-level pessimistic lock on the seat-inventory record for the requested train, date and class, decrements the available count inside a single database transaction, persists passenger rows and returns a generated human-readable booking reference such as RLY-2998-000123. Passenger profile management is served by GET, POST and PUT on /api/v1/passenger-profiles so that saved profiles can be created, updated and selected at booking time per User Story 3004. Every write endpoint accepts an Idempotency-Key header; a replayed key returns the original result instead of creating a duplicate booking.

The Payment Service creates a payment intent against the PSP, holds the booking in a Pending state, and only transitions the booking to Confirmed and issues a ticket when a signature-verified settlement webhook or a successful server-side verification is received - directly enforcing the acceptance criterion that failed payments do not create confirmed tickets. Pending bookings that receive no settlement within 15 minutes are expired by a background reconciliation job that releases the held seats back to inventory. Cancellation calls POST /api/v1/tickets/{id}/cancel, evaluates the configurable refund-policy table by hours-before-departure band, writes a Refund record and exposes cancellation status to the passenger, satisfying User Story 3007.

Error handling is standardised on RFC 7807 problem-detail responses with a correlation identifier echoed in every response header and log entry. Transient downstream failures use Polly-based retry with exponential backoff (three attempts, base delay 200 milliseconds) plus a circuit breaker that opens after five consecutive failures for 30 seconds, preventing retry storms against the PSP or communications provider. Notification dispatch is at-least-once with provider-side deduplication keys, and failed messages after retry exhaustion land on a dead-letter queue monitored by an alert rule.

- !Critical Workflow Sequence

**Caption:** The sequence diagram traces the critical booking-and-payment workflow from search through seat hold, payment intent, PSP settlement, ticket issuance and notification, including the failure branch where payment is declined and the held seat is released. It matters because this single path carries all revenue and all seat-inventory correctness risk.

**Fallback:** Passenger to UI to Gateway to Booking Service (reserve seat, status Pending) to Payment Service (create intent) to PSP (authorise) to Payment Service (webhook verified) to Booking Service (confirm, issue ticket) to Service Bus to Notification Service (email and SMS). On decline, Payment Service returns failure, Booking Service releases the seat hold and the booking is marked Failed with no ticket issued.
