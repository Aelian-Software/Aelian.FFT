# Aelian.FFT

[![GitHub](https://img.shields.io/github/license/Aelian-Software/Aelian.FFT)](https://github.com/Aelian-Software/Aelian.FFT/blob/main/LICENSE) [![Nuget](https://img.shields.io/nuget/v/Aelian.FFT)](https://www.nuget.org/packages/Aelian.FFT/)

A highly optimized fast fourier transform implementation for .NET 8 and up, written in 100% pure c#, so there are no dependencies on native libraries.

It utilizes an in-place iterative Radix-2 Cooley-Tukey algorithm tuned for SIMD, and has support for both complex-valued and real-valued input.

To my knowledge, it is the fastest .NET FFT implementation that is freely and publicly available.

## Usage

```c#
using Aelian.FFT;

// Call Initialize () once when your program is loading
FastFourierTransform.Initialize ();

// Transform real-valued data:

var RealBuffer = new double[4096];
// Fill RealBuffer with meaningful data here
FastFourierTransform.RealFFT ( RealBuffer, /* forward: */ true );

// Transform complex-valued data:

var ComplexBuffer = new System.Numerics.Complex[4096];
// Fill ComplexBuffer with meaningful data here
FastFourierTransform.FFT ( ComplexBuffer, /* forward: */ true );
```

> **Note** that Aelian.FFT utilizes an _in-place_ algorithm, which means your input data is overwritten with output data. This saves costly memory allocation and memory access penalties, but might not always be the most convenient or intuitive approach. 

## Limitations

Certain optimizations in Aelian.FFT consist of precomputing values in tables. These tables take up some memory and take a short time to initialize. They have a fixed maximum size that limits the maximum input size for the FFT. 

The current limit for `Constants.MaxTableDepth` is 18, which limits the maximum FFT input to 65,536 samples (or 32,768 complex values). Increasing the value of `Constants.MaxTableDepth` also increases the memory usage exponentially, so while it is certainly possible to process larger FFT's by increasing this value, it is currently set to what I considered to be a reasonable limit.

In the future I might make this value configurable.

## Benchmarks comparing Aelian.FFT to other .NET FFT implementations

Benchmarks ran on a 11th Gen Intel Core i9-11900K 3.50GHz, 1 CPU, 16 logical and 8 physical cores, using .NET 8.0.5 (8.0.524.21615), X64 RyuJIT AVX2

The following alternative implementations were used in this benchmark:

- [Chris Lomont's C# Fast Fourier Transform](https://lomont.org/software/misc/fft/LomontFFT.html)
- [NAudio](https://github.com/naudio/NAudio)
- [MathNet.Numerics](https://github.com/mathnet/mathnet-numerics)
- [FftSharp](https://github.com/swharden/FftSharp)
- [NWaves](https://github.com/ar1st0crat/NWaves)

### Real-valued input

Each benchmark represents running 10,000 transforms with an input of 4096 real values:

|                       Method |    N |        Mean |     Error |    StdDev | Ratio | RatioSD |
|----------------------------- |----- |------------:|----------:|----------:|------:|--------:|
|               Aelian_RealFFT | 4096 |   122.01 ms |  2.079 ms |  2.775 ms |  1.00 |    0.00 |
|         Aelian_RealFFT_Split * | 4096 |    83.81 ms |  0.660 ms |  0.551 ms |  0.68 |    0.02 |
|       Aelian_RealFFT_Inverse | 4096 |   127.80 ms |  1.394 ms |  1.164 ms |  1.04 |    0.03 |
| Aelian_RealFFT_Inverse_Split * | 4096 |    86.83 ms |  0.973 ms |  0.910 ms |  0.71 |    0.02 |
|         NWaves_RealFFT_Split | 4096 |   151.71 ms |  0.672 ms |  0.596 ms |  1.23 |    0.03 |
| NWaves_RealFFT_Inverse_Split | 4096 |   159.52 ms |  0.842 ms |  0.703 ms |  1.29 |    0.03 |
|               Lomont_RealFFT | 4096 |   247.15 ms |  4.765 ms |  4.224 ms |  2.01 |    0.06 |
|       Lomont_RealFFT_Inverse | 4096 |   270.25 ms |  1.863 ms |  1.555 ms |  2.19 |    0.05 |
|              MathNet_RealFFT | 4096 | 1,078.25 ms |  5.293 ms |  4.692 ms |  8.78 |    0.24 |
|      MathNet_RealFFT_Inverse | 4096 |   905.07 ms |  5.071 ms |  4.235 ms |  7.34 |    0.19 |
|             FftSharp_RealFFT | 4096 | 2,801.29 ms | 55.419 ms | 92.593 ms | 23.17 |    1.09 |
|     FftSharp_RealFFT_Inverse | 4096 | 2,878.72 ms | 33.555 ms | 29.746 ms | 23.44 |    0.75 |

> **Note** that NAudio does not have a real-valued FFT, so it is omitted from this benchmark.

> \* The split data overload of RealFFT is faster than the interleaved one, since it can skip unzipping and rezipping, but it is also less practical since it splits out the real-valued samples in even and odd arrays.

### Complex-valued input

Each benchmark represents running 10,000 transforms with an input of 4096 complex values:

|                   Method |    N |       Mean |    Error |   StdDev |     Median | Ratio | RatioSD |
|------------------------- |----- |-----------:|---------:|---------:|-----------:|------:|--------:|
|               Aelian_FFT | 4096 |   295.4 ms |  2.94 ms |  2.29 ms |   296.2 ms |  1.00 |    0.00 |
|         Aelian_FFT_Split | 4096 |   184.2 ms |  0.83 ms |  0.77 ms |   184.1 ms |  0.62 |    0.01 |
|       Aelian_FFT_Inverse | 4096 |   307.5 ms |  2.49 ms |  2.21 ms |   307.4 ms |  1.04 |    0.01 |
| Aelian_FFT_Inverse_Split | 4096 |   192.4 ms |  1.19 ms |  1.05 ms |   192.1 ms |  0.65 |    0.00 |
|           NWaves_FFT | 4096 |   560.8 ms | 11.08 ms | 26.77 ms |   568.3 ms |  1.90 |    0.10 |
|   NWaves_FFT_Inverse | 4096 |   617.0 ms | 13.72 ms | 38.26 ms |   633.1 ms |  2.14 |    0.03 |
|            NAudio_FFT_32 * | 4096 |   501.1 ms |  7.82 ms |  7.31 ms |   500.7 ms |  1.70 |    0.02 |
|    NAudio_FFT_Inverse_32 * | 4096 |   194.5 ms |  1.71 ms |  1.52 ms |   194.7 ms |  0.66 |    0.01 |
|               Lomont_FFT | 4096 |   584.0 ms |  7.60 ms |  6.34 ms |   583.7 ms |  1.97 |    0.02 |
|       Lomont_FFT_Inverse | 4096 |   637.8 ms | 11.32 ms | 10.58 ms |   638.0 ms |  2.15 |    0.04 |
|              MathNet_FFT | 4096 |   800.4 ms |  6.09 ms |  5.40 ms |   799.4 ms |  2.71 |    0.03 |
|      MathNet_FFT_Inverse | 4096 |   806.9 ms |  5.29 ms |  4.69 ms |   806.2 ms |  2.73 |    0.03 |
|             FftSharp_FFT | 4096 | 2,750.8 ms | 53.29 ms | 49.85 ms | 2,735.5 ms |  9.28 |    0.20 |
|     FftSharp_FFT_Inverse | 4096 | 2,873.4 ms | 47.58 ms | 42.18 ms | 2,867.0 ms |  9.72 |    0.17 |

> \* NAudio only supports its own Complex value type using 32-bit floats, so while it is faster in case of an inverse FFT, its output is also far less precise.

If you know of a .NET FFT implementation that you think belongs in this list, please let me know.
