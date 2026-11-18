// One emulator for the whole test run, shared by every test class.
// Tests stay independent: each one creates its own bucket and deletes it.
[assembly: AssemblyFixture(typeof(DocumentStorage.IntegrationTests.LocalStackFixture))]
