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
