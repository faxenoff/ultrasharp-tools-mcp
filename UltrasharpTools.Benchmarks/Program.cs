using BenchmarkDotNet.Running;
using UltrasharpTools.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
