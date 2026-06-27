#define BENCHMARK_OTHERS
#define USE_ALIGNED

using System;
using System.Numerics;

using Aelian.FFT.Extensions;

using BenchmarkDotNet.Attributes;

namespace Benchmarks
	{
	public class BenchmarkComplexFft
		{
		private const int RunCount = 10000;
		private Complex[]? _RandomData; // Randomly generated complex values, this will not be written to after Setup ()

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

		private NWaves.Transforms.Fft64? _NWavesFft64;
		private Lomont.LomontFFT _Lomont = new () { A = 1, B = -1 };

		private double[]? _InRe;
		private double[]? _InIm;
		private double[]? _OutRe;
		private double[]? _OutIm;
		private Complex[]? _ComplexBuffer;
		private double[]? _RealBuffer;
		private NAudio.Dsp.Complex[]? _NAudioBuffer;

		[Params ( 4096 )]
		public int N;

		[GlobalSetup]
		public void Setup ()
			{
			var Rnd = new Random ( 1337 );
			_RandomData = new Complex[N];

#if USE_ALIGNED
			_IterationData = Aelian.FFT.AlignedSignalData.AllocateFromComplexSize ( N );
			_IterationSplitRealData = Aelian.FFT.AlignedMemory<double>.Allocate ( _IterationData.ComplexLength );
			_IterationSplitImaginaryData = Aelian.FFT.AlignedMemory<double>.Allocate ( _IterationData.ComplexLength );
#else
			_IterationData = Aelian.FFT.SignalData.CreateFromRealSize ( N );
			_IterationSplitRealData = new double[_IterationData.ComplexLength];
			_IterationSplitImaginaryData = new double[_IterationData.ComplexLength];
#endif

			var IterationDataSpan = _IterationData.AsComplex ();
			var SplitRealSpan = _IterationSplitRealData.AsSpan ();
			var SplitImaginarySpan = _IterationSplitImaginaryData.AsSpan ();

			for ( int i = 0; i < N; i++ )
				{
				IterationDataSpan[i] = new Complex ( Rnd.NextDouble () * 2.0 - 1.0, Rnd.NextDouble () * 2.0 - 1.0 );
				SplitRealSpan[i] = IterationDataSpan[i].Real;
				SplitImaginarySpan[i] = IterationDataSpan[i].Imaginary;
				}				

			Aelian.FFT.FastFourierTransform.Initialize ();

			// Initialize other implementations

			_NWavesFft64 = new NWaves.Transforms.Fft64 ( N );

			_InRe = new double[_IterationData.ComplexLength];
			_InIm = new double[_IterationData.ComplexLength];
			_OutRe = new double[_IterationData.ComplexLength];
			_OutIm = new double[_IterationData.ComplexLength];
			_ComplexBuffer = new Complex[_IterationData.ComplexLength];
			_RealBuffer = new double[_IterationData.RealLength];
			_NAudioBuffer = new NAudio.Dsp.Complex[_IterationData.ComplexLength];
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
				|| _ComplexBuffer is null
				|| _NAudioBuffer is null
				)
				throw new Exception ( "Not all fields have been initialized" );

#if USE_ALIGNED
			_RandomData.AsSpan ().CopyTo ( _IterationData.AsSpanOf<Complex> () );
#else
			_RandomData.AsSpan ().CopyTo ( _IterationData.AsReal () );
#endif

			var ComplexSpan = _RandomData.AsSpan ();
			var RealSpan = ComplexSpan.Cast<Complex, double> ();
			var SplitRealSpan = _IterationSplitRealData.AsSpan ();
			var SplitImaginarySpan = _IterationSplitImaginaryData.AsSpan ();

			for ( int i = 0; i < N; i++ )
				{
				SplitRealSpan[i] = ComplexSpan[i].Real;
				SplitImaginarySpan[i] = ComplexSpan[i].Imaginary;

				_InRe[i] = SplitRealSpan[i];
				_InIm[i] = SplitImaginarySpan[i];

				_NAudioBuffer[i] = new NAudio.Dsp.Complex () { X = (float) ComplexSpan[i].Real, Y = (float) ComplexSpan[i].Imaginary };
				}

			ComplexSpan.CopyTo ( _ComplexBuffer );
			RealSpan.CopyTo ( _RealBuffer );

			_OutRe.AsSpan ().Clear ();
			_OutIm.AsSpan ().Clear ();
			}

		/*--------------------------------------------------------------\
		| Aelian.FFT                                                    |
		\* ------------------------------------------------------------*/

		[Benchmark ( Baseline = true )]
		public void Aelian_FFT ()
			{
			var ComplexSpan = _IterationData!.AsComplex ();

			for ( int i = 0; i < RunCount; i++ )
				Aelian.FFT.FastFourierTransform.FFT ( ComplexSpan, true );
			}

		[Benchmark]
		public void Aelian_FFT_Split ()
			{
			var SplitRealSpan = _IterationSplitRealData!.AsSpan ();
			var SplitImagSpan = _IterationSplitImaginaryData!.AsSpan ();

			for ( int i = 0; i < RunCount; i++ )
				Aelian.FFT.FastFourierTransform.FFT ( SplitRealSpan, SplitImagSpan, true );
			}

		[Benchmark]
		public void Aelian_FFT_Inverse ()
			{
			var ComplexSpan = _IterationData!.AsComplex ();

			for ( int i = 0; i < RunCount; i++ )
				Aelian.FFT.FastFourierTransform.FFT ( ComplexSpan, false );
			}

		[Benchmark]
		public void Aelian_FFT_Inverse_Split ()
			{
			var SplitRealSpan = _IterationSplitRealData!.AsSpan ();
			var SplitImagSpan = _IterationSplitImaginaryData!.AsSpan ();

			for ( int i = 0; i < RunCount; i++ )
				Aelian.FFT.FastFourierTransform.FFT ( SplitRealSpan, SplitImagSpan, false );
			}

#if BENCHMARK_OTHERS

		/*--------------------------------------------------------------\
		| NWaves                                                        |
		\* ------------------------------------------------------------*/

		[Benchmark]
		public void NWaves_FFT ()
			{
			for ( int i = 0; i < RunCount; i++ )
				_NWavesFft64!.DirectNorm ( _InRe, _InIm, _OutRe, _OutIm );
			}

		[Benchmark]
		public void NWaves_FFT_Inverse ()
			{
			for ( int i = 0; i < RunCount; i++ )
				_NWavesFft64!.InverseNorm ( _InRe, _InIm, _OutRe, _OutIm );
			}

		/*--------------------------------------------------------------\
		| Math.NET                                                      |
		\* ------------------------------------------------------------*/

		[Benchmark]
		public void MathNet_FFT ()
			{
			for ( int i = 0; i < RunCount; i++ )
				MathNet.Numerics.IntegralTransforms.Fourier.Forward ( _ComplexBuffer );
			}

		[Benchmark]
		public void MathNet_FFT_Inverse ()
			{
			for ( int i = 0; i < RunCount; i++ )
				MathNet.Numerics.IntegralTransforms.Fourier.Inverse ( _ComplexBuffer );
			}

		/*--------------------------------------------------------------\
		| LomontFFT                                                     |
		\* ------------------------------------------------------------*/

		[Benchmark]
		public void Lomont_FFT ()
			{
			for ( int i = 0; i < RunCount; i++ )
				_Lomont.FFT ( _RealBuffer!, true );
			}

		[Benchmark]
		public void Lomont_FFT_Inverse ()
			{
			for ( int i = 0; i < RunCount; i++ )
				_Lomont.FFT ( _RealBuffer!, false );
			}

		/*--------------------------------------------------------------\
		| NAudio                                                        |
		| NOTE: NAudio only supports its own Complex value type using   |
		| 32-bit floats, resulting in reduced precision                 |
		\* ------------------------------------------------------------*/

		[Benchmark]
		public void NAudio_FFT_32 ()
			{
			var M = Aelian.FFT.MathUtils.ILog2 ( N );

			for ( int i = 0; i < RunCount; i++ )
				NAudio.Dsp.FastFourierTransform.FFT ( true, M, _NAudioBuffer );
			}

		[Benchmark]
		public void NAudio_FFT_Inverse_32 ()
			{
			var M = Aelian.FFT.MathUtils.ILog2 ( N / 2 );

			for ( int i = 0; i < RunCount; i++ )
				NAudio.Dsp.FastFourierTransform.FFT ( false, M, _NAudioBuffer );
			}

		/*--------------------------------------------------------------\
		| FftSharp                                                      |
		\* ------------------------------------------------------------*/

		[Benchmark]
		public void FftSharp_FFT ()
			{
			for ( int i = 0; i < RunCount; i++ )
				FftSharp.FFT.Forward ( _ComplexBuffer );
			}

		[Benchmark]
		public void FftSharp_FFT_Inverse ()
			{
			for ( int i = 0; i < RunCount; i++ )
				FftSharp.FFT.Inverse ( _ComplexBuffer );
			}
#endif
		}
	}
