# Propr Review Demo C#

Small static blog review demo built with a native C# console app.

## Commands

- `dotnet run -- --output dist` builds the static site into `dist/`
- `npm install` installs Playwright test dependencies
- `npx playwright install --with-deps chromium` installs the browser used by e2e tests
- `npm test` runs the Playwright end-to-end suite against `dist/`

## Section Pipeline

- User-facing sections should reuse the existing section and article pipeline.
- New grouped destinations should keep the same content, ordering, and article rendering flow instead of introducing a parallel model.

## Review branches

- `BUG_SCENARIOS.md` lists the intentionally defective feature branches that should be reviewed against `main`.
