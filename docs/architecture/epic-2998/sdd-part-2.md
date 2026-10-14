# **4. Technical Design Specification**

## **4.1 Integration Plan**

- **Payment Gateway (REST/HTTPS)**: The Payment API calls `POST /authorize`, `POST /capture`, and `POST /refund` with a JSON contract of `{bookingReference, amount, currency, idempotencyKey}` and receives `{gatewayReference, status, failureCode}`. Authentication uses a merchant API key plus HMAC-SHA256 request signing, with the key stored in Azure Key Vault secret `payment-gateway-api-key`. This matters because Story 3006 requires that only successful authorization confirms a booking.
- **Email/SMS Provider (REST/HTTPS)**: The Notification API posts `{channel, recipient, templateId, mergeFields}` to the provider and persists the returned message identifier in `NOTIFICATION.DeliveryStatus`. Three templates are required - booking confirmation, cancellation/refund, and schedule change - matching the three notification events in Story 3009. Delivery webhooks update status asynchronously so support staff can prove whether a passenger was notified.
- **Event flows**: Booking confirmation, cancellation, and schedule change raise internal domain events (`BookingConfirmed`, `BookingCancelled`, `ScheduleChanged`) consumed by `NotificationService` through an outbox table, which guarantees a notification is never lost if the provider is briefly unavailable (dependency: provider uptime, see Section 7.2).
- **Authentication methods**: Browser-to-API uses JWT bearer tokens with a 60-minute access-token lifetime and a 7-day refresh token; API-to-gateway uses signed API keys; API-to-database uses an Azure Managed Identity so no SQL password exists in configuration.
- **Measurable integration checkpoints**: Gateway sandbox contract test suite must pass 100% before Week 5 integration testing; notification provider must demonstrate at least 99% delivery for a 500-message smoke batch; every external call must emit an Application Insights dependency telemetry record with correlation identifier; failed external calls must retry 3 times before being marked failed.

## **4.2 Configuration Checklist**

- **Azure resources (named)**: `app-railway-api-prod` (Azure App Service, P1v3), `swa-railway-ui-prod` (Azure Static Web Apps), `sql-railway-prod` with database `RailwayTicketingDb` (Azure SQL, S3 tier), `kv-railway-prod` (Azure Key Vault), `appi-railway-prod` (Application Insights), `log-railway-prod` (Log Analytics). Names are environment-suffixed (`-dev`, `-test`, `-prod`) so promotion is a parameter change, not a code change.
- **Key Vault secrets**: `payment-gateway-api-key`, `payment-gateway-hmac-secret`, `notification-provider-api-key`, `jwt-signing-key`. No secret may appear in `appsettings.json`, environment files, or source control; the API reads them at startup via Managed Identity.
- **Application settings**: `Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenMinutes=60`, `Payment:TimeoutSeconds=30`, `Payment:RetryCount=3`, `Refund:PolicySlabs` (JSON array of hours-to-departure thresholds and charge percentages), `Cache:MasterDataMinutes=60`, `ApplicationInsights:ConnectionString`.
- **Infrastructure as code**: All resources are declared in Bicep modules under `infra/` and deployed through GitHub Actions; manual portal changes are prohibited, and drift is detected by a weekly `what-if` run. Bicep state and parameter files are version controlled per environment.
- **Database configuration**: Entity Framework Core 8 migrations are the only schema change mechanism; required indexes are `IX_TrainSchedule_TrainId_TravelDate`, `IX_SeatInventory_ScheduleId_TravelClassId`, `UX_Booking_BookingReference`, and `IX_SupportRequest_UserId_Status`. Point-in-time restore retention is set to 35 days.

## **4.3 Service Level Agreement**

- **Latency**: Train search (`GET /api/trains/search`) must return in 800 ms or less at the 95th percentile for a single-day, single-route query; seat availability in 500 ms p95; booking creation in 1.5 s p95 excluding gateway time; payment confirmation in 5 s p95 including gateway round trip.
- **Uptime**: 99.9% monthly availability for search and booking endpoints (maximum approximately 43 minutes of unplanned downtime per 30-day month), measured by Application Insights availability tests executed every 5 minutes from three Azure regions.
- **RTO/RPO**: Recovery Time Objective is 4 hours and Recovery Point Objective is 15 minutes, achieved through Azure SQL automated backups with point-in-time restore and geo-redundant backup storage; a restore drill is executed once per quarter and the result recorded.
- **Maintenance windows**: Planned maintenance occurs Sundays 02:00-04:00 UTC, announced 72 hours in advance; deployments outside this window must be zero-downtime slot swaps. Booking and payment endpoints are never taken offline during a window without a read-only maintenance page.
- **Escalation SLAs**: Severity 1 (payments failing, booking unavailable) acknowledged within 15 minutes and mitigated within 4 hours; Severity 2 (notifications delayed, search degraded) acknowledged within 1 hour and resolved within 1 business day; support requests raised under Story 3010 receive a first response within 24 hours and are tracked until closure.

## **4.4 Security & Compliance Architecture**

- **Network segmentation**: The API is reachable only through Azure Front Door with a Web Application Firewall using the OWASP 3.2 managed rule set; the Azure SQL Database accepts connections only from the App Service subnet via a Private Endpoint and denies public network access.
- **Encryption**: TLS 1.2 or higher is enforced for all inbound and outbound traffic (HTTP is redirected, not served); data at rest uses Azure SQL Transparent Data Encryption; PII columns holding email and phone number use Always Encrypted deterministic encryption so support queries remain possible.
- **Key rotation**: JWT signing keys rotate every 90 days with a 24-hour overlap window; payment gateway and notification provider keys rotate every 180 days or immediately on suspected compromise; rotation is automated through Key Vault versioned secrets with no application redeploy required.
- **Compliance mappings**: Card data is never stored, logged, or transmitted through this system - the hosted gateway page collects it - which keeps the platform in PCI DSS SAQ-A scope. Passenger PII handling follows data-minimization and a purge of support attachments after 180 days; audit logs for booking, payment, cancellation, and refund are retained for 7 years for financial dispute resolution.
- **Policy enforcement**: Role-Based Authorization restricts Passengers to their own `USER_ACCOUNT` scope and Support Agents to read-only booking access linked to an open support request; all privileged actions are written to an immutable audit log in Log Analytics; secret scanning, dependency scanning, and CodeQL run on every pull request and block merge on high-severity findings.

## **4.5 Workflow, Orchestration & Scheduling**

- **Booking workflow execution steps**: search, select schedule and travel class, capture passengers, reserve seats (transactional decrement), create booking in `PendingPayment`, authorize payment, then confirm or release. Each step is a discrete, resumable state on `BOOKING.BookingStatus`, so an abandoned session can be reconciled rather than silently leaking inventory.
- **Orchestration tooling**: Orchestration is performed in-process by `BookingService` and `PaymentService` using database transactions plus an outbox table; a .NET `BackgroundService` hosted worker drains the outbox every 30 seconds to dispatch notifications. No external workflow engine is introduced, which avoids added licensing cost and operational surface for an eight-story scope.
- **Scheduled jobs**: An expiry job runs every 5 minutes to release seats held by bookings that remained in `PendingPayment` for more than 15 minutes; a refund reconciliation job runs hourly to match `CANCELLATION.RefundStatus` against gateway refund states; a schedule-change detection job runs daily at 01:00 UTC and raises `ScheduleChanged` notifications.
- **Retries and backoffs**: External calls retry 3 times with exponential backoff of 2s, 4s, and 8s plus jitter; the outbox dispatcher retries a message up to 10 times over 24 hours before moving it to a dead-letter table with an alert; database deadlock victims retry twice before surfacing an error.
- **Failure-handling paths**: A gateway timeout triggers a status query before any retry to avoid double charging; an unconfirmable payment releases seats and leaves the booking unconfirmed per Story 3006; notification failure never rolls back a confirmed booking but raises a Severity 2 alert, because a passenger holding a valid ticket must not lose it due to a messaging outage.

## **4.6 Release Management & Environment Strategy**

- **Pipelines**: GitHub Actions runs build, unit test, code scan, package, and deploy, producing one immutable artifact promoted unchanged through dev, test, and production; the pipeline fails closed on any high-severity CodeQL or dependency finding.
- **Branching strategy**: Trunk-based development on `main` with short-lived `feature/story-<id>` branches named after the Azure DevOps story (for example `feature/story-3003-book-railway-tickets`), squash-merged through a pull request requiring one reviewer and green checks.
- **Environment promotion rules**: dev deploys automatically on merge to `main`; test deploys on a tagged release candidate and requires the full regression suite to pass; production requires an approval gate from the release manager and a successful staging slot smoke test before swap.
- **Artifact versioning**: Semantic versioning `MAJOR.MINOR.PATCH` with the build number and Git commit SHA embedded in the artifact and exposed at `GET /api/health/version`, so any production incident can be traced to an exact commit in minutes.
- **Rollback procedures**: Production rollback is an Azure App Service slot swap back to the previous slot, targeted at under 10 minutes; database migrations must be backward compatible for one release (expand-then-contract) so code can roll back without a schema restore.

## **4.7 Testing Strategy & Quality Gates**

- **Testing stages**: Unit tests on domain services, integration tests against a containerized SQL Server and stubbed gateway, API contract tests, end-to-end UI tests with Playwright covering search, book, pay and cancel, and user acceptance testing mapped one-to-one to the eight story acceptance criteria.
- **Automation coverage thresholds**: Minimum 80% line coverage overall and 90% on `BookingService`, `PaymentService`, and `RefundService`, because those classes hold the seat-inventory and money-movement rules where a defect is most expensive.
- **Environment readiness gates**: A build may not promote unless database migrations applied cleanly, the gateway sandbox is reachable, seeded train/station master data exists, and the health endpoint returns `Healthy` for database, Key Vault, and both external dependencies.
- **Defect severity rules**: Severity 1 (overbooking, double charge, confirmed ticket without payment) blocks release unconditionally; Severity 2 (refund miscalculation, missing notification) blocks release unless an approved workaround exists; Severity 3 and 4 are scheduled into the next sprint.
- **Performance benchmarks**: A load test must sustain 200 concurrent search users and 50 concurrent bookings per minute while holding the p95 latency targets in Section 4.3, with zero overbooking observed across a 30-minute soak test.

# **5. Implementation Plan**

## **5.1 Phased Rollout Strategy**

- **Week 1-2 - Foundation**: Provision dev and test Azure resources from Bicep (`app-railway-api-*`, `swa-railway-ui-*`, `sql-railway-*`, `kv-railway-*`, `appi-railway-*`), stand up the GitHub Actions pipeline, scaffold the .NET 8 solution and React TypeScript app, and apply the initial EF Core migration for all thirteen entities. Advancement criterion: pipeline deploys an empty-but-healthy API to dev and `GET /api/health` returns `Healthy`.
- **Week 3-4 - Search and availability (Feature 2999)**: Implement `TrainSearchService`, `SeatAvailabilityService`, their repositories, and the Train Search UI. Advancement criterion: Stories 3000 and 3001 acceptance criteria pass in test with seeded master data and p95 search latency of 800 ms or less.
- **Week 5-6 - Booking and profiles (Feature 3002)**: Implement `BookingService` with transactional seat decrement, booking-reference generation, `PassengerService`, and the Booking and Profile UIs. Advancement criterion: Stories 3003 and 3004 pass, and a concurrency test proves zero overbooking at 50 simultaneous bookings on one seat pool.
- **Week 7-8 - Payments (Story 3006)**: Integrate the Payment Gateway sandbox, implement idempotency keys, confirm/release logic, and the Payment UI. Advancement criterion: no confirmed ticket exists for any failed payment across the full negative-path test matrix.
- **Week 9 - Cancellation and refunds (Story 3007)**: Implement `RefundService` with the configurable policy slabs, the cancellation endpoint, and refund status visibility. Advancement criterion: refund amounts match the business-supplied slab table for all boundary cases.
- **Week 10 - Notifications (Story 3009)**: Implement the outbox dispatcher, three provider templates, and delivery-status persistence for booking, cancellation, and schedule-change events. Advancement criterion: at least 99% delivery on a 500-message smoke batch.
- **Week 11 - Support (Story 3010)**: Implement `SupportService` with create, track, update, and close transitions plus the Support Agent role view. Advancement criterion: a support request can be followed through its full lifecycle to closure with an audit trail.
- **Week 12 - Hardening**: Run penetration testing, the load and soak tests from Section 4.7, WAF tuning, and Key Vault/Managed Identity verification in a production-like environment. Advancement criterion: zero open Severity 1 or Severity 2 defects.
- **Week 13 - Production pilot**: Deploy to production behind a feature flag limited to a single route pair and an invited passenger cohort, monitoring conversion, payment success rate, and error budget daily. Advancement criterion: 7 consecutive days within SLA with no Severity 1 incident.
- **Week 14 - General availability**: Remove the feature flag, enable all routes, publish runbooks, and transition to the standing support rota. Advancement criterion: operations sign-off with runbooks, dashboards, and alerts in place.

## **5.2 Teams and Security Roles**

- **Product Owner (Azure DevOps)**: Owns Epic 2998 scope, prioritizes the four Features, approves acceptance-criteria interpretation, and signs off user acceptance testing. Escalation path for scope disputes and refund-policy clarification.
- **Architect**: Owns this System Design Document, the six diagrams, and all Architecture Decision Records; approves any deviation from the layered React/.NET 8/Azure SQL baseline and reviews schema changes before migration merge.
- **Frontend team (React/TypeScript)**: Owns the Static Web App, the five UI modules, token handling in the browser, and accessibility; has Contributor RBAC on `swa-railway-ui-dev` and `-test` only, never on production.
- **Backend team (.NET 8)**: Owns the six API groups, domain services, repositories, EF Core migrations, and external integrations; has Contributor RBAC on dev and test App Services and read-only access to production telemetry.
- **Database/Data owner**: Owns `RailwayTicketingDb` schema, indexing, retention, and PII column encryption; the sole role permitted to approve production migration scripts and to grant any direct database access.
- **Security engineer**: Owns Key Vault access policies, WAF rules, key-rotation schedules, and the PCI DSS SAQ-A posture; approves every new secret and every new outbound integration endpoint.
- **DevOps/Platform engineer**: Owns Bicep infrastructure, GitHub Actions pipelines, environment promotion gates, and slot-swap rollback execution; the only role with production deployment rights, exercised through the approval gate.
- **QA lead**: Owns the test strategy, coverage thresholds, defect severity triage, and the performance benchmark suite; holds the authority to block a release on a Severity 1 defect.
- **Support Agent (application role)**: Operational role defined inside the product for Story 3010, granted read-only access to bookings referenced by an open support request and write access to support-request status and notes; cannot issue refunds without an approved cancellation.
- **Site reliability on-call**: Owns incident response against the Section 4.3 escalation SLAs, runbook execution, and the quarterly restore drill; escalates unresolved Severity 1 incidents to the Architect and Product Owner after 2 hours.

# **6. Effort & Schedule Estimation**

## **6.1 Estimation Approach**

Estimation uses three complementary techniques so that a single method's bias does not drive the plan. Story points are assigned on a modified Fibonacci scale (1, 2, 3, 5, 8, 13) during refinement: Stories 3000 and 3001 are 5 points each, 3003 is 13 points because of the concurrency-safe seat reservation, 3004 is 5 points, 3006 is 13 points due to gateway integration and idempotency, 3007 is 8 points for the policy slab engine, 3009 is 8 points for the outbox and three templates, and 3010 is 5 points - a total of 62 points for Epic 2998. T-shirt sizing is applied at Feature level for executive communication: Train Search and Availability is M, Booking and Passenger Management is L, Payments and Ticket Lifecycle is XL, and Notifications and Support is M. Three-point estimation is then applied to the whole Epic to express schedule risk: Optimistic 11 weeks (gateway sandbox available on day one, master data clean), Realistic 14 weeks (the plan in Section 5.1), and Pessimistic 18 weeks (gateway certification delays, refund-policy rework, or master-data quality remediation), giving a PERT expected duration of approximately 14.2 weeks. The dominant estimation risk is external: payment-gateway certification and notification-provider sender verification are vendor-controlled and are therefore tracked as dependencies rather than as team velocity.

## **6.2 Timeline & Milestones**

- **M1 - Foundation ready (Week 1 start to Week 2 end)**: Azure dev/test environments, CI/CD pipeline, and initial schema. Dependency: Azure subscription and Bicep approval. Critical path: yes - every later milestone waits on the pipeline and schema.
- **M2 - Search and availability complete (Week 3 start to Week 4 end)**: Stories 3000 and 3001 accepted. Dependency: seeded train, station, schedule, class, and fare master data. Critical path: yes - booking cannot be built without schedule and inventory reads.
- **M3 - Booking and profiles complete (Week 5 start to Week 6 end)**: Stories 3003 and 3004 accepted with the zero-overbooking concurrency test passing. Dependency: M2. Critical path: yes.
- **M4 - Payments and refunds complete (Week 7 start to Week 9 end)**: Stories 3006 and 3007 accepted. Dependency: payment-gateway sandbox credentials and the business-approved refund slab table, both required no later than Week 6. Critical path: yes, and this is the highest-risk segment of the schedule.
- **M5 - Notifications, support, hardening and go-live (Week 10 start to Week 14 end)**: Stories 3009 and 3010 accepted, security and load testing passed, pilot completed, general availability declared. Dependency: notification-provider sender verification by Week 9. Critical path: partially - Story 3010 can run parallel to hardening, so only notifications and hardening sit on the critical path.

# **7. Assumptions & Dependencies**

## **7.1 Assumptions**

Train, station, schedule, travel-class, and fare master data is supplied and maintained by an upstream railway master-data system, because no User Story under Epic 2998 defines administrative maintenance of this data; the platform reads it and does not author it. Passenger identity is handled by this platform's own JWT-based sign-in with Passenger and Support Agent roles, since no story specifies an external identity provider, and the design keeps this swappable for Microsoft Entra ID later. Payment card data is captured on a hosted gateway page, so the system stores only a gateway reference, amount, method, and status - this is the assumption that keeps PCI DSS scope at SAQ-A. The refund policy is a configurable set of hours-to-departure slabs provided by the business rather than a fixed formula, because Story 3007 states only that refunds are calculated per policy. Single-currency, single-region operation is assumed for the first release, as no story mentions multi-currency or data residency beyond one Azure region. Email and SMS are the only notification channels, per Story 3009, with push notifications deliberately deferred. Seat selection is at travel-class granularity rather than specific seat-map picking, since Story 3001 asks for availability by class. All assumptions are recorded here so they can be challenged during design review rather than discovered during implementation.

## **7.2 External Dependencies**

The Payment Gateway is the most significant external dependency: the program needs sandbox credentials by Week 6 and production merchant certification before the Week 13 pilot, with an expected gateway availability of 99.9% and refund settlement within the provider's stated banking cycle; a gateway outage degrades booking confirmation and is the primary Severity 1 risk. The Email/SMS Provider must complete sender identity and SMS short-code verification by Week 9 and is expected to deliver at least 99% of messages, with queued retries covering transient failures. Azure platform services carry Microsoft's standard service level agreements - Azure SQL Database 99.99%, App Service 99.95%, Key Vault 99.99% - and their availability bounds the 99.9% application target stated in Section 4.3. The upstream railway master-data source must deliver an initial load before Week 3 and incremental updates daily, as schedule and fare accuracy directly determines search correctness. Internally, the Security team must approve WAF rules and Key Vault access policies before the Week 12 hardening phase, the Database owner must approve production migrations, and the Platform team must provide the production subscription and approval gates by Week 12. Each dependency has a named owner and a required-by week so that slippage is visible on the critical path described in Section 6.2.

# **8. Appendices**

## **8.1 Additional Diagrams**

`- !High-Level Flow`

![High-Level Flow](system-context.png)

*Caption - High-Level Flow:* The system context view, tying Epic 2998's two human actors (Passenger, Support Agent) and two external systems (Payment Gateway, Email/SMS Provider) to the single platform boundary that owns all booking data.

`- !Deployment Diagram`

![Deployment Diagram](deployment.png)

*Caption - Deployment Diagram:* Shows the production topology - Azure Front Door with WAF, the React UI on Azure Static Web Apps, the .NET 8 API on Azure App Service, Azure SQL Database, Key Vault, Application Insights, and Log Analytics - and where the two external integrations exit the Azure boundary. It supports the SLA, RTO/RPO, and network-segmentation commitments in Sections 4.3 and 4.4.

`- !CI/CD Pipeline`

*Caption - CI/CD Pipeline:* One immutable artifact is promoted from dev through test to production. A developer opens a pull request from a `feature/story-<id>` branch; the pipeline builds the .NET 8 API and React UI, runs unit and integration tests, executes CodeQL, dependency and secret scanning, publishes a semantically versioned artifact, deploys automatically to dev, promotes to test on a release-candidate tag after full regression, then requires release-manager approval before a production staging-slot smoke test and slot swap. Rollback is the reverse slot swap, targeted at under 10 minutes (Section 4.6).

## **8.2 Traceability Matrix**

| Azure DevOps Item | Title | Architecture Components | Diagrams |
|---|---|---|---|
| Epic 2998 | Online Railway Ticketing System | Entire platform | All six |
| Feature 2999 | Train Search and Availability | Train Search API, TrainSearchService, SeatAvailabilityService, TrainRepository | System Context, Solution Architecture, Component, Sequence |
| Story 3000 | Search trains between stations | `GET /api/trains/search`, TRAIN, TRAIN_SCHEDULE, STATION, SEAT_INVENTORY.Fare | Sequence, Data Model |
| Story 3001 | View seat availability | `GET /api/schedules/{id}/availability`, SEAT_INVENTORY, TRAVEL_CLASS (no caching) | Component, Data Model |
| Feature 3002 | Booking and Passenger Management | Booking API, Passenger Profile API, BookingService, PassengerService | Solution Architecture, Component |
| Story 3003 | Book railway tickets | `POST /api/bookings`, transactional seat decrement, BookingReference, BOOKING_PASSENGER | Sequence, Data Model |
| Story 3004 | Manage passenger profiles | `GET/POST/PUT /api/passenger-profiles`, PASSENGER_PROFILE, USER_ACCOUNT | Component, Data Model |
| Feature 3005 | Payments and Ticket Lifecycle | Payment API, PaymentService, RefundService, Payment Gateway integration | Solution Architecture, Sequence, Deployment |
| Story 3006 | Pay for bookings | `POST /api/payments`, idempotency key, confirm/release branch, PAYMENT | Sequence |
| Story 3007 | Cancel tickets and receive refunds | `POST /api/bookings/{ref}/cancel`, policy slabs, CANCELLATION.RefundStatus | Data Model, Component |
| Feature 3008 | Notifications and Support | Notification API, Support API, outbox dispatcher | Solution Architecture, Component |
| Story 3009 | Receive booking notifications | NotificationService, three provider templates, NOTIFICATION.DeliveryStatus | System Context, Sequence |
| Story 3010 | Raise support requests | `POST /api/support-requests`, SupportService, SUPPORT_REQUEST lifecycle, Support Agent role | Component, Data Model |

## **8.3 Architecture Artifact Index**

| Artifact | Repository Path |
|---|---|
| System Context (PNG / Mermaid) | `docs/architecture/epic-2998/system-context.png` / `.mmd` |
| Solution Architecture (PNG / Mermaid) | `docs/architecture/epic-2998/solution-architecture.png` / `.mmd` |
| Component Diagram (PNG / Mermaid) | `docs/architecture/epic-2998/component-diagram.png` / `.mmd` |
| Sequence - Booking & Payment (PNG / Mermaid) | `docs/architecture/epic-2998/sequence-booking-payment.png` / `.mmd` |
| Data Model (PNG / Mermaid) | `docs/architecture/epic-2998/data-model.png` / `.mmd` |
| Deployment (PNG / Mermaid) | `docs/architecture/epic-2998/deployment.png` / `.mmd` |
| System Design Document (Markdown) | `docs/architecture/epic-2998/System-Design-Document.md` |
| System Design Document (Word) | `docs/architecture/epic-2998/System-Design-Document.docx` |
