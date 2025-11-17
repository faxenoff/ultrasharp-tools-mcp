using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using UltrasharpTools.Tools.Infrastructure;

namespace UltrasharpTools.Benchmarks;

/// <summary>
/// Benchmarks for BloomFilter Span&lt;T&gt; optimizations in GetStableHashCode.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
public class BloomFilterBenchmarks
{
private BloomFilter _filter = null!;
private string[] _typicalSymbols = null!;
private string[] _longSymbols = null!;

[GlobalSetup]
public void Setup()
{
_filter = new BloomFilter(expectedElements: 10000, falsePositiveRate: 0.01);

// Typical C# symbol names (short, should use stackalloc)
_typicalSymbols = new[]
{
"MyNamespace.MyClass",
"System.String",
"Microsoft.CodeAnalysis.ISymbol",
"UltrasharpTools.Tools.Services.CodeAnalysisService",
"MyProject.Domain.Entities.CustomerRepository"
};

// Very long FQNs (should use ArrayPool)
_longSymbols = new[]
{
"MyCompany.MyProject.Infrastructure.Services.Implementation.CustomerManagement.Repositories.EntityFramework.GenericRepositoryWithUnitOfWorkPattern",
"System.Runtime.CompilerServices.Unsafe.As<T, TTo>(ref T source)" + new string('a', 200)
};

// Pre-populate filter
foreach (var symbol in _typicalSymbols)
{
_filter.Add(symbol);
}
}

[Benchmark(Baseline = true)]
public void Add_TypicalSymbols()
{
foreach (var symbol in _typicalSymbols)
{
_filter.Add(symbol);
}
}

[Benchmark]
public void Add_LongSymbols()
{
foreach (var symbol in _longSymbols)
{
_filter.Add(symbol);
}
}

[Benchmark]
public bool MightContain_TypicalSymbols()
{
bool result = false;
foreach (var symbol in _typicalSymbols)
{
result |= _filter.MightContain(symbol);
}
return result;
}

[Benchmark]
public bool MightContain_LongSymbols()
{
bool result = false;
foreach (var symbol in _longSymbols)
{
result |= _filter.MightContain(symbol);
}
return result;
}
}
