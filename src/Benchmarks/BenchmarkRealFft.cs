#define BENCHMARK_OTHERS

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

namespace Benchmarks
	{
	public class BenchmarkRealFft
		{
		private const int RunCount = 10000;
		private Aelian.FFT.SignalData RealInputData { get; set; }
		
		[Params ( 4096 )]
		public int N;

		[GlobalSetup]
		public void Setup ()
			{
			var Rnd = new Random ();
			RealInputData = Aelian.FFT.SignalData.CreateFromRealSize ( N );
			var Mem = RealInputData.AsReal ();

			for ( int i = 0; i < N; i++ )
				Mem[i] = Rnd.NextDouble () * 2.0 - 1.0;

			Aelian.FFT.FastFourierTransform.Initialize ();
			}

		private unsafe void CopySourceData<T> ( Span<T> destination )
			where T : unmanaged
			{
			var Source = RealInputData.AsReal ();

			var ByteSize = Source.Length * sizeof ( double );
			var DestByteSize = destination.Length * sizeof ( T );

			fixed ( T* pData = destination )
			fixed ( double* pSource = Source )
				{
				Unsafe.CopyBlock ( pData, pSource, (uint) Math.Min ( ByteSize, DestByteSize ) );
				}
			}

		/*--------------------------------------------------------------\
		| Aelian.FFT                                                    |
		\* ------------------------------------------------------------*/

		[Benchmark ( Baseline = true )]
		public void Aelian_RealFFT ()
			{
			var Buffer = RealInputData.Clone ().AsReal ();

			for ( int i = 0; i < RunCount; i++ )
				Aelian.FFT.FastFourierTransform.RealFFT ( Buffer, true );
			}

		/// <summary>
		/// The split data overload of RealFFT is faster than the interleaved one, since it can skip unzipping and rezipping,
		/// but it is also less practical since it splits out the real-valued samples in even and odd arrays
		/// </summary>
		[Benchmark]
		public void Aelian_RealFFT_Split ()
			{
			var BufferRe = new double[RealInputData.ComplexLength]; // Input: Even samples, output: spectrum real values
			var BufferIm = new double[RealInputData.ComplexLength]; // Input: Odd samples, output: spectrum imaginary values

			CopySourceData<double> ( BufferRe );
			CopySourceData<double> ( BufferIm );

			for ( int i = 0; i < RunCount; i++ )
				Aelian.FFT.FastFourierTransform.RealFFT ( BufferRe, BufferIm, true );
			}

		[Benchmark]
		public void Aelian_RealFFT_Inverse ()
			{
			var Buffer = RealInputData.Clone ().AsReal ();

			for ( int i = 0; i < RunCount; i++ )
				Aelian.FFT.FastFourierTransform.RealFFT ( Buffer, false );
			}

		/// <summary>
		/// The split data overload of RealFFT is faster than the interleaved one, since it can skip unzipping and rezipping,
		/// but it is also less practical since it splits out the real-valued samples in even and odd arrays
		/// </summary>
		[Benchmark]
		public void Aelian_RealFFT_Inverse_Split ()
			{
			var BufferRe = new double[RealInputData.ComplexLength]; // Input: spectrum real values, output: Even samples
			var BufferIm = new double[RealInputData.ComplexLength]; // Input: spectrum imaginary values, output: Odd samples

			CopySourceData<double> ( BufferRe );
			CopySourceData<double> ( BufferIm );

			for ( int i = 0; i < RunCount; i++ )
				Aelian.FFT.FastFourierTransform.RealFFT ( BufferRe, BufferIm, false );
			}

#if BENCHMARK_OTHERS

		/*--------------------------------------------------------------\
		| NWaves                                                        |
		\* ------------------------------------------------------------*/

		[Benchmark]
		public void NWaves_RealFFT_Split ()
			{
			var Buffer = new double[RealInputData.RealLength];
			var OutRe = new double[RealInputData.ComplexLength];
			var OutIm = new double[RealInputData.ComplexLength];
			var NWaves = new NWaves.Transforms.RealFft64 ( RealInputData.ComplexLength );

			CopySourceData<double> ( Buffer );

			for ( int i = 0; i < RunCount; i++ )
				NWaves.DirectNorm ( Buffer, null, OutRe, OutIm );
			}

		[Benchmark]
		public void NWaves_RealFFT_Inverse_Split ()
			{
			var BufferRe = new double[RealInputData.ComplexLength];
			var BufferIm = new double[RealInputData.ComplexLength];
			var Out = new double[RealInputData.RealLength];
			var NWaves = new NWaves.Transforms.RealFft64 ( RealInputData.ComplexLength );

			CopySourceData<double> ( BufferRe );
			CopySourceData<double> ( BufferIm );

			for ( int i = 0; i < RunCount; i++ )
				NWaves.InverseNorm ( BufferRe, BufferIm, Out );
			}

		/*--------------------------------------------------------------\
		| Math.NET                                                      |
		\* ------------------------------------------------------------*/

		[Benchmark]
		public void MathNet_RealFFT ()
			{
			var N = RealInputData.RealLength;
			var Buffer = new double[N + 2]; // MathNet.Numerics.IntegralTransforms.Fourier.ForwardReal demands that buffer size be N+2

			CopySourceData<double> ( Buffer );

			for ( int i = 0; i < RunCount; i++ )
				MathNet.Numerics.IntegralTransforms.Fourier.ForwardReal ( Buffer, N );
			}

		[Benchmark]
		public void MathNet_RealFFT_Inverse ()
			{
			var N = RealInputData.RealLength;
			var Buffer = new double[N + 2]; // MathNet.Numerics.IntegralTransforms.Fourier.InverseReal demands that buffer size be N+2

			CopySourceData<double> ( Buffer );

			for ( int i = 0; i < RunCount; i++ )
				MathNet.Numerics.IntegralTransforms.Fourier.InverseReal ( Buffer, N );
			}

		/*--------------------------------------------------------------\
		| LomontFFT                                                     |
		\* ------------------------------------------------------------*/

		[Benchmark]
		public void Lomont_RealFFT ()
			{
			var Lomont = new Lomont.LomontFFT () { A = 1, B = -1 };
			var Buffer = RealInputData.ToRealArray ();

			for ( int i = 0; i < RunCount; i++ )
				Lomont.RealFFT ( Buffer, true );
			}

		[Benchmark]
		public void Lomont_RealFFT_Inverse ()
			{
			var Lomont = new Lomont.LomontFFT () { A = 1, B = -1 };
			var Buffer = RealInputData.ToRealArray ();

			for ( int i = 0; i < RunCount; i++ )
				Lomont.RealFFT ( Buffer, false );
			}

		/*--------------------------------------------------------------\
		| FftSharp                                                      |
		\* ------------------------------------------------------------*/

		[Benchmark]
		public void FftSharp_RealFFT ()
			{
			var Buffer = RealInputData.ToRealArray ();

			for ( int i = 0; i < RunCount; i++ )
				FftSharp.FFT.ForwardReal ( Buffer );
			}

		[Benchmark]
		public void FftSharp_RealFFT_Inverse ()
			{
			var Buffer = new Complex[RealInputData.ComplexLength + 1]; // FftSharp.FFT.InverseReal demands spectrum size be a power of 2 + 1

			CopySourceData<Complex> ( Buffer );

			for ( int i = 0; i < RunCount; i++ )
				FftSharp.FFT.InverseReal ( Buffer );
			}

#endif
		}
	}
