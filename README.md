# Aelian.FFT

A highly optimized fast fourier transform implementation for .NET 8 and up.
Utilizes an iterative Radix-2 Cooley-Tukey algorithm tuned for SIMD.

To my knowledge, it is the fastest FFT implementation that is freely and publicly available for .NET

## Usage

```c#
using Aelian.FFT;

// Call Initialize () once when your program is loading
FastFourierTransform.Initialize ();

var Buffer = new double[4096];
// Fill Buffer with meaningful data here
FastFourierTransform.RealFFT ( Buffer, /* forward: */ true );
```

> Note that Aelian.FFT utilizes an in-place algorithm, which means your input data is overwritten with output data. This saves costly memory allocation and memory access penalties, but might not always be the most convenient or intuitive approach. 

## Limitations

Certain optimizations in Aelian.FFT consist of precomputing values in tables. These tables take up some memory and take a short time to initialize. They have a fixed maximum size that limits the maximum input size for the FFT. 

The current limit for `Constants.MaxTableDepth` is 18, which in turn limits the maximum FFT input to 65,536 samples (or 32,768 complex values). Increasing the value of `Constants.MaxTableDepth` also increases the memory usage exponentially, so while it is certainly possible to process larger FFT's by increasing this value, it is currently set to what I considered to be a reasonable limit.

In the future I might make this value configurable.

## Benchmarks comparing Aelian.FFT to other .NET FFT implementations

Each benchmark represents running 10,000 transforms.

Benchmarks ran on a 11th Gen Intel Core i9-11900K 3.50GHz, 1 CPU, 16 logical and 8 physical cores, using .NET 8.0.5 (8.0.524.21615), X64 RyuJIT AVX2


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

> \* NAudio does not have a real-valued FFT, so we can only benchmark the complex FFT implementation. In addition to that, it only supports its own Complex value type using floats, so it's kind of comparing apples and oranges.

If you know of a .NET FFT implementation that you think belongs in this list, please let me know.
