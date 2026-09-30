# Authentication implementation

Implemented against the existing Connecto repository, preserving the projects, SQL Server model, migration history, RDS/networking, and existing ECS deployment approach.

## Behavior

- Cognito owns registration, login, email verification, recovery, and passwords. The public app client uses SRP and refresh-token authentication; no client secret, hosted UI, callback URLs, or OAuth domain is introduced.
- All controller endpoints require authentication by default; `/health` remains public.
- The API validates RS256 signatures through Cognito discovery/JWKS, issuer, expiry, `token_use=access`, `client_id`, and a nonempty `sub`. `Cognito:Audience` is the app client ID and is validated against `client_id`, not the ID token's `aud`.
- `ICurrentUser` lazily provisions an internal GUID user for each subject. SQL Server's filtered unique index enforces uniqueness; concurrent duplicate inserts reload the winning row.
- `GET /api/users/me` returns a profile projection without navigation properties or the external subject ID.
- Needs/Offerings creation derives ownership from the token; `X-User-Id` is ignored. Both match endpoints require the Owner policy. The offering matches endpoint retains its existing placeholder response after authorization.
- Existing profiles keep null subjects. No automatic email linking is performed. New profile fields begin empty because access tokens normally omit email/name claims. Profile editing, administrative roles, and advanced identity verification remain separate work.
- The previous placeholder `/api/user` endpoints are replaced by `/api/users/me`.

## Files changed

- `Connecto.Api/Authentication/{CognitoAuthentication,CurrentUser,OwnershipAuthorization}.cs`
- `Connecto.Api/Controllers/{UserController,NeedsController,OfferingsController}.cs`
- `Connecto.Api/{Program.cs,appsettings.json,Connecto.Api.csproj}`
- `Connecto.Domain/Entities/User.cs`
- `Connecto.Infrastructure/Services/Data/ConnectoDbContext.cs`
- `Connecto.Infrastructure/Services/Data/Seed/SeedData.cs`
- `Connecto.Infrastructure/Migrations/*_AddAuthSubjectId.cs`, its designer, and the model snapshot
- `Connecto.Application/Internal/Impl/{NeedService,OfferingService}.cs`
- `Connecto.Tests.Unit/Authentication/AuthenticationTests.cs` and its project file
- `terraform/cognito.tf`
- `.github/workflows/deployment.yml`
- This document

Seed IDs now match the existing model snapshot. This fixes nondeterministic seeding so EF does not delete/recreate sample profiles and listings. Creation services initialize skill collections; Need creation also accepts the request's optional location safely.

## Verification

- .NET test suite: 16 passed, 0 failed. Tests use signed RSA JWTs through the actual bearer handler, with local discovery configuration and EF InMemory storage.
- Covers anonymous 401s, public health, authenticated profile access, repeated provisioning across requests, wrong signature/client/issuer, ID tokens, expired tokens, owner policy for both entities, authenticated creation, spoofed ownership header, and cross-user match access.
- EF migration generated using existing Infrastructure migrations and API startup; `has-pending-model-changes` passes. Migration adds nullable `AuthSubjectId` and a filtered unique index; seed updates only set the new column to null.
- Terraform fmt, init (without backend initialization), and validate pass.
- Read-only Terraform plan with the original state and variables: **2 add, 0 change, 0 destroy**. AWS credentials were available. Nothing was applied.
- Existing nullable-property/compiler warnings and Offering reputation decimal precision warning remain. SQL Server concurrency behavior is implemented but not exercised by the InMemory tests; no database was updated.

## Deployment

1. Review and apply Terraform using the original backend/state and deployment variables. Do not apply from a fresh empty state. This creates only Cognito resources according to the checked plan.
2. Set GitHub production environment variables `COGNITO_ISSUER` and `COGNITO_APP_CLIENT_ID` from the Terraform outputs `cognito_issuer` and `cognito_app_client_id`. They are public identifiers. The deployment workflow injects `Cognito__Issuer` and `Cognito__Audience` into the existing API task definition and refuses deployment if either is missing.
3. Apply the additive EF migration to SQL Server before deploying this API. Use the existing migration process after checking it actually runs EF migrations.
4. Deploy the API and configure the frontend with the pool ID, region, and app client ID. Send Cognito access tokens as Bearer tokens. Local runs also need the issuer and client ID via configuration/environment; missing values fail startup.

AWS apply, ECS deployment, and RDS migration require the appropriate credentials/permissions and have not been executed.

## Existing migration setup limitation

Terraform's migration task currently runs the API image with the API Docker entrypoint and no migration command override. The deployment migration workflow runs that task; the separate `Connecto.Database` program does not configure a SQL Server provider/connection. The task also injects the RDS master secret as the entire connection-string value (the secret normally contains JSON credentials). These pre-existing deployment issues were preserved and need resolution before relying on that task to apply this migration. No RDS, networking, task-definition, or migration-runner changes are included here.

Token validation follows https://docs.aws.amazon.com/cognito/latest/developerguide/amazon-cognito-user-pools-using-tokens-verifying-a-jwt.html.
