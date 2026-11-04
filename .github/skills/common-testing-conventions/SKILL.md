---
name: common-testing-conventions
description: Mandatory testing conventions for every example in this repo (.NET, Python, TypeScript): isolated tests with their own setup and cleanup, no disabled parallelism, AAA, naming. Use when writing tests or changing test fixtures.
user-invocable: false
---

# Testing Conventions Skill

Same conventions as the main Envilder repo
([`common-testing-conventions`](https://github.com/macalbert/envilder/tree/main/.github/skills/common-testing-conventions)),
with the rules below made explicit because every example here starts real
emulators (LocalStack, LocalStack for Azure) and talks to a real cloud.

## 1. Isolation (mandatory, every stack)

Tests follow Kent Beck's
[Test Desiderata](https://testdesiderata.com/): **isolated**, **deterministic**,
**order-independent**. Each test builds the environment it needs and removes it
afterwards, so it can run alone, in any order and in parallel with any other
test.

- **Own data per test.** Generate unique secret paths, secret names and map
  files per test (random suffix). Never share a fixed path or name between
  tests.
- **Setup and cleanup per test.** Create what the test needs before it, delete
  it after it, with the framework's lifecycle, never with `try/finally` in the
  test body:

  | Stack | Per-test setup and cleanup |
  | ----- | -------------------------- |
  | .NET (xUnit v3) | `IAsyncLifetime` on the test class (`InitializeAsync` / `DisposeAsync`); xUnit creates one instance per test |
  | Python (pytest) | function-scoped `yield` fixture |
  | TypeScript (Vitest) | `beforeEach` / `afterEach` |

  Expensive infrastructure (a container) may be shared through a class or
  module fixture, but only as an empty, stateless host: the data each test puts
  in it is created and removed by that test.
- **No process-wide state.** Do not change environment variables, global
  configuration, the current directory or static singletons to steer the code
  under test. Pass the dependency in (a client, a provider, a path).
- **Never disable parallelism.** No `Parallelization(Mode = ParallelMode.None)`,
  `DisableParallelization`, `[Collection]` used as a lock, `fileParallelism:
  false`, `--runInBand`, `-n 0` or similar. Needing one of them means the test is
  not isolated: fix the test.
- **If a library leaves no way to isolate a test** (for example, it only reads
  its client configuration from environment variables), do not work around it
  with global state. Report the limitation, propose a public seam in that
  library, and leave the test out until the seam exists.

## 2. AAA (mandatory)

- `// Arrange`, `// Act`, `// Assert` (`# ...` in Python), each at most once,
  never combined (`// Act & Assert` is not allowed).
- Act is a single call on the subject under test. Assertions only in Assert.
- Exceptions: C# `await act.Should().ThrowAsync<T>()`, Python
  `act = lambda: ...` then `pytest.raises` in Assert, Vitest
  `await expect(act).rejects.toThrow()`.
- No `if`, loops or `try/catch` inside a test.

## 3. Naming and variables

- `Should_{ExpectedBehavior}_When_{Condition}`, in every stack.
- `sut`, `expected`, `actual`.
