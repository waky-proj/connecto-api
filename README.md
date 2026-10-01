# connecto-api

Existing ASP.NET Core API and modular core | 1 October 2026

## Status and domain ownership

This repository contains the .NET 10 API, application/domain projects, EF Core SQL Server persistence, migration runner, tests, Docker and AWS Terraform/deployment configuration. It remains authoritative during staged migration. New service repositories are documentation scaffolds; creation does not extract any capability.

Implemented: Cognito access-token authentication and subject-based user provisioning, self-profile retrieval/allowlisted editing, paginated own activity, Need/Offering creation and owner-authorised Need matching. Match mapping is not a persistent accepted-agreement lifecycle. Discovery/preview and Offering matches include placeholders. AI parsing is unavailable: authenticated parse routes return 503 ProblemDetails with manual-entry guidance, and structured posting starts and works without an AI key. Setting a key does not enable extraction; the vendor adapter is not implemented. Matching-weight defaults are not wired into MatchingEngine. Connections, Engagements, persistent chat, Feedback, Media, Safety cases and measurement remain future work.

## Current HTTP contracts

| Route | Behaviour |
|---|---|
| GET /health | Anonymous host response; not database readiness |
| GET /api/users/me | Authenticated self profile and subject-based provisioning |
| PATCH /api/users/me | Own firstName, lastName, city and bio only; length validation |
| GET /api/users/me/activity | Own Needs/Offerings; page >= 1, pageSize 1–100, separate totals |
| POST /api/needs | Member-owned Need creation |
| POST /api/offerings | Member-owned Offering creation |
| GET /api/needs/{needId}/matches | Owner-authorised matching |
| GET /api/offerings/{id}/matches | Owner check then placeholder response |
| GET /api/matches/preview | Placeholder response |
| POST /api/discover/needs | Placeholder response |
| POST /api/ai/parse/need or /offering | Authenticated 503 application/problem+json; manual entry remains available |

There is no custom password/user-create route. /users/me contains private self-profile fields; public profiles need separate DTOs. Use request/controller types for exact payloads. Payments, escrow and delivery logistics remain outside initial proposed scope.

## Structure and boundaries

Connecto.Api owns transport/authentication; Connecto.Application owns use cases, matching/calculators and parsing interfaces; Connecto.Domain holds current entities/models; Connecto.Infrastructure owns EF mappings, services, migrations and seed data; Connecto.Database is the migration runner; Connecto.Tests.Unit contains tests. These are technical layers in one deployment, not microservices.

Introduce domain ownership for Members, Listings, Discovery, Connections, Engagements, Conversations, Feedback/Reputation, Media, Safety and Measurement. Only domain owners write their records. Keep Connections/Engagements together until acceptance recovery and policies are verified. Do not fabricate accepted engagements from score rows or review-derived reputation from legacy fields.

## Local development

Use .NET 10 SDK and SQL Server/LocalDB. Configure Cognito:Issuer (HTTPS issuer) and Cognito:Audience (public app client ID). OpenAI configuration is optional and currently does not activate parsing; no key is needed for structured listing creation. Never commit credentials. ConnectionStrings:DefaultConnection is local fallback. Database:Server overrides it and requires Database:Name, Username and Password. Deployed connection construction enables SQL encryption and certificate validation.

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

- [Domain boundaries](../docs/domain-design/business-boundaries.md)
- [Microservices/app migration](../docs/domain-design/microservices.md)
- [Security and repository catalogue](../docs/domain-design/microservices-validation-security-repositories.md)
- [Document change register](../docs/document-change-register.md)

Parent AGENTS.md requires assessment/register proposals for material scope/contracts/security/deployment changes and explicit approval before canonical Word edits. README updates do not approve unresolved policies. Confirm repository ownership/license before redistribution; this README establishes no license.
