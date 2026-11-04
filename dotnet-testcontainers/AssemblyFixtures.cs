// One emulator of each kind for the whole test run, shared by every test class.
// Tests stay independent: each one stores its own uniquely named secrets and deletes them.
[assembly: AssemblyFixture(typeof(Envilder.Examples.Tests.LocalStackFixture))]
[assembly: AssemblyFixture(typeof(Envilder.Examples.Tests.LocalStackAzureFixture))]
