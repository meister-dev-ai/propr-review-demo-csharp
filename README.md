# Propr Review Demo C#

Small static blog review demo built with a native C# console app.

## Commands

- `dotnet run -- --output dist` builds the static site into `dist/`
- `npm install` installs Playwright test dependencies
- `npx playwright install --with-deps chromium` installs the browser used by e2e tests
- `npm test` runs the Playwright end-to-end suite against `dist/`
