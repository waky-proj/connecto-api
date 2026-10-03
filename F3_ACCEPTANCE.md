# F3 opt-in acceptance path

This path exercises the implemented S1 profile and own activity APIs against **real SQL Server**. A separate browser journey checks that a new browser context recovers A's saved profile and Need after login while B sees neither. It is local/environment acceptance only when actually run. The SQL test uses locally signed test JWTs; it does not test Cognito. The browser journey calls the configured Cognito service and requires two disposable confirmed accounts. Neither path establishes staging, public HTTPS, migration recovery or production readiness.

## SQL Server API test

Prerequisites: .NET 10 SDK and a **dedicated disposable SQL Server database** with the current `Connecto.Infrastructure` EF migrations already applied. Use a connection string with the desired encryption and certificate trust settings. The test never creates or migrates a database. It writes uniquely named A/B users and listings, then removes those rows in `finally`; use a disposable database in case a process is interrupted before cleanup. Existing rows must not be touched. The test checks the migration state before writing and fails if migrations are pending or SQL Server is unreachable.

From `connecto-api` in PowerShell:

```powershell
$env:CONNECTO_F3_SQL_CONNECTION = 'Server=...;Database=...;User Id=...;Password=...;Encrypt=True;TrustServerCertificate=False'
$env:CONNECTO_F3_ALLOW_DATABASE_WRITES = 'YES'
dotnet test Connecto.Tests.Unit/Connecto.Tests.Unit.csproj --configuration F3Acceptance --filter 'Category=SqlServerAcceptance' --verbosity normal
```

Use `Integrated Security=True` instead of username/password where appropriate. Keep connection strings out of committed files and shell transcripts. The `F3Acceptance` build configuration also avoids the normal Debug output when a local API process has those files open. Without both environment variables, the test is reported as **skipped**. A configured but unreachable or unmigrated database fails rather than skipping.

The test verifies profile persistence across a newly created API host, the four editable fields, 201/2001-character validation, ignored identity/email/reputation assignments, separate A/B subject rows, own listing ownership, A-only paging across two pages, B isolation and forbidden access to A's Need/Offering matches. It also checks anonymous profile access. Signed JWTs are wired only into the in-process test host.

## Fresh browser session

Prerequisites: the frontend and API are running, the API points to a SQL Server database with current migrations, the frontend has `VITE_COGNITO_REGION` and `VITE_COGNITO_CLIENT_ID`, the API has matching `Cognito__Issuer` and `Cognito__Audience`, and two **disposable, confirmed** Cognito users can log in through `USER_PASSWORD_AUTH`. The Cognito public client must permit that flow. Install Playwright and Chromium in the frontend checkout if absent:

```powershell
cd ../connecto-app
npm install --no-save --package-lock=false playwright
npx playwright install chromium
```

For a local run, start the API and Vite in separate terminals with these settings, supplying real values through your secret manager or terminal environment:

```powershell
# API terminal, from connecto-api
$env:ConnectionStrings__DefaultConnection = 'Server=...;Database=...;User Id=...;Password=...;Encrypt=True;TrustServerCertificate=False'
$env:Cognito__Issuer = 'https://cognito-idp.<region>.amazonaws.com/<pool-id>'
$env:Cognito__Audience = '<public-app-client-id>'
dotnet run --project Connecto.Api/Connecto.Api.csproj --configuration F3Acceptance --no-launch-profile --urls http://127.0.0.1:5247
```

```powershell
# Frontend terminal, from connecto-app
$env:CONNECTO_API_TARGET = 'http://127.0.0.1:5247'
$env:VITE_COGNITO_REGION = '<region>'
$env:VITE_COGNITO_CLIENT_ID = '<public-app-client-id>'
npm run dev
```

Then run from `connecto-app`:

```powershell
$env:CONNECTO_F3_BROWSER_URL = 'http://127.0.0.1:5173'
$env:CONNECTO_F3_EMAIL_A = '<confirmed-test-account-a-email>'
$env:CONNECTO_F3_PASSWORD_A = '<account-a-password>'
$env:CONNECTO_F3_EMAIL_B = '<confirmed-test-account-b-email>'
$env:CONNECTO_F3_PASSWORD_B = '<account-b-password>'
npm run acceptance:f3:browser
```

The browser script changes A's profile and creates a uniquely titled Need. It closes that browser context, logs in as B in another fresh context, checks B cannot see A's profile marker or Need, and logs in as A in a third context to check both are recovered. Use disposable accounts/database because these browser-created records remain for review. Missing browser variables or Playwright/Chromium cause a skip or prerequisite error, never a pass. Record the actual environment, run time, outcome and any cleanup separately before marking F3 acceptance complete. This script does not exercise registration, refresh, delayed response races, or a deployed staging path.
