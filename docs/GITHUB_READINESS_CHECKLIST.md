# GitHub Readiness Checklist

Use this checklist before pushing MATGER.

## Required Checks

```powershell
dotnet restore .\MATGER.sln
dotnet build .\MATGER.sln
dotnet test .\MATGER.sln
```

Expected before pushing:

```text
Build succeeded
Tests passed
45/45
```

## Database Check

```powershell
dotnet ef database update --project .\MATGER.Api\MATGER.Api.csproj --startup-project .\MATGER.Api\MATGER.Api.csproj
```

Expected:

```text
Done.
```

## Startup Check

```powershell
dotnet run --project .\MATGER.Api\MATGER.Api.csproj
```

Expected:

- API starts.
- Swagger available in Development.
- Demo seed runs without exception.
- No startup crash.

## Files That Must Not Be Pushed

- `.env`
- `bin/`
- `obj/`
- `logs/`
- `TestResults/`
- `coverage/`
- `*.zip`
- local backups

## Documentation Check

- README is updated.
- Demo accounts are documented.
- Security notes exist.
- Workflows are documented.
- Demo API flow exists.
- Final status is documented.

## Final Git Commands

```powershell
git status
git add .
git commit -m "Prepare MATGER backend for GitHub portfolio release"
git push
```

Only push after build, tests, database update, and startup all succeed.
