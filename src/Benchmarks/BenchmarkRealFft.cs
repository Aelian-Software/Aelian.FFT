#define BENCHMARK_OTHERS
#define USE_ALIGNED

using System;
using System.Numerics;

using Aelian.FFT.Extensions;

using BenchmarkDotNet.Attributes;

namespace Benchmarks
	{
	public class BenchmarkRealFft : IDisposable
		{
		private const int FastRunCount = 500000;
		private const int RunCount = 100000;
		private const int MediumRunCount = 10000; // For slower implementations. Makes the benchmark not take forever.
		private const int SlowRunCount = 1000; // For slower implementations. Makes the benchmark not take forever.

		private double[]? _RandomData; // Randomly generated double values, this will not be written to after Setup ()

#if USE_ALIGNED
		private Aelian.FFT.AlignedSignalData? _IterationData; // Input: Samples, Output: complex spectrum interleaved real/imaginary values
		private Aelian.FFT.AlignedMemory<double>? _IterationSplitRealData; // Input: Even samples, Output: complex spectrum values' real components
		private Aelian.FFT.AlignedMemory<double>? _IterationSplitImaginaryData; // Input: Odd samples, Output: complex spectrum values' imaginary components
#else
		private Aelian.FFT.SignalData? _IterationData; // Input: Samples, Output: complex spectrum interleaved real/imaginary values
		private double[]? _IterationSplitRealData; // Input: Even samples, Output: complex spectrum values' real components
		private double[]? _IterationSplitImaginaryData; // Input: Odd samples, Output: complex spectrum values' imaginary components
#endif

		// Other implementation state

		private NWaves.Transforms.RealFft64? _NWavesRealFft64;
		private Lomont.LomontFFT _Lomont = new () { A = 1, B = -1 };
		private FftFlat.RealFourierTransform? _FftFlatReal;

		private double[]? _IterationDataArray;
		private double[]? _OutSamples;
		private double[]? _InRe;
		private double[]? _InIm;
		private double[]? _OutRe;
		private double[]? _OutIm;
		private float[]? _MathNetBuffer;
		private Complex[]? _InComplex;
		private double[]? _IterationDataArrayPlus2;

		// Benchmarks

		[Params ( 4096 )]
		public int N;

		[GlobalSetup]
		public void Setup ()
			{
			var Rnd = new Random ( 1337 );

			_RandomData = new double[N];

#if USE_ALIGNED
			_IterationData = Aelian.FFT.AlignedSignalData.AllocateFromRealSize ( N );
			_IterationSplitRealData = Aelian.FFT.AlignedMemory<double>.Allocate ( _IterationData.ComplexLength );
			_IterationSplitImaginaryData = Aelian.FFT.AlignedMemory<double>.Allocate ( _IterationData.ComplexLength );
#else
			_IterationData = Aelian.FFT.SignalData.CreateFromRealSize ( N );
			_IterationSplitRealData = new double[_IterationData.ComplexLength];
			_IterationSplitImaginaryData = new double[_IterationData.ComplexLength];
#endif

			for ( int i = 0; i < N; i++ )
				_RandomData[i] = Rnd.NextDouble () * 2.0 - 1.0;

			Aelian.FFT.FastFourierTransform.Initialize ();

			// Initialize other implementations

			_NWavesRealFft64 = new ( _IterationData.ComplexLength );
			_FftFlatReal = new FftFlat.RealFourierTransform ( _IterationData.RealLength );

			_IterationDataArray = new double[_IterationData.RealLength];
			_OutSamples = new double[_IterationData.RealLength];
			_InRe = new double[_IterationData.ComplexLength];
			_InIm = new double[_IterationData.ComplexLength];
			_OutRe = new double[_IterationData.ComplexLength];
			_OutIm = new double[_IterationData.ComplexLength];
			_InComplex = new Complex[_IterationData.ComplexLength + 1]; // FftSharp and FFtFlat demand spectrum size be a power of 2 + 1
			_MathNetBuffer = new float[_IterationData.RealLength + 2]; // MathNet demands that buffer size be N+2
			_IterationDataArrayPlus2 = new double[_IterationData.RealLength + 2]; // FFtFlat demands that buffer size be N+2
			}

		/// <summary>
		/// Initialize this benchmark iteration, copying the randomly generated _RealInputData to the buffers used for transformation
		/// </summary>
		[IterationSetup]
		public void IterationSetup ()
			{
			if
				(
				_IterationData is null
				|| _IterationSplitRealData is null
				|| _IterationSplitImaginaryData is null
				|| _RandomData is null
				|| _InRe is null
				|| _InIm is null
				|| _OutRe is null
				|| _OutIm is null
				|| _InComplex is null
				|| _MathNetBuffer is null
				|| _IterationDataArrayPlus2 is null
				)
				throw new Exception ( "Not all fields have been initialized" );

#if USE_ALIGNED
			_RandomData.AsSpan ().CopyTo ( _IterationData.AsSpanOf<double> () );
#else
			_RandomData.AsSpan ().CopyTo ( _IterationData.AsReal () );
#endif

			var ComplexSpan = _RandomData.AsSpan ().Cast<double, Complex> ();
			var SplitLength = ComplexSpan.Length;
			var SplitRealSpan = _IterationSplitRealData.AsSpan ();
			var SplitImaginarySpan = _IterationSplitImaginaryData.AsSpan ();

			for ( int i = 0; i < SplitLength; i++ )
				{
				SplitRealSpan[i] = ComplexSpan[i].Real;
				SplitImaginarySpan[i] = ComplexSpan[i].Imaginary;

				_InRe[i] = SplitRealSpan[i];
				_InIm[i] = SplitImaginarySpan[i];
				}

			// Iteration setup for other implementations

			_IterationDataArrayPlus2.AsSpan ().Clear ();
			_RandomData.AsSpan ().CopyTo ( _IterationDataArrayPlus2.AsSpan () );

			_RandomData.AsSpan ().CopyTo ( _IterationDataArray.AsSpan () );

			_MathNetBuffer.AsSpan ().Clear ();

			for ( int i = 0; i < _RandomData.Length; i++ )
				_MathNetBuffer[i] = (float) _RandomData[i];

			_OutRe.AsSpan ().Clear ();
			_OutIm.AsSpan ().Clear ();

			_InComplex.AsSpan ().Clear ();
			ComplexSpan.CopyTo ( _InComplex );
			}

		/*--------------------------------------------------------------\
		| Aelian.FFT                                                    |
		\* ------------------------------------------------------------*/

		[Benchmark ( Baseline = true, OperationsPerInvoke = FastRunCount )]
		public void Aelian_RealFFT ()
			{
			var RealSpan = _IterationData!.AsReal ();

			for ( int i = 0; i < FastRunCount; i++ )
				Aelian.FFT.FastFourierTransform.RealFFT ( RealSpan, true );
			}

		/// <summary>
		/// The split data overload of RealFFT is faster than the interleaved one, since it can skip unzipping and rezipping,
		/// but it is also less practical since it splits out the real-valued samples in even and odd arrays
		/// </summary>
		[Benchmark ( OperationsPerInvoke = FastRunCount )]
		public void Aelian_RealFFT_Split ()
			{
			var SplitRealSpan = _IterationSplitRealData!.AsSpan ();
			var SplitImagSpan = _IterationSplitImaginaryData!.AsSpan ();

			for ( int i = 0; i < FastRunCount; i++ )
				{
				Aelian.FFT.FastFourierTransform.RealFFT
					(
					SplitRealSpan,
					SplitImagSpan,
					true
					);
				}
			}

		[Benchmark ( OperationsPerInvoke = FastRunCount )]
		public void Aelian_RealFFT_Inverse ()
			{
			var RealSpan = _IterationData!.AsReal ();

			for ( int i = 0; i < FastRunCount; i++ )
				Aelian.FFT.FastFourierTransform.RealFFT ( RealSpan, false );
			}

		/// <summary>
		/// The split data overload of RealFFT is faster than the interleaved one, since it can skip unzipping and rezipping,
		/// but it is also less practical since it splits out the real-valued samples in even and odd arrays
		/// </summary>
		[Benchmark ( OperationsPerInvoke = FastRunCount )]
		public void Aelian_RealFFT_Inverse_Split ()
			{
			var SplitRealSpan = _IterationSplitRealData!.AsSpan ();
			var SplitImagSpan = _IterationSplitImaginaryData!.AsSpan ();

			for ( int i = 0; i < FastRunCount; i++ )
				{
				Aelian.FFT.FastFourierTransform.RealFFT
					(
					SplitRealSpan,
					SplitImagSpan,
					false
					);
				}
			}

#if BENCHMARK_OTHERS

		/*--------------------------------------------------------------\
		| NWaves                                                        |
		\* ------------------------------------------------------------*/

		[Benchmark ( OperationsPerInvoke = RunCount )]
		public void NWaves_RealFFT_Split ()
			{
			for ( int i = 0; i < RunCount; i++ )
				_NWavesRealFft64!.DirectNorm ( _RandomData, null, _OutRe, _OutIm );
			}

		[Benchmark ( OperationsPerInvoke = RunCount )]
		public void NWaves_RealFFT_Inverse_Split ()
			{
			for ( int i = 0; i < RunCount; i++ )
				_NWavesRealFft64!.InverseNorm ( _InRe, _InIm, _OutSamples );
			}

		/*--------------------------------------------------------------\
		| Math.NET                                                      |
		\* ------------------------------------------------------------*/

		[Benchmark ( OperationsPerInvoke = MediumRunCount )]
		public void MathNet_RealFFT ()
			{
			var N = _RandomData!.Length;

			for ( int i = 0; i < MediumRunCount; i++ )
				MathNet.Numerics.IntegralTransforms.Fourier.ForwardReal ( _MathNetBuffer, N );
			}

		[Benchmark ( OperationsPerInvoke = MediumRunCount )]
		public void MathNet_RealFFT_Inverse ()
			{
			var N = _RandomData!.Length;

			for ( int i = 0; i < MediumRunCount; i++ )
				MathNet.Numerics.IntegralTransforms.Fourier.InverseReal ( _MathNetBuffer, N );
			}

		/*--------------------------------------------------------------\
		| LomontFFT                                                     |
		\* ------------------------------------------------------------*/

		[Benchmark ( OperationsPerInvoke = RunCount )]
		public void Lomont_RealFFT ()
			{
			for ( int i = 0; i < RunCount; i++ )
				_Lomont.RealFFT ( _IterationDataArray!, true );
			}

		[Benchmark ( OperationsPerInvoke = RunCount )]
		public void Lomont_RealFFT_Inverse ()
			{
			for ( int i = 0; i < RunCount; i++ )
				_Lomont.RealFFT ( _IterationDataArray!, false );
			}

		/*--------------------------------------------------------------\
		| FftFlat                                                      |
		\* ------------------------------------------------------------*/

		[Benchmark ( OperationsPerInvoke = FastRunCount )]
		public void FftFlat_RealFFT ()
			{
			var RealSpan = _IterationDataArrayPlus2!.AsSpan ();

			for ( int i = 0; i < FastRunCount; i++ )
				_FftFlatReal!.Forward ( RealSpan );
			}

		[Benchmark ( OperationsPerInvoke = FastRunCount )]
		public void FftFlat_RealFFT_Inverse ()
			{
			var ComplexSpan = _InComplex.AsSpan ();

			for ( int i = 0; i < FastRunCount; i++ )
				_FftFlatReal!.Inverse ( ComplexSpan );
			}

		/*--------------------------------------------------------------\
		| FftSharp                                                      |
		\* ------------------------------------------------------------*/

		[Benchmark ( OperationsPerInvoke = SlowRunCount )]
		public void FftSharp_RealFFT ()
			{
			for ( int i = 0; i < SlowRunCount; i++ )
				FftSharp.FFT.ForwardReal ( _IterationDataArray );
			}

		[Benchmark ( OperationsPerInvoke = SlowRunCount )]
		public void FftSharp_RealFFT_Inverse ()
			{
			for ( int i = 0; i < SlowRunCount; i++ )
				FftSharp.FFT.InverseReal ( _InComplex );
			}
#endif

		public void Dispose ()
			{
#if USE_ALIGNED
			_IterationData?.Dispose ();
			_IterationSplitRealData?.Dispose ();
			_IterationSplitImaginaryData?.Dispose ();
#endif
			}

		}
	}
