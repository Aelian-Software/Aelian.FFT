#define BENCHMARK_OTHERS

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

namespace Benchmarks
	{
	public class BenchmarkComplexFft
		{
		private const int RunCount = 10000;
		private Complex[] ComplexInputData { get; set; }
		
		[Params ( 4096 )]
		public int N;

		[GlobalSetup]
		public void Setup ()
			{
			var Rnd = new Random ();
			ComplexInputData = new Complex[N];

			for ( int i = 0; i < N; i++ )
				ComplexInputData[i] = new Complex ( Rnd.NextDouble () * 2.0 - 1.0, Rnd.NextDouble () * 2.0 - 1.0 );

			Aelian.FFT.FastFourierTransform.Initialize ();
			}

		private unsafe void CopySourceData<T> ( T[] toBuffer )
			where T : unmanaged
			{
			var ByteSize = ComplexInputData.Length * sizeof ( double );
			var DestByteSize = toBuffer.Length * sizeof ( T );

			fixed ( T* pData = toBuffer )
			fixed ( Complex* pSource = ComplexInputData )
				{
				Unsafe.CopyBlock ( pData, pSource, (uint) Math.Min ( ByteSize, DestByteSize ) );
				}
			}

		private void CopySourceData<T> ( NAudio.Dsp.Complex[] toBuffer )
			where T : unmanaged
			{
			var CommonSize = Math.Min ( ComplexInputData.Length, toBuffer.Length );

			for ( int i = 0; i < CommonSize; i++ )
				toBuffer[i] = new NAudio.Dsp.Complex () { X = (float) ComplexInputData[i].Real, Y = (float) ComplexInputData[i].Imaginary };
			}

		/*--------------------------------------------------------------\
		| Aelian.FFT                                                    |
		\* ------------------------------------------------------------*/

		[Benchmark ( Baseline = true )]
		public void Aelian_FFT ()
			{
			var Buffer = new Complex[ComplexInputData.Length];

			CopySourceData ( Buffer );

			for ( int i = 0; i < RunCount; i++ )
				Aelian.FFT.FastFourierTransform.FFT ( Buffer, true );
			}

		[Benchmark]
		public void Aelian_FFT_Inverse ()
			{
			var Buffer = new Complex[ComplexInputData.Length];

			CopySourceData ( Buffer );

			for ( int i = 0; i < RunCount; i++ )
				Aelian.FFT.FastFourierTransform.FFT ( Buffer, false );
			}

#if BENCHMARK_OTHERS

		/*--------------------------------------------------------------\
		| Math.NET                                                      |
		\* ------------------------------------------------------------*/

		[Benchmark]
		public void MathNet_FFT ()
			{
			var Buffer = new Complex[ComplexInputData.Length];

			CopySourceData ( Buffer );

			for ( int i = 0; i < RunCount; i++ )
				MathNet.Numerics.IntegralTransforms.Fourier.Forward ( Buffer );
			}

		[Benchmark]
		public void MathNet_FFT_Inverse ()
			{
			var Buffer = new Complex[ComplexInputData.Length];

			CopySourceData ( Buffer );

			for ( int i = 0; i < RunCount; i++ )
				MathNet.Numerics.IntegralTransforms.Fourier.Inverse ( Buffer );
			}

		/*--------------------------------------------------------------\
		| LomontFFT                                                     |
		\* ------------------------------------------------------------*/

		[Benchmark]
		public void Lomont_FFT ()
			{
			var Lomont = new Lomont.LomontFFT () { A = 1, B = -1 };
			var Buffer = new double[ComplexInputData.Length * 2];

			CopySourceData ( Buffer );

			for ( int i = 0; i < RunCount; i++ )
				Lomont.FFT ( Buffer, true );
			}

		[Benchmark]
		public void Lomont_FFT_Inverse ()
			{
			var Lomont = new Lomont.LomontFFT () { A = 1, B = -1 };
			var Buffer = new double[ComplexInputData.Length * 2];

			CopySourceData ( Buffer );

			for ( int i = 0; i < RunCount; i++ )
				Lomont.FFT ( Buffer, false );
			}

		/*--------------------------------------------------------------\
		| NAudio                                                        |
		| NOTE: NAudio only supports its own Complex value type using   |
		| 32-bit floats, so it's kind of comparing apples and oranges   |
		\* ------------------------------------------------------------*/

		[Benchmark]
		public void NAudio_FFT ()
			{
			var Buffer = new NAudio.Dsp.Complex[ComplexInputData.Length];
			var M = Aelian.FFT.MathUtils.ILog2 ( Buffer.Length );

			CopySourceData ( Buffer );

			for ( int i = 0; i < RunCount; i++ )
				NAudio.Dsp.FastFourierTransform.FFT ( true, M, Buffer );
			}

		[Benchmark]
		public void NAudio_FFT_Inverse ()
			{
			var Buffer = new NAudio.Dsp.Complex[ComplexInputData.Length / 2];
			var M = Aelian.FFT.MathUtils.ILog2 ( Buffer.Length );

			CopySourceData ( Buffer );

			for ( int i = 0; i < RunCount; i++ )
				NAudio.Dsp.FastFourierTransform.FFT ( false, M, Buffer );
			}

		/*--------------------------------------------------------------\
		| FftSharp                                                      |
		\* ------------------------------------------------------------*/

		[Benchmark]
		public void FftSharp_FFT ()
			{
			var Buffer = new Complex[ComplexInputData.Length];

			CopySourceData ( Buffer );

			for ( int i = 0; i < RunCount; i++ )
				FftSharp.FFT.Forward ( Buffer );
			}

		[Benchmark]
		public void FftSharp_FFT_Inverse ()
			{
			var Buffer = new Complex[ComplexInputData.Length];

			CopySourceData ( Buffer );

			for ( int i = 0; i < RunCount; i++ )
				FftSharp.FFT.Inverse ( Buffer );
			}

#endif
		}
	}
