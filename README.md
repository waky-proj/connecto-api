# connecto-api

Existing ASP.NET Core API and modular core | 1 October 2026

## Status and domain ownership

This repository contains the .NET 10 API, application/domain projects, EF Core SQL Server persistence, migration runner, tests, Docker and AWS Terraform/deployment configuration. It remains authoritative during staged migration. New service repositories are documentation scaffolds; creation does not extract any capability.

The opt-in SQL Server and fresh-browser F3 acceptance commands and prerequisites are in [F3_ACCEPTANCE.md](F3_ACCEPTANCE.md). A skipped path is not acceptance evidence.

Implemented locally: Cognito access-token authentication and subject-based user provisioning, self-profile retrieval/allowlisted editing, paginated own activity, Need/Offering creation, owner-authorised Need matching, authenticated persisted Offering search/detail and configured scoring weights. S4 adds member blocks, private reports, restricted operator cases and a local safety-event outbox/journey ledger. S5–S9 add connection requests, accepted Engagement snapshots, persistent participant conversations, independent outcomes and reciprocal reviews with derived role-specific reputation. S10 adds optional review photos and private case evidence through quarantined S3 uploads, server-side image validation, metadata-free variants, parent-bound serving, reports and withdrawal. Match preview and Offering matches remain placeholders. AI parsing is unavailable: authenticated parse routes return 503 ProblemDetails with manual-entry guidance, and structured posting starts and works without an AI key. Setting a key does not enable extraction; the vendor adapter is not implemented. The related-wording vocabulary is small and awaits owner-reviewed acceptance fixtures. Measurement remains future work. S10 has local and disposable LocalDB evidence; real S3/Cognito browser, accessibility, staging and operating-policy gates remain open.

## Current HTTP contracts

| Route | Behaviour |
|---|---|
| GET /health | Anonymous host response; not database readiness |
| GET /api/users/me | Authenticated self profile and subject-based provisioning |
| PATCH /api/users/me | Own firstName, lastName, city and bio only; length validation |
| GET /api/users/me/activity | Own Needs/Offerings; page >= 1, pageSize 1–100, separate totals |
| POST /api/needs | Member-owned Need creation |
| POST /api/offerings | Member-owned Offering creation |
| GET /api/needs/{needId}/matches | Owner-authorised matching with public Offering details, factor scores, reasons and policy version |
| GET /api/discover/offerings | Authenticated, paginated published Offering search; optional query, category and availableToday filters |
| GET /api/discover/offerings/{id} | Authenticated public Offering detail with self/inactive/legacy visibility exclusions |
| GET /api/safety/blocks; PUT or DELETE /api/safety/blocks/{memberId} | Own block list and idempotent block/unblock; block excludes both directions from discovery and Need matching |
| POST /api/safety/reports; GET /api/safety/reports | Private report creation and own-report list; reason is unsafe, harassment, fraud or other |
| GET /api/safety/cases; GET /api/safety/cases/{id}; POST /api/safety/cases/{id}/review | Configured Cognito operator group only; review records acknowledgement, not resolution |
| GET /api/offerings/{id}/matches | Owner check then placeholder response |
| GET /api/matches/preview | Placeholder response |
| POST /api/ai/parse/need or /offering | Authenticated 503 application/problem+json; manual entry remains available |
| POST /api/media/upload-grants | Participant-only review or disputed-case grant; private S3 POST policy fixes key, content type, 5 MB maximum and five-minute lifetime |
| POST /api/media/{id}/finalise | Uploader-only processing; checks actual JPEG/PNG/WebP format, size, dimensions and frames; writes an oriented, resized JPEG without source metadata |
| GET /api/media/agreements/{id}; GET /api/media/{id}/content | Parent-aware listing and authenticated, no-store image streaming; hidden review and private case rules apply on every read |
| GET /api/media/agreements/{id}/case-evidence | Configured operator-only case evidence listing; staff evidence reads are audited |
| POST /api/media/{id}/withdraw; POST /api/media/{id}/report; POST /api/media/{id}/exclude | Uploader withdrawal, published-photo reporting and operator removal; access is denied before deferred object cleanup |

There is no custom password/user-create route. /users/me contains private self-profile fields; public profiles need separate DTOs. Use request/controller types for exact payloads. Payments, escrow and delivery logistics remain outside initial proposed scope.

## Structure and boundaries

Connecto.Api owns transport/authentication; Connecto.Application owns use cases, matching/calculators and parsing interfaces; Connecto.Domain holds current entities/models; Connecto.Infrastructure owns EF mappings, services, migrations and seed data; Connecto.Database is the migration runner; Connecto.Tests.Unit contains tests. These are technical layers in one deployment, not microservices.

Introduce domain ownership for Members, Listings, Discovery, Connections, Engagements, Conversations, Feedback/Reputation, Media, Safety and Measurement. Only domain owners write their records. Keep Connections/Engagements together until acceptance recovery and policies are verified. Do not fabricate accepted engagements from score rows or review-derived reputation from legacy fields.

## Local development

Use .NET 10 SDK and SQL Server/LocalDB. Configure Cognito:Issuer (HTTPS issuer) and Cognito:Audience (public app client ID). OpenAI configuration is optional and currently does not activate parsing; no key is needed for structured listing creation. Never commit credentials. ConnectionStrings:DefaultConnection is local fallback. Database:Server overrides it and requires Database:Name, Username and Password. Deployed connection construction enables SQL encryption and certificate validation.

Set `Safety:OperatorGroup` to the exact Cognito group name for authorised safety staff. Its default empty value denies case access. Run the additive `AddSafetyAndOutbox` migration through the reviewed migration path before starting an API with S4 writes. The dispatcher runs every five seconds and retries undelivered events; the SQL receipt key deduplicates journey-ledger effects. Real SQL crash/replay and operator-process acceptance are still required before a member pilot.

S10 requires `Media:Bucket` and an AWS region/credentials through the SDK chain. Apply `AddMediaAssets` and `AddMediaAccessAudit` before enabling uploads. Terraform declares a private, encrypted bucket, exact-origin POST CORS, quarantine lifecycle and a scoped media task role; the existing API ECS task is managed outside this Terraform module, so attach the exported role and bucket configuration in that deployment before use. Keep the bucket private. The API returns 503 for upload/finalisation while `Media:Bucket` is unset. Review uploads allow three ready or live pending images per participant; case evidence allows five. Rejected and expired pending images do not consume that quota. The cleanup worker retries withdrawn/rejected/orphan removal hourly; S3 also expires abandoned quarantine objects after a day. A failed image never blocks text feedback. The S3 path and browser CORS are not yet verified in a deployed environment.

From this repository:

```powershell
dotnet restore Connecto.Api/Connecto.Api.csproj
dotnet build Connecto.Api/Connecto.Api.csproj
dotnet test Connecto.Tests.Unit/Connecto.Tests.Unit.csproj
dotnet run --project Connecto.Api/Connecto.Api.csproj --launch-profile http
```

HTTP profile uses http://localhost:5247; HTTPS profile additionally uses https://localhost:7249. Development Swagger is /swagger. Configure/review the target database before running migration commands; migrations mutate persistent data. These commands are guidance, not checks executed by this README task.

## Contributor guidelines

Keep controllers thin and explicit DTOs at boundaries; derive identity server-side and enforce domain invariants in application/domain code. Invert current Application-to-Infrastructure dependencies incrementally. Use versioned producer-owned API/events, pinned clients and compatibility tests; avoid shared EF entities or a shared mutable business-domain library. Cutover preserves stable IDs, one authoritative writer and additive migrations.

Verify transition/concurrency, idempotency, ownership/participants/staff checks, queue replay and acceptance recovery as capabilities ship. Unknown/restricted/private data must not become fabricated fit or public projections. Document unresolved policies rather than inventing numeric durations/targets.

## Security and operations

Current JWT validation checks signature/RS256, issuer, lifetime, access token_use, client_id and subject. ID tokens or unsigned identity headers are not API credentials. Need/Offering owner policies must expand to explicit participant/staff checks for new domains. Private/internal HTTP calls require workload identity and user authority where applicable; AWS task-role IAM alone does not authorise ASP.NET endpoints.

Terraform's public Cognito client now allows USER_PASSWORD_AUTH and REFRESH_TOKEN_AUTH to match the frontend, retaining SRP compatibility and no client secret. AWS has not been updated; review the plan against existing state before applying. Verify real registration/login/refresh and public/downstream TLS before closing the identity gate. The declared ALB is still HTTP. Separate runtime grants from migration/master credentials; verify SQL trust, network routes, scoped secrets and actual AWS state. Keep private content/tokens out of logs and public DTOs.

The workspace contract check `node --test docs/domain-design/identity-contract.test.mjs` runs from the Connecto root and compares real frontend auth requests with the sibling API Terraform configuration. It is separate from app-only tests so an independent app checkout does not require the API repository. See the foundation progress file for runtime acceptance still outstanding.

AWS ECS/ECR/RDS/Cognito is the documented baseline. Gate API rollout on matching-image migration success. API tasks/services currently referenced externally need state-preserving platform migration, not blind recreation. No Terraform resources moved to connecto-platform in this task. See ../docs/continuation.md and AUTHENTICATION.md; live AWS verification remains necessary.

## Supporting design and change control

S11 has durable discovery and suitability signals at `POST /api/pilot/signals/discovery` and `/suitable`, plus a read-only pilot report at `GET /api/pilot/metrics?from=<UTC>&to=<UTC>`. Configure `Pilot:ViewerGroup` for read-only pilot viewers; the configured `Safety:OperatorGroup` also has access. Both deny by default when unconfigured. The half-open window is limited to 366 days. Discovery coverage counts distinct searched Needs with a server-verified eligible candidate; suitability counts explicit Need–Offering marks. The request-created cohort exposes conversion, completion, disagreement, feedback, time-to-connect and repeat-participant numerators and denominators. Outcomes after the selected request window can change the live snapshot. See [S11 local evidence](../docs/test-evidence/2026-10-11/s11-local-measurement.md). Do not use this report as pilot acceptance until owner targets, real Cognito/staging verification, recovery drills and full journey review are complete.

- [Domain boundaries](../docs/domain-design/business-boundaries.md)
- [Microservices/app migration](../docs/domain-design/microservices.md)
- [Security and repository catalogue](../docs/domain-design/microservices-validation-security-repositories.md)
- [Document change register](../docs/document-change-register.md)

Parent AGENTS.md requires assessment/register proposals for material scope/contracts/security/deployment changes and explicit approval before canonical Word edits. README updates do not approve unresolved policies. Confirm repository ownership/license before redistribution; this README establishes no license.
