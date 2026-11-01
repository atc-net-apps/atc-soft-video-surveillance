// Run in Release, all or some:
//   dotnet run -c Release --project benchmark/AtcSoft.Video.Surveillance.Benchmarks -- --filter "*"
//   dotnet run -c Release --project benchmark/AtcSoft.Video.Surveillance.Benchmarks -- --filter "*RecordingCatalog*"
BenchmarkSwitcher
    .FromAssembly(typeof(AtcSoft.Video.Surveillance.Benchmarks.Archive).Assembly)
    .Run(args);