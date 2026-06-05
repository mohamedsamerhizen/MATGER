# GitHub Publish Checklist

Run these commands before the final push:

```powershell
dotnet build .\MATGER.sln
dotnet test .\MATGER.sln
docker compose up -d matger-sqlserver
dotnet ef database update --project .\MATGER.Api --startup-project .\MATGER.Api
dotnet run --project .\MATGER.Api
```

Final repository checks:

- Confirm `.env` is not present in the Git commit.
- Confirm `.env.example` is committed and contains development-safe sample values only.
- Confirm `bin/`, `obj/`, `logs/`, and `TestResults/` are not committed.
- Confirm screenshots in `docs/screenshots/` are real captures before committing image files.
- Confirm `.github/workflows/ci.yml` is committed.
- Confirm README image links resolve after screenshot files are added.
