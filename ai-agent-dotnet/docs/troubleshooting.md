# Troubleshooting

Every message on this page was produced by a real run while building this
example (Envilder 0.22.0, 2026-11-18). Paths are shortened.

## Which layer failed?

| Exit code of `envilder exec` | Where to look |
|------------------------------|---------------|
| `125` | Envilder: map file, AWS credentials, IAM, or a missing secret. Your command did not start |
| `126`, `127` | The command could not be executed or was not found |
| anything else | Your command's own exit code: here, `dotnet test` (`2` = test failures or a failing fixture) |

Inside a `dotnet test` failure, the first lines of the fixture error tell
LocalStack problems (container exit code, `License activation failed!`) apart
from application or assertion failures (`Expected ... but found ...`).

## Envilder

### The map file is not valid JSON

```text
✗ Map file invalid: .../envilder.json (1 problem)
  • Invalid JSON in parameter map file: .../envilder.json (line 3, column 1)
```

`validate --offline` exits `1`. Fix the file before any run that needs
credentials.

### The map file does not exist

```text
💥 GAME OVER — you fell down the wrong pipe!

  Failed to read map file: .../nope.json
```

`exec` exits `125`. `--map` is relative to the current directory: run from
`ai-agent-dotnet/` or pass the right path.

### A mapped secret does not exist, or the identity cannot read it

```text
💥 GAME OVER — some secrets could not be fetched
  ✗ LOCALSTACK_AUTH_TOKEN→ ******************************************ist

  ⮕ WHY?
     not found
```

`exec` exits `125` and never starts the tests. The path is masked. Check that
the parameter exists in the region your identity uses and that the identity
has `ssm:GetParameter` on it. An IAM denial is reported the same way with a
different reason.

### No AWS credentials

```text
💥 GAME OVER — some secrets could not be fetched
  ✗ LOCALSTACK_AUTH_TOKEN→ ***************************************ken

  ⮕ WHY?
     Could not load credentials from any providers
```

`exec` exits `125`. This message also appears when `--profile` names a profile
that does not exist in your AWS config: check the spelling before checking
your login. With an expired SSO session in an interactive terminal, Envilder
offers to run `aws sso login` and then retries the resolution.

### `npx --no envilder --version` prints the npm version

```text
> npx --no envilder --version
11.19.0
> npx --no envilder -- --version
0.22.0
```

With `--no` before the package name, npm treats `--version` and `--help` as
its own options. Put `--` before them, or use `npx --no -- envilder --version`.
Commands such as `validate` and `exec` are not affected.

## LocalStack

### `LOCALSTACK_AUTH_TOKEN is not set`

```text
LocalStackFixture threw in InitializeAsync
InvalidOperationException: LOCALSTACK_AUTH_TOKEN is not set. Run the integration tests through Envilder, ...
```

You ran `dotnet test` directly. Run it through `envilder exec` (see the
[README](../README.md#run-the-integration-tests-with-envilder)). Do not export
the token by hand or put it in a `.env` file.

### LocalStack quits with `License activation failed!`

```text
ContainerNotRunningException : Container ... exited with code 55.
  License activation failed! 🔑❌
  Reason: The credentials defined in your environment are invalid. ...
  Due to this error, Localstack has quit.
```

Envilder resolved a value and started the tests, but LocalStack rejected it.
The parameter holds an invalid or expired token. `localstack/localstack:stable`
requires a valid token even when the tests use only S3. Update the parameter
with a valid token (the root README explains how).

### Docker is not running

Testcontainers fails before any test runs. Start Docker and run again; nothing
needs to change in Envilder.

## Application and tests

### `DocumentStorage:BucketName` is required

The API validates its options at startup. Set `DocumentStorage__BucketName`
(environment) or `DocumentStorage:BucketName` (`appsettings.json`). The tests
set it themselves.

### A test fails with an assertion

That is the application or the test, not Envilder: the exit code is
`dotnet test`'s own. Envilder passes it through unchanged (for example, a
command that exits `3` makes `envilder exec` exit `3`).
