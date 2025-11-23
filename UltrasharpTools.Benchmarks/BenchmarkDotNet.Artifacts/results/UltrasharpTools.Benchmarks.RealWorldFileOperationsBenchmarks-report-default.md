
BenchmarkDotNet v0.15.6, Windows 11 (10.0.26200.7171)
Intel Core Ultra 9 275HX 2.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.100
  [Host]   : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

 Method                                             | Mean             | Error             | StdDev         | Ratio  | RatioSD | Gen0    | Gen1    | Gen2    | Allocated | Alloc Ratio |
--------------------------------------------------- |-----------------:|------------------:|---------------:|-------:|--------:|--------:|--------:|--------:|----------:|------------:|
 'File.* - Read+Process+Write (10 files)'           | 16,558,664.58 ns |  1,645,723.919 ns |  90,207.682 ns | 42.375 |    0.60 | 93.7500 | 93.7500 | 93.7500 | 2168734 B |      14.937 |
 'OptimizedFileIO - Read+Process+Write (10 files)'  | 20,081,425.00 ns | 15,678,976.085 ns | 859,417.589 ns | 51.390 |    2.03 | 93.7500 | 93.7500 | 93.7500 | 2168956 B |      14.939 |
 'File.ReadAllTextAsync - Large Files'              |         11.76 ns |          2.060 ns |       0.113 ns |  0.000 |    0.00 |  0.0042 |       - |       - |      80 B |       0.001 |
 'OptimizedFileIO.ReadAllTextAsync - Large Files'   |         10.53 ns |          1.803 ns |       0.099 ns |  0.000 |    0.00 |  0.0042 |       - |       - |      80 B |       0.001 |
 'File.ReadAllTextAsync - Medium Files'             |  1,096,435.03 ns |    357,294.341 ns |  19,584.509 ns |  2.806 |    0.06 | 76.1719 | 37.1094 | 37.1094 | 1170874 B |       8.064 |
 'OptimizedFileIO.ReadAllTextAsync - Medium Files'  |  1,273,010.48 ns |  2,901,029.734 ns | 159,015.229 ns |  3.258 |    0.36 | 76.1719 | 37.1094 | 37.1094 | 1170891 B |       8.065 |
 'File.ReadAllTextAsync - Small Files'              |    390,828.30 ns |    109,166.071 ns |   5,983.761 ns |  1.000 |    0.02 |  7.8125 |  0.4883 |       - |  145190 B |       1.000 |
 'OptimizedFileIO.ReadAllTextAsync - Small Files'   |    379,075.32 ns |     41,349.170 ns |   2,266.488 ns |  0.970 |    0.01 |  7.8125 |  0.4883 |       - |  145209 B |       1.000 |
 'File.WriteAllTextAsync - Medium Files'            | 16,490,602.08 ns |  3,176,814.514 ns | 174,131.924 ns | 42.201 |    0.68 | 62.5000 | 31.2500 | 31.2500 | 1179758 B |       8.126 |
 'OptimizedFileIO.WriteAllTextAsync - Medium Files' | 19,579,619.27 ns |  3,592,249.973 ns | 196,903.343 ns | 50.106 |    0.80 | 62.5000 | 31.2500 | 31.2500 | 1180026 B |       8.127 |
 'File.WriteAllTextAsync - Small Files'             |  2,044,482.49 ns |  1,688,494.612 ns |  92,552.088 ns |  5.232 |    0.22 |  7.8125 |       - |       - |  153725 B |       1.059 |
 'OptimizedFileIO.WriteAllTextAsync - Small Files'  |  2,163,567.58 ns |    447,739.528 ns |  24,542.114 ns |  5.537 |    0.09 |  7.8125 |       - |       - |  154040 B |       1.061 |
