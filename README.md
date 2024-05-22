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
|           Aelian_RealFFT | 4096 |   130.8 ms |  2.05 ms |  1.82 ms |  1.00 |    0.00 |
|   Aelian_RealFFT_Inverse | 4096 |   130.9 ms |  0.63 ms |  0.59 ms |  1.00 |    0.02 |
|           Lomont_RealFFT | 4096 |   227.6 ms |  1.17 ms |  0.92 ms |  1.74 |    0.02 |
|   Lomont_RealFFT_Inverse | 4096 |   275.0 ms |  2.19 ms |  1.94 ms |  2.10 |    0.03 |
|           NAudio_RealFFT * | 4096 |   216.3 ms |  1.12 ms |  0.93 ms |  1.65 |    0.02 |
|   NAudio_RealFFT_Inverse * | 4096 |   190.7 ms |  0.96 ms |  0.85 ms |  1.46 |    0.02 |
|          MathNet_RealFFT | 4096 | 1,130.0 ms |  5.47 ms |  4.85 ms |  8.64 |    0.13 |
|  MathNet_RealFFT_Inverse | 4096 |   908.5 ms |  4.51 ms |  4.22 ms |  6.95 |    0.10 |
|         FftSharp_RealFFT | 4096 | 2,680.2 ms | 15.49 ms | 13.73 ms | 20.49 |    0.30 |
| FftSharp_RealFFT_Inverse | 4096 | 2,978.1 ms | 55.46 ms | 98.58 ms | 23.49 |    0.62 |

> \* NAudio does not have a real-valued FFT, so we can only benchmark the complex FFT implementation. In addition to that, it only supports its own Complex value type using 32-bit floats, so it's kind of comparing apples and oranges.

### Complex-valued input

Each benchmark represents running 10,000 transforms with an input of 4096 complex values:

|               Method |    N |       Mean |    Error |   StdDev | Ratio | RatioSD |
|--------------------- |----- |-----------:|---------:|---------:|------:|--------:|
|           Aelian_FFT | 4096 |   295.2 ms |  3.22 ms |  2.85 ms |  1.00 |    0.00 |
|   Aelian_FFT_Inverse | 4096 |   305.4 ms |  1.93 ms |  1.71 ms |  1.03 |    0.01 |
|           NAudio_FFT * | 4096 |   511.2 ms |  8.44 ms |  7.89 ms |  1.73 |    0.04 |
|   NAudio_FFT_Inverse * | 4096 |   199.1 ms |  3.77 ms |  4.49 ms |  0.68 |    0.02 |
|           Lomont_FFT | 4096 |   581.4 ms | 11.15 ms | 13.69 ms |  1.96 |    0.06 |
|   Lomont_FFT_Inverse | 4096 |   647.8 ms | 12.49 ms | 15.80 ms |  2.21 |    0.07 |
|          MathNet_FFT | 4096 |   799.0 ms |  8.86 ms |  7.86 ms |  2.71 |    0.04 |
|  MathNet_FFT_Inverse | 4096 |   810.8 ms | 11.90 ms |  9.94 ms |  2.75 |    0.05 |
|         FftSharp_FFT | 4096 | 2,774.1 ms | 30.18 ms | 26.76 ms |  9.40 |    0.12 |
| FftSharp_FFT_Inverse | 4096 | 2,881.9 ms | 53.64 ms | 47.55 ms |  9.76 |    0.17 |

> \* NAudio only supports its own Complex value type using 32-bit floats, so while it is faster in case of an inverse FFT, its output is also far less precise.

If you know of a .NET FFT implementation that you think belongs in this list, please let me know.
