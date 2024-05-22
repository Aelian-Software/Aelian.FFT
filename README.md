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

### Real-valued input

Each benchmark represents running 10,000 transforms with an input of 4096 real values:

|                   Method |    N |       Mean |    Error |   StdDev | Ratio | RatioSD |
|------------------------- |----- |-----------:|---------:|---------:|------:|--------:|
|           Aelian_RealFFT | 4096 |   155.3 ms |  1.26 ms |  1.12 ms |  1.00 |    0.00 |
|   Aelian_RealFFT_Inverse | 4096 |   161.6 ms |  1.67 ms |  1.48 ms |  1.04 |    0.01 |
|           Lomont_RealFFT | 4096 |   226.8 ms |  0.92 ms |  0.82 ms |  1.46 |    0.01 |
|   Lomont_RealFFT_Inverse | 4096 |   273.3 ms |  1.79 ms |  1.59 ms |  1.76 |    0.02 |
|           NAudio_RealFFT * | 4096 |   218.8 ms |  4.28 ms |  4.58 ms |  1.42 |    0.03 |
|   NAudio_RealFFT_Inverse * | 4096 |   190.4 ms |  1.26 ms |  1.05 ms |  1.23 |    0.01 |
|          MathNet_RealFFT | 4096 | 1,130.3 ms |  6.78 ms |  5.66 ms |  7.28 |    0.06 |
|  MathNet_RealFFT_Inverse | 4096 |   899.2 ms |  2.57 ms |  2.40 ms |  5.79 |    0.05 |
|         FftSharp_RealFFT | 4096 | 2,684.7 ms | 40.48 ms | 35.89 ms | 17.29 |    0.22 |
| FftSharp_RealFFT_Inverse | 4096 | 2,858.4 ms | 42.35 ms | 47.07 ms | 18.38 |    0.38 |

> \* NAudio does not have a real-valued FFT, so we can only benchmark the complex FFT implementation. In addition to that, it only supports its own Complex value type using 32-bit floats, so it's kind of comparing apples and oranges.

### Complex-valued input

Each benchmark represents running 10,000 transforms with an input of 4096 complex values:

|               Method |    N |       Mean |    Error |   StdDev |     Median | Ratio | RatioSD |
|--------------------- |----- |-----------:|---------:|---------:|-----------:|------:|--------:|
|           Aelian_FFT | 4096 |   341.3 ms |  3.49 ms |  2.91 ms |   340.8 ms |  1.00 |    0.00 |
|   Aelian_FFT_Inverse | 4096 |   370.9 ms |  6.93 ms | 13.99 ms |   364.5 ms |  1.12 |    0.04 |
|           NAudio_FFT * | 4096 |   499.8 ms |  9.55 ms | 18.16 ms |   489.7 ms |  1.53 |    0.05 |
|   NAudio_FFT_Inverse * | 4096 |   190.8 ms |  1.04 ms |  0.93 ms |   190.7 ms |  0.56 |    0.00 |
|           Lomont_FFT | 4096 |   580.7 ms | 11.56 ms | 12.36 ms |   582.6 ms |  1.69 |    0.03 |
|   Lomont_FFT_Inverse | 4096 |   629.5 ms | 12.36 ms | 16.50 ms |   623.6 ms |  1.88 |    0.05 |
|          MathNet_FFT | 4096 |   803.2 ms |  8.48 ms |  7.93 ms |   803.7 ms |  2.35 |    0.02 |
|  MathNet_FFT_Inverse | 4096 |   796.4 ms |  5.13 ms |  4.79 ms |   796.1 ms |  2.33 |    0.02 |
|         FftSharp_FFT | 4096 | 2,657.6 ms | 23.92 ms | 22.38 ms | 2,651.7 ms |  7.79 |    0.11 |
| FftSharp_FFT_Inverse | 4096 | 2,833.6 ms | 26.66 ms | 23.63 ms | 2,831.3 ms |  8.30 |    0.11 |

> \* NAudio only supports its own Complex value type using 32-bit floats, so while it is faster in case of an inverse FFT, its output is also far less precise.

If you know of a .NET FFT implementation that you think belongs in this list, please let me know.
