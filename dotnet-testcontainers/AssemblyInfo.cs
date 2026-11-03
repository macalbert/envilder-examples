// The fixtures resolve envilder.json from your real cloud while other tests point the
// default AWS and Azure credentials at the emulators through process environment
// variables, so the test classes must not run at the same time.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]
