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

---

# **4. Technical Design Specification**

## **4.1 Integration Plan**
- **Payment service provider (PSP) dependency:** The Payment Service integrates over HTTPS using a tokenised payment intent API and a signed webhook callback; webhook signatures are verified with an HMAC-SHA256 secret held in Azure Key Vault, and unverified callbacks are rejected with HTTP 401 (this protects User Story 3006's rule that only successful payments confirm bookings). Integration checkpoint: 100 percent of settlement webhooks verified and reconciled within 15 minutes.
- **Notification provider dependency:** The Notification Service calls an email and SMS provider API for booking, cancellation and schedule-change messages (User Story 3009), using templates versioned in the repository; delivery receipts are persisted against the notification record. Integration checkpoint: 99 percent of confirmation messages dispatched within 60 seconds of ticket issuance.
- **Internal asynchronous contract:** Booking publishes BookingConfirmed, BookingCancelled and ScheduleChanged events to the Azure Service Bus topic booking-events with a JSON data contract containing bookingReference, ticketId, passengerContact and eventTimestamp; the Notification Service subscribes with a dedicated subscription and 10 maximum delivery attempts before dead-lettering.
- **Authentication across boundaries:** The browser presents a JWT bearer token (15-minute access token, 8-hour refresh token) validated at the API gateway; service-to-service and service-to-Azure-resource calls use Microsoft Entra managed identities with no shared secrets in configuration, satisfying the baseline token-based authentication and backend authorisation rule.
- **Data contracts and versioning:** All public endpoints are versioned under /api/v1 with JSON data transfer objects (DTOs); contract changes are additive only within a major version, and OpenAPI 3.0 specifications are generated at build time and published as pipeline artifacts so Developer and QA agents validate against the same contract.

## **4.2 Configuration Checklist**
- **Named Azure resources:** Resource group rg-railway-ticketing-prod containing Azure SQL Database railwaydb (General Purpose, zone redundant), Azure Cache for Redis cache-railway (Standard C1), Azure Service Bus namespace sb-railway with topic booking-events, Azure Key Vault kv-railway and Application Insights appi-railway - each deployed per environment with a dev, qa or prod suffix.
- **Application settings:** ConnectionStrings:RailwayDb, Redis:ConnectionString, ServiceBus:FullyQualifiedNamespace, Payments:ProviderBaseUrl, Payments:WebhookSecretName, Notifications:ProviderBaseUrl and Jwt:Authority are mandatory; all secret values are Key Vault references resolved by managed identity, never literals in appsettings.json (no secret is ever committed to source control).
- **Business configuration:** Refund policy bands (hours before departure to refund percentage), seat-hold timeout of 15 minutes, search cache time-to-live of 60 seconds, maximum passengers per booking of 6, and supported travel classes are held in a configuration table so policy changes require no redeployment.
- **Infrastructure as code:** All resources are declared in Bicep modules under infra/ with environment-specific parameter files, state and deployments executed only through the pipeline service principal; manual portal changes are prohibited and detected by a nightly drift-check job.
- **Runtime and framework versions:** .NET 8 (long-term support) for all services, React 18 with TypeScript 5.x and Node.js 20 for the client, Entity Framework Core 8 for persistence, and container images based on mcr.microsoft.com/dotnet/aspnet:8.0 pinned by digest for reproducible builds.

## **4.3 Service Level Agreement**
- **Latency targets:** Train search (User Story 3000) returns in under 500 milliseconds at the 95th percentile on a cache hit and under 1,200 milliseconds at the 95th percentile on a cache miss; booking creation completes in under 2 seconds at the 95th percentile excluding external PSP authorisation time.
- **Availability targets:** 99.9 percent monthly availability for search and booking endpoints and 99.95 percent for payment settlement callback processing, measured by synthetic availability tests running every 5 minutes from two Azure regions.
- **Recovery objectives:** Recovery Time Objective (RTO) of 60 minutes and Recovery Point Objective (RPO) of 15 minutes, supported by Azure SQL point-in-time restore with 35-day retention and geo-redundant backup; a documented restore drill is executed quarterly and its result recorded as evidence.
- **Maintenance and change windows:** Planned maintenance occurs Sundays 02:00-04:00 UTC with blue-green slot swaps so passenger-facing downtime is zero; emergency changes require Release Manager approval and a post-incident review within 3 business days.
- **Escalation and support SLAs:** Severity 1 (payments failing or booking unavailable) acknowledged within 15 minutes and mitigated within 4 hours; Severity 2 within 1 hour and 1 business day; passenger support requests raised under User Story 3010 receive first response within 8 business hours and are tracked to closure with status visible to the passenger.

## **4.4 Security & Compliance Architecture**
- **Network segmentation:** Public ingress terminates at Azure Application Gateway with WAF in prevention mode using the OWASP Core Rule Set 3.2; backend services run in a private virtual network subnet and reach Azure SQL, Redis, Service Bus and Key Vault exclusively over private endpoints with public network access disabled.
- **Encryption standards:** TLS 1.2 or higher is enforced for every external and internal hop with weaker cipher suites disabled; Azure SQL uses Transparent Data Encryption with Always Encrypted columns for passenger identity documents and contact details, and Redis is configured for in-transit encryption only with no PII cached.
- **Key and secret management:** All secrets, webhook signing keys and connection strings live in Azure Key Vault with soft delete and purge protection enabled, 90-day automated rotation for application secrets, and access limited to service managed identities through role-based access control (no human standing access to production secrets).
- **Compliance mapping:** Raw card data never touches the platform - the PSP is responsible for cardholder data, keeping the solution in PCI DSS SAQ-A scope; passenger PII is classified, tagged, retained for 7 years for ticketing and tax purposes then purged, and data-subject export and erasure requests are serviced through the Support Service workflow.
- **Policy enforcement and auditing:** Authorisation is enforced in every backend service by role (Passenger, SupportAgent, Administrator) and by resource ownership so a passenger can only read their own bookings; all booking, payment, refund and profile mutations write immutable audit entries with actor, correlation identifier and timestamp to Log Analytics with 365-day retention.

## **4.5 Workflow, Orchestration & Scheduling**
- **Booking orchestration:** The booking workflow executes reserve-seat, create-payment-intent, await-settlement, issue-ticket and publish-event as discrete, idempotent steps; each step is keyed by the Idempotency-Key header so a client retry or duplicate webhook can never double-book a seat or double-charge a passenger.
- **Compensating actions:** If settlement fails or times out, a compensating release-seat action returns inventory and marks the booking Failed; if ticket issuance fails after a successful capture, the workflow raises a Severity 1 alert and queues an automatic refund rather than leaving money captured without a ticket.
- **Scheduled jobs:** A pending-booking reconciliation job runs every 5 minutes to expire holds older than 15 minutes; a PSP settlement reconciliation job runs hourly to compare captured payments against confirmed bookings; and a schedule-change detection job runs every 30 minutes against the timetable feed to trigger ScheduleChanged notifications.
- **Retry and backoff policy:** Outbound calls to the PSP and the communications provider use three retries with exponential backoff (200 milliseconds, 400 milliseconds, 800 milliseconds) plus jitter, wrapped in a circuit breaker that opens after 5 consecutive failures for 30 seconds to protect downstream providers and preserve service threads.
- **Failure handling paths:** Service Bus subscriptions dead-letter after 10 delivery attempts; dead-letter depth greater than 0 raises an alert to the on-call engineer; poison messages are inspected and replayed through an operator runbook rather than discarded, so no passenger notification is silently lost.

## **4.6 Release Management & Environment Strategy**
- **Pipelines:** GitHub Actions builds the .NET 8 solution and the React client, runs tests and security scans, publishes container images to Azure Container Registry tagged with the commit SHA, and deploys through Bicep - every artifact is traceable to a single commit.
- **Branching strategy:** Trunk-based development on main with short-lived feature branches named feature/<work-item-id>-<slug>; pull requests require one approving review, a green pipeline and linked Azure DevOps work item before merge, giving backlog-to-code traceability for Epic 2998.
- **Environment promotion:** DEV deploys automatically on merge to main, QA requires QA Lead approval, and PROD requires Release Manager approval with a change record; identical Bicep templates are used in all three environments with only parameter files differing, eliminating configuration drift between environments.
- **Artifact versioning:** Semantic versioning (MAJOR.MINOR.PATCH) is applied to every service image and to the OpenAPI contract; images are immutable and never retagged, so a rollback is a redeploy of a known-good digest rather than a rebuild.
- **Rollback procedure:** Production uses blue-green deployment slots with a documented swap-back that restores the previous version in under 5 minutes; database changes are forward-only and backward-compatible for one release so application rollback never requires a schema rollback.

- !CI/CD Pipeline

**Caption:** The CI/CD diagram shows commit-to-production flow with the quality and approval gates that protect passenger-facing ticketing.

## **4.7 Testing Strategy & Quality Gates**
- **Test stages:** Unit tests (xUnit for .NET, Vitest for React) run on every push; integration tests exercise booking, payment-webhook and notification flows against ephemeral dependencies; end-to-end tests cover search, book, pay, cancel and support-request journeys mapped one-to-one to User Stories 3000, 3001, 3003, 3004, 3006, 3007, 3009 and 3010.
- **Coverage thresholds:** Minimum 80 percent line coverage overall and 90 percent for the Booking and Payment domain layers, enforced as a hard pipeline gate because these layers carry seat-inventory and monetary correctness.
- **Environment readiness gates:** A deployment is only promoted after smoke tests confirm health endpoints, database connectivity, cache connectivity and Service Bus subscription health; any failed readiness check blocks promotion automatically.
- **Defect severity rules:** Severity 1 (money captured without a ticket, double booking of a seat, payment data exposure) blocks release unconditionally; Severity 2 (notification not delivered, availability display stale beyond the 60-second cache window) blocks release unless an approved mitigation exists; Severity 3 and 4 are scheduled into the next sprint.
- **Performance benchmarks:** Load tests sustain 500 concurrent search users and 100 concurrent bookings per minute while holding the 95th-percentile targets in Section 4.3, with a negative test proving that a declined payment never results in a confirmed ticket and that the held seat is returned to inventory.

---

# **5. Implementation Plan**

## **5.1 Phased Rollout Strategy**
- **Week 1-2 - Foundation and environments:** Provision rg-railway-ticketing-dev, qa and prod through Bicep, establish the GitHub Actions pipeline, Azure SQL railwaydb schema baseline, Key Vault, Service Bus topic booking-events and Application Insights; advancement gate is a successful end-to-end pipeline run deploying a health-check service to DEV.
- **Week 3-4 - Train Search and Availability (Feature 2999):** Implement the schedule and seat-inventory schema, GET /api/v1/trains/search and class-level availability with Redis caching; advancement gate is User Stories 3000 and 3001 passing automated acceptance tests with 95th-percentile search latency under 500 milliseconds on cache hit.
- **Week 5-7 - Booking and Passenger Management (Feature 3002):** Implement passenger profiles, multi-passenger booking, pessimistic seat-inventory locking, idempotency and booking-reference generation; advancement gate is User Stories 3003 and 3004 passing, plus a concurrency test proving zero double-allocation under 100 simultaneous reservation attempts on the same seat class.
- **Week 8-10 - Payments and Ticket Lifecycle (Feature 3005):** Integrate the PSP payment intent, signed webhook verification, ticket issuance, cancellation and policy-based refund calculation with the 15-minute hold-expiry job; advancement gate is User Stories 3006 and 3007 passing, including the negative case where a failed payment leaves no confirmed ticket.
- **Week 11-12 - Notifications and Support (Feature 3008):** Implement Service Bus consumption, email and SMS templates for booking, cancellation and schedule change, and the support-request lifecycle from logging through tracking to closure; advancement gate is User Stories 3009 and 3010 passing with 99 percent of confirmations dispatched within 60 seconds.
- **Week 13 - Hardening and security validation:** Execute penetration testing, WAF tuning in prevention mode, Key Vault rotation verification, PII classification review and audit-log completeness checks; advancement gate is zero open high or critical findings.
- **Week 14 - Performance and resilience validation:** Run load tests at 500 concurrent search users and 100 bookings per minute, chaos-test PSP unavailability and Service Bus dead-lettering, and validate circuit-breaker and compensating-release behaviour; advancement gate is all Section 4.3 targets met.
- **Week 15 - User acceptance and QA sign-off:** Business stakeholders validate the eight User Stories against acceptance criteria in the QA environment; advancement gate is formal QA approval recorded on the Azure DevOps work items.
- **Week 16 - Production pilot and general availability:** Blue-green deployment to PROD with a limited-route pilot for 3 days under heightened monitoring, then full route enablement; advancement gate is zero Severity 1 incidents during the pilot window and successful rollback rehearsal evidence.

## **5.2 Teams and Security Roles**
- **Product Owner / Business Analyst:** Owns Epic 2998 and its Features and User Stories in Azure DevOps, approves acceptance criteria changes, and arbitrates refund-policy band values; holds Contributor access to work items and read-only access to the repository.
- **Architect:** Owns this System Design Document, the Mermaid and PNG diagrams in docs/architecture/epic-2998/, and all Architecture Decision Records; approves any deviation from the baseline stack and holds write access to the docs path.
- **Backend engineering team (.NET 8):** Owns the Search, Booking, Payment, Notification and Support services, their database migrations and their unit and integration tests; holds Contributor role in DEV, read-only in PROD, with no standing access to production secrets.
- **Frontend engineering team (React / TypeScript):** Owns the single-page application, token handling, accessibility compliance and client-side validation; holds the same environment scopes as the backend team and must never persist tokens outside secure browser storage.
- **Platform / DevOps engineer:** Owns Bicep infrastructure modules, GitHub Actions pipelines, Azure Container Registry, private endpoints and the deployment service principal; is the only role permitted to change production infrastructure, and all changes flow through pull requests.
- **Security engineer:** Owns WAF rules, Key Vault policies, secret rotation, penetration-test scheduling and PCI DSS SAQ-A scope assertions; holds Security Reader plus Key Vault Administrator in non-production and break-glass-only access in production.
- **QA lead and QA engineers:** Own the automation suites, coverage gates, performance benchmarks and defect-severity triage; approve promotion from DEV to QA and provide the sign-off required before the Release Manager approves PROD.
- **Release manager:** Approves production deployments and change records, owns the maintenance window Sundays 02:00-04:00 UTC and authorises rollback or slot swap-back decisions during incidents.
- **Site reliability / on-call engineer:** Owns Application Insights alert rules, dead-letter-queue monitoring, incident response against the Section 4.3 escalation SLAs, and quarterly restore-drill execution; escalates Severity 1 payment or booking failures within 15 minutes.
- **Support agents (business role):** Operate the Support Service created for User Story 3010 with the SupportAgent role, able to read passenger bookings for the purpose of resolving requests and to update support-ticket status, with every access written to the immutable audit log.

---

# **6. Effort & Schedule Estimation**

## **6.1 Estimation Approach**
Estimation combines story points, T-shirt sizing and three-point (Optimistic/Realistic/Pessimistic) analysis so that both relative complexity and schedule risk are visible to stakeholders. Each of the eight User Stories under Epic 2998 is sized in story points on a modified Fibonacci scale, each of the four Features is given a T-shirt size, and the overall Epic is bounded by a three-point estimate that explicitly accounts for external payment-provider dependency risk.

Feature-level T-shirt sizing: Train Search and Availability (Feature 2999) is Medium; Booking and Passenger Management (Feature 3002) is Large because of seat-inventory concurrency control; Payments and Ticket Lifecycle (Feature 3005) is Large because of PSP integration, webhook verification and refund policy; Notifications and Support (Feature 3008) is Medium. Story-point allocation is: 8 points for Search trains between stations (3000), 5 for View seat availability (3001), 13 for Book railway tickets (3003), 5 for Manage passenger profiles (3004), 13 for Pay for bookings (3006), 8 for Cancel tickets and receive refunds (3007), 5 for Receive booking notifications (3009) and 5 for Raise support requests (3010) - a total of 62 story points.

The three-point estimate for the full Epic is: Optimistic 13 weeks, Realistic 16 weeks, Pessimistic 21 weeks, giving a PERT-weighted expectation of approximately 16.3 weeks. The pessimistic branch is driven mainly by payment-provider certification delays and by any late change to refund-policy bands, both of which are tracked as dependencies in Section 7.2. A team velocity of approximately 20 story points per two-week sprint across two paired squads underpins the realistic case.

## **6.2 Timeline & Milestones**
- **Milestone M1 - Platform foundation ready (Week 1 to Week 2):** Environments, pipeline and data baseline complete; no inbound dependency; it is on the critical path because every subsequent Feature deploys through this pipeline.
- **Milestone M2 - Search and availability live in QA (Week 3 to Week 4):** Depends on M1; delivers User Stories 3000 and 3001; partially parallelisable with early Booking design work, so a one-week slip here consumes float rather than schedule.
- **Milestone M3 - Booking and passenger profiles complete (Week 5 to Week 7):** Depends on M2 for inventory contracts; delivers User Stories 3003 and 3004; sits on the critical path because payment work cannot be validated end to end without a reservable booking.
- **Milestone M4 - Payments, tickets, cancellations and refunds complete (Week 8 to Week 10):** Depends on M3 and on PSP sandbox credentials being available by Week 7; delivers User Stories 3006 and 3007; this is the highest-risk critical-path item and carries the pessimistic-case exposure.
- **Milestone M5 - Notifications, support, hardening and go-live (Week 11 to Week 16):** Depends on M4 for booking events; delivers User Stories 3009 and 3010, then security, performance, user acceptance testing and the production pilot, closing with general availability in Week 16.

# **7. Assumptions & Dependencies**

## **7.1 Assumptions**
The backlog for Epic 2998 defines capabilities but not every technical particular, so the following assumptions are stated explicitly to de-risk ambiguity. Azure is the hosting platform and the baseline stack of React with TypeScript on the front end and .NET 8 ASP.NET Core on the back end applies, because no Feature or User Story specifies an alternative. A tokenised payment service provider with signed webhook settlement is assumed, since User Story 3006 names only supported payment methods and not a vendor; no raw card data is stored by the platform under any circumstance.

Refund rules referenced by User Story 3007 are assumed to be a configurable table of hours-before-departure bands mapped to refund percentages, maintained by the business rather than hard-coded, because the acceptance criterion states only that refunds are calculated per policy. Train, route, schedule and fare master data are assumed to arrive from an operator timetable feed loaded into the platform's schedule store, since no authoring capability appears in the backlog. Seat availability is assumed to be tracked at train, date and travel-class granularity rather than individual seat numbers, matching the wording of User Story 3001 that availability is displayed by travel class; if berth-level allocation is later required it is a change request against Feature 2999.

Notification channels are assumed to be email and SMS as stated in User Story 3009, with no push-notification channel in this release. A maximum of 6 passengers per booking and a 15-minute seat-hold window are assumed operational defaults held in configuration. Support requests under User Story 3010 are assumed to be handled in-platform by agents holding the SupportAgent role rather than federated to an external customer-relationship-management system.

## **7.2 External Dependencies**
The payment service provider is the most significant external dependency: sandbox credentials are required by Week 7 and production credentials plus webhook endpoint allow-listing by Week 14, with provider availability directly bounding the 99.95 percent settlement-processing target. The email and SMS provider is required from Week 11, including sender-identity verification and SMS sender registration, which in several jurisdictions carries a multi-week lead time and is therefore started in Week 8.

The railway operator timetable feed is a data dependency owned outside the delivery team and must be available in a stable format by Week 3 for Feature 2999; a static seeded dataset is the agreed contingency so search development is never blocked. Azure platform services - SQL Database, Cache for Redis, Service Bus, Key Vault, Application Insights and Application Gateway - are consumed under Microsoft service level agreements, and the composite availability of these services bounds the 99.9 percent application target. Internal dependencies include the security team for penetration testing in Week 13 and the Release Manager for the production change approval in Week 16.

# **8. Appendices**

## **8.1 Additional Diagrams**

- !High-Level Flow

**Caption:** The high-level flow diagram shows the complete passenger request path for Epic 2998 - browser through Application Gateway and the API gateway to the Search, Booking, Payment, Notification and Support services, then to Azure SQL, Redis, Service Bus and the external payment and communications providers. It ties directly to the Epic description of searching trains, booking tickets, payments, ticket management, notifications and customer support.

- !Deployment Diagram

**Caption:** The deployment diagram shows the Azure topology including the WAF-enabled gateway, private-endpoint-isolated data services, Key Vault for secrets and Application Insights for telemetry, demonstrating how the Section 4.4 security controls are physically realised.

- !CI/CD Pipeline

**Caption:** The CI/CD pipeline diagram shows the commit-to-production path with unit tests, coverage and security gates, DEV automatic deployment, QA and PROD approvals, blue-green release and monitored rollback, implementing the Section 4.6 release strategy.

- !Data Model

**Caption:** The entity-relationship diagram covers the core entities - Station, Train, Schedule, SeatInventory, PassengerProfile, Booking, Passenger, Ticket, Payment, Refund, Notification and SupportRequest - with primary keys, foreign keys, cardinalities and PII tagging on passenger contact and identity attributes.

**Fallback:** If images cannot be rendered, the Mermaid sources for every diagram are committed alongside this document as system-context.mmd, solution-architecture.mmd, component-diagram.mmd, sequence-booking-payment.mmd, data-model.mmd, deployment.mmd, high-level-flow.mmd and cicd-pipeline.mmd in docs/architecture/epic-2998/.

## **8.2 Traceability Matrix**

| Work Item | Type | Title | Primary Component | Design Reference |
| --- | --- | --- | --- | --- |
| 2998 | Epic | Online Railway Ticketing System | Whole platform | Sections 1-8 |
| 2999 | Feature | Train Search and Availability | Search Service, Redis cache, Schedule store | 2.1, 3.1, 5.1 Week 3-4 |
| 3000 | User Story | Search trains between stations | GET /api/v1/trains/search | 3.1, 4.3 latency targets |
| 3001 | User Story | View seat availability | SeatInventory read with cache invalidation | 3.1, 4.7 severity rules |
| 3002 | Feature | Booking and Passenger Management | Booking Service, Azure SQL | 2.1, 3.1, 5.1 Week 5-7 |
| 3003 | User Story | Book railway tickets | POST /api/v1/bookings, booking reference | 3.1, 4.5 orchestration |
| 3004 | User Story | Manage passenger profiles | /api/v1/passenger-profiles | 3.1, 4.4 PII controls |
| 3005 | Feature | Payments and Ticket Lifecycle | Payment Service, PSP integration | 2.1, 3.1, 5.1 Week 8-10 |
| 3006 | User Story | Pay for bookings | Payment intent and verified webhook | 3.1, 4.1, 4.5 compensations |
| 3007 | User Story | Cancel tickets and receive refunds | Cancellation and refund-policy table | 3.1, 4.2 business configuration |
| 3008 | Feature | Notifications and Support | Notification Service, Support Service | 2.1, 5.1 Week 11-12 |
| 3009 | User Story | Receive booking notifications | Service Bus subscriber, email and SMS | 4.1, 4.5 dead-lettering |
| 3010 | User Story | Raise support requests | Support Service, SupportAgent role | 4.3 support SLA, 5.2 roles |

## **8.3 Glossary**
ADR - Architecture Decision Record. DTO - Data Transfer Object. JWT - JSON Web Token. PII - Personally Identifiable Information. PCI DSS - Payment Card Industry Data Security Standard. PSP - Payment Service Provider. RPO - Recovery Point Objective. RTO - Recovery Time Objective. SMS - Short Message Service. TLS - Transport Layer Security. WAF - Web Application Firewall.
