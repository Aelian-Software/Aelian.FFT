# Aelian.FFT

[![GitHub](https://img.shields.io/github/license/Aelian-Software/Aelian.FFT)](https://github.com/Aelian-Software/Aelian.FFT/blob/main/LICENSE) [![Nuget](https://img.shields.io/nuget/v/Aelian.FFT)](https://www.nuget.org/packages/Aelian.FFT/)

A highly optimized fast fourier transform implementation for .NET 10 and up, written in 100% pure c#, with no dependencies on any libraries.

It utilizes an in-place iterative Radix-2 Cooley-Tukey algorithm tuned for SIMD, and has support for both complex-valued and real-valued input.

To my knowledge, it is the fastest .NET FFT implementation that is freely and publicly available.

> 🚀 Version 1.1.0 has just been published, with additional optimizations that make it even faster!

## Usage

### Initialization

```c#
using Aelian.FFT;

// Call Initialize () once when your program is loading
FastFourierTransform.Initialize ();
```

### Transforming complex-valued data

```c#
using var SignalData = Aelian.FFT.AlignedSignalData.AllocateFromComplexSize ( N );

// Fill SignalData with meaningful data here

FastFourierTransform.FFT ( SignalData.AsComplex (), /* forward: */ true );

var ComplexSpectrum = SignalData.AsComplex ();
```

### Transforming real-valued data

```c#
using var SignalData = Aelian.FFT.AlignedSignalData.AllocateFromRealSize ( N );

// Fill SignalData with meaningful data here

FastFourierTransform.RealFFT ( SignalData.AsReal (), /* forward: */ true );

var ComplexSpectrum = SignalData.AsComplex ();
```

Or, alternatively, you can use your own buffers rather than `AlignedSignalData`:

```c#
var MySignalData = new double[4096];

// Fill MySignalData with meaningful data here

FastFourierTransform.RealFFT ( MySignalData, /* forward: */ true );
```

Although it is recommended to use `AlignedSignalData` for the best possible performance.

> **Note** that Aelian.FFT utilizes an _in-place_ algorithm, which means your input data is overwritten with output data. This saves costly memory allocation and memory access penalties, but might not always be the most convenient or intuitive approach. 

## Limitations

Certain optimizations in Aelian.FFT consist of precomputing values in tables. These tables take up some memory and take a short time to initialize. They have a fixed maximum size that limits the maximum input size for the FFT. 

The current limit, set using `Constants.MaxTableDepth`, is 18, which limits the maximum FFT input to 65,536 samples (or 32,768 complex values). Increasing the value of `Constants.MaxTableDepth` also increases the memory usage exponentially, so while it is certainly possible to process larger FFT's by increasing this value, it is currently set to what I considered to be a reasonable limit.

In the future I might make this value configurable.

## Benchmarks comparing Aelian.FFT to other .NET FFT implementations

Benchmarks ran on a AMD Ryzen 9 7950X3D 4.20GHz, 1 CPU, 32 logical and 16 physical cores, using .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v4, on Windows 11 Pro 25H2.

The following alternative implementations were used in this benchmark:

- [Chris Lomont's C# Fast Fourier Transform](https://lomont.org/software/misc/fft/LomontFFT.html)
- [NAudio 2.3.0](https://github.com/naudio/NAudio)
- [MathNet.Numerics 5.0.0](https://github.com/mathnet/mathnet-numerics)
- [FftSharp 2.2.0](https://github.com/swharden/FftSharp)
- [NWaves 0.9.6](https://github.com/ar1st0crat/NWaves)
- [fftflat 1.0.1](https://github.com/sinshu/fftflat)

### Real-valued input

Each row in the table represents a transform with an input of 4096 real values:

| Method                       | N    | Mean       | Error     | StdDev     | Median     | Ratio | RatioSD |
|----------------------------- |----- |-----------:|----------:|-----------:|-----------:|------:|--------:|
| **Aelian_RealFFT**               | 4096 |   **7.287 μs** | 0.0209 μs |  0.0186 μs |   7.288 μs |  1.00 |    0.00 |
| **Aelian_RealFFT_Split**         | 4096 |   **6.032 μs** | 0.0148 μs |  0.0131 μs |   6.031 μs |  0.83 |    0.00 |
| **Aelian_RealFFT_Inverse**       | 4096 |   **7.346 μs** | 0.0190 μs | 0.0169 μs |  7.347 μs |  1.01 |    0.00 |
| **Aelian_RealFFT_Inverse_Split** | 4096 |   **6.707 μs** | 0.0261 μs |  0.0244 μs |   6.712 μs |  0.92 |    0.00 |
| NWaves_RealFFT_Split         | 4096 |  10.972 μs | 0.0335 μs |  0.0313 μs |  10.981 μs |  1.51 |    0.01 |
| NWaves_RealFFT_Inverse_Split | 4096 |  12.737 μs | 0.0347 μs |  0.0307 μs |  12.747 μs |  1.75 |    0.01 |
| MathNet_RealFFT              | 4096 |  55.588 μs | 0.9413 μs |  1.0841 μs |  55.507 μs |  7.63 |    0.15 |
| MathNet_RealFFT_Inverse      | 4096 |  56.200 μs | 0.9124 μs |  0.8088 μs |  56.058 μs |  7.71 |    0.11 |
| Lomont_RealFFT               | 4096 |  17.387 μs | 0.3247 μs |  0.7328 μs |  17.525 μs |  2.39 |    0.10 |
| Lomont_RealFFT_Inverse       | 4096 |  19.628 μs | 0.3686 μs |  0.3448 μs |  19.574 μs |  2.69 |    0.05 |
| FftFlat_RealFFT              | 4096 |  10.658 μs | 0.0933 μs |  0.0827 μs |  10.661 μs |  1.46 |    0.01 |
| FftFlat_RealFFT_Inverse      | 4096 |  11.039 μs | 0.0692 μs |  0.0613 μs |  11.027 μs |  1.51 |    0.01 |
| FftSharp_RealFFT             | 4096 | 235.494 μs | 7.3672 μs | 21.7224 μs | 232.985 μs | 32.32 |    2.97 |
| FftSharp_RealFFT_Inverse     | 4096 | 239.624 μs | 7.0646 μs | 20.7193 μs | 233.636 μs | 32.89 |    2.83 |

> A μs is 1 Microsecond, 0.001 milliseconds or 0.000001 sec

> **Note** that NAudio does not have a real-valued FFT, so it is omitted from this benchmark.

> \* The split data overload of RealFFT is faster than the interleaved one, since it can skip unzipping and rezipping, but it is also less practical since it splits out the real-valued samples in even and odd arrays.

### Complex-valued input

Each row in the table represents a transform with an input of 4096 complex values:

| Method                   | N    | Mean      | Error    | StdDev   | Ratio | RatioSD |
|------------------------- |----- |----------:|---------:|---------:|------:|--------:|
| **Aelian_FFT**               | 4096 |  **14.59 μs** | 0.027 μs | 0.025 μs |  1.00 |    0.00 |
| **Aelian_FFT_Split**         | 4096 |  **12.09 μs** | 0.051 μs | 0.048 μs |  0.83 |    0.00 |
| **Aelian_FFT_Inverse**       | 4096 |  **14.33 μs** | 0.049 μs | 0.046 μs |  0.98 |    0.00 |
| **Aelian_FFT_Inverse_Split** | 4096 |  **12.81 μs** | 0.023 μs | 0.019 μs |  0.88 |    0.00 |
| NWaves_FFT               | 4096 |  63.12 μs | 1.260 μs | 3.019 μs |  4.33 |    0.21 |
| NWaves_FFT_Inverse       | 4096 |  67.95 μs | 0.324 μs | 0.287 μs |  4.66 |    0.02 |
| MathNet_FFT              | 4096 |  77.17 μs | 0.876 μs | 0.819 μs |  5.29 |    0.06 |
| MathNet_FFT_Inverse      | 4096 |  76.72 μs | 1.046 μs | 0.978 μs |  5.26 |    0.07 |
| Lomont_FFT               | 4096 |  57.74 μs | 0.199 μs | 0.187 μs |  3.96 |    0.01 |
| Lomont_FFT_Inverse       | 4096 |  67.38 μs | 0.403 μs | 0.377 μs |  4.62 |    0.03 |
| NAudio_FFT_32            | 4096 |  38.55 μs | 0.455 μs | 0.426 μs |  2.64 |    0.03 |
| NAudio_FFT_Inverse_32    | 4096 |  15.45 μs | 0.257 μs | 0.240 μs |  1.06 |    0.02 |
| FftFlat_RealFFT          | 4096 |  17.18 μs | 0.321 μs | 0.300 μs |  1.18 |    0.02 |
| FftFlat_RealFFT_Inverse  | 4096 |  17.07 μs | 0.264 μs | 0.247 μs |  1.17 |    0.02 |
| FftSharp_FFT             | 4096 | 203.76 μs | 1.089 μs | 1.019 μs | 13.96 |    0.07 |
| FftSharp_FFT_Inverse     | 4096 | 213.26 μs | 1.661 μs | 1.553 μs | 14.62 |    0.11 |

> A μs is 1 Microsecond, 0.001 milliseconds or 0.000001 sec

> \* NAudio only supports its own Complex value type using 32-bit floats, so while it is faster in case of an inverse FFT, its output is also far less precise.

> \* The split data overload of FFT is faster than the interleaved one, since it can skip unzipping and rezipping, but it is also less practical since it splits out the real and imaginary components in separate arrays.

If you know of a .NET FFT implementation that you think belongs in this list, please let me know.
