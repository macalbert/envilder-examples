# Document storage API (.NET 10)

A small ASP.NET Core Minimal API that will store documents in Amazon S3.

## Layout

```text
ai-agent-dotnet/
  envilder.json                     secret contract: variable names to AWS SSM paths, no values
  package.json                      pins the Envilder CLI (npx --no envilder ...)
  src/DocumentStorage.Api/          the API
  tests/DocumentStorage.UnitTests/  fast tests, no Docker, no cloud
  tests/DocumentStorage.IntegrationTests/  tests against LocalStack started by Testcontainers
```

## Conventions

- Test first. Follow [the repository's testing conventions](../.github/skills/common-testing-conventions/SKILL.md):
  isolated tests with their own setup and cleanup, AAA, `Should_{ExpectedBehavior}_When_{Condition}`.
- Keep it minimal: HTTP handling in the API, use cases in plain classes, AWS
  SDK calls in one adapter behind an interface. No mediator, no generic repository.
- NuGet versions live in the repository's [`Directory.Packages.props`](../Directory.Packages.props).
- The application reads ordinary configuration (environment variables,
  `appsettings.json`). It does not know where a value comes from.

## Secrets

[`envilder.json`](./envilder.json) declares the secrets this example needs.
[Envilder](https://envilder.com) resolves them from AWS SSM when an authorized
command runs. Nothing else in the repository holds a secret, and no `.env`
file is used.

## Run

```powershell
dotnet test --project tests/DocumentStorage.UnitTests
```

```powershell
npm ci; npx --no envilder validate --offline --map=envilder.json
```
