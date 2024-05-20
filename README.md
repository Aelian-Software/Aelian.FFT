# Aelian.FFT

An optimized SIMD FFT implementation for .NET 8 and up.

Benchmarks comparing Aelian.FFT to other .NET FFT implementations

```
11th Gen Intel Core i9-11900K 3.50GHz, 1 CPU, 16 logical and 8 physical cores
.NET 8.0.5 (8.0.524.21615), X64 RyuJIT AVX2


|                 Method |    N |     Mean |   Error |  StdDev | Ratio |
|----------------------- |----- |---------:|--------:|--------:|------:|
|         Lomont_RealFFT | 4096 | 231.3 ms | 1.13 ms | 1.01 ms |  1.00 |
| Lomont_RealFFT_Inverse | 4096 | 268.1 ms | 3.16 ms | 2.80 ms |  1.16 |
|          AelianRealFFT | 4096 | 152.0 ms | 0.94 ms | 0.79 ms |  0.66 |
|  AelianRealFFT_Inverse | 4096 | 159.5 ms | 2.31 ms | 2.16 ms |  0.69 |
```
