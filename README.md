# Propr Review Demo C#

Small static blog review demo built with a native C# console app.

## Commands

- `dotnet run -- --output dist` builds the static site into `dist/`
- `npm install` installs Playwright test dependencies
- `npx playwright install --with-deps chromium` installs the browser used by e2e tests
- `npm test` runs the Playwright end-to-end suite against `dist/`

## Content Model

- First-class site pages and sections should be content-driven from markdown under `content/`.
- New top-level destinations should be modeled there so routing, ordering, and discovery stay consistent.

## Review branches

- `BUG_SCENARIOS.md` lists the intentionally defective feature branches that should be reviewed against `main`.
