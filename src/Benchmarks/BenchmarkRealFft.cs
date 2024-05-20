using System;
using System.Numerics;
using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

namespace Benchmarks
	{
	//[DisassemblyDiagnoser ( syntax: BenchmarkDotNet.Diagnosers.DisassemblySyntax.Masm, printSource: true )]
	public class BenchmarkRealFft
		{
		private const int RunCount = 10000;
		private double[] RealInputData { get; set; }
		
		[Params ( 4096 )]
		public int N;

		[GlobalSetup]
		public void Setup ()
			{
			var Rnd = new Random ();
			RealInputData = new double[N];

			for ( int i = 0; i < N; i++ )
				RealInputData[i] = Rnd.NextDouble () * 2.0 - 1.0;

			Aelian.FFT.FastFourierTransform.Initialize ();
			}

		private unsafe void CopySourceData<T> ( T[] toBuffer )
			where T : unmanaged
			{
			var ByteSize = RealInputData.Length * sizeof ( double );
			var DestByteSize = toBuffer.Length * sizeof ( T );

			fixed ( T* pData = toBuffer )
			fixed ( double* pSource = RealInputData )
				{
				Unsafe.CopyBlock ( pData, pSource, (uint) Math.Min ( ByteSize, DestByteSize ) );
				}
			}

		/*--------------------------------------------------------------\
		| Aelian.FFT                                                    |
		\* ------------------------------------------------------------*/

		[Benchmark ( Baseline = true )]
		public unsafe void Aelian_RealFFT ()
			{
			var Buffer = new double[RealInputData.Length];

			CopySourceData ( Buffer );

			for ( int i = 0; i < RunCount; i++ )
				Aelian.FFT.FastFourierTransform.RealFFT ( Buffer, true );
			}

		[Benchmark]
		public unsafe void Aelian_RealFFT_Inverse ()
			{
			var Buffer = new double[RealInputData.Length];

			CopySourceData ( Buffer );

			for ( int i = 0; i < RunCount; i++ )
				Aelian.FFT.FastFourierTransform.RealFFT ( Buffer, false );
			}

		/*--------------------------------------------------------------\
		| Math.NET                                                      |
		\* ------------------------------------------------------------*/

		[Benchmark]
		public unsafe void MathNet_RealFFT ()
			{
			var N = RealInputData.Length;
			var Buffer = new double[N + 2]; // MathNet.Numerics.IntegralTransforms.Fourier.ForwardReal demands that buffer size be N+2

			CopySourceData ( Buffer );

			for ( int i = 0; i < RunCount; i++ )
				MathNet.Numerics.IntegralTransforms.Fourier.ForwardReal ( Buffer, N );
			}

		[Benchmark]
		public unsafe void MathNet_RealFFT_Inverse ()
			{
			var N = RealInputData.Length;
			var Buffer = new double[N + 2]; // MathNet.Numerics.IntegralTransforms.Fourier.InverseReal demands that buffer size be N+2

			CopySourceData ( Buffer );

			for ( int i = 0; i < RunCount; i++ )
				MathNet.Numerics.IntegralTransforms.Fourier.InverseReal ( Buffer, N );
			}

		/*--------------------------------------------------------------\
		| LomontFFT                                                     |
		\* ------------------------------------------------------------*/

		[Benchmark]
		public unsafe void Lomont_RealFFT ()
			{
			var Lomont = new Lomont.LomontFFT () { A = 1, B = -1 };
			var Buffer = new double[RealInputData.Length];

			CopySourceData ( Buffer );

			for ( int i = 0; i < RunCount; i++ )
				Lomont.RealFFT ( Buffer, true );
			}

		[Benchmark]
		public unsafe void Lomont_RealFFT_Inverse ()
			{
			var Lomont = new Lomont.LomontFFT () { A = 1, B = -1 };
			var Buffer = new double[RealInputData.Length];

			CopySourceData ( Buffer );

			for ( int i = 0; i < RunCount; i++ )
				Lomont.RealFFT ( Buffer, false );
			}

		/*--------------------------------------------------------------\
		| NAudio                                                        |
		| NOTE: NAudio does not have a real-valued FFT, so we can only  |
		| benchmark the complex FFT implementation.                     |
		| Also, it only supports its own Complex value type using       |
		| floats, so it's kind of comparing apples and oranges          |
		\* ------------------------------------------------------------*/

		[Benchmark]
		public unsafe void NAudio_RealFFT ()
			{
			var Buffer = new NAudio.Dsp.Complex[RealInputData.Length / 2];
			var M = Aelian.FFT.MathUtils.ILog2 ( Buffer.Length );

			CopySourceData ( Buffer );

			for ( int i = 0; i < RunCount; i++ )
				NAudio.Dsp.FastFourierTransform.FFT ( true, M, Buffer );
			}

		[Benchmark]
		public unsafe void NAudio_RealFFT_Inverse ()
			{
			var Buffer = new NAudio.Dsp.Complex[RealInputData.Length / 2];
			var M = Aelian.FFT.MathUtils.ILog2 ( Buffer.Length );

			CopySourceData ( Buffer );

			for ( int i = 0; i < RunCount; i++ )
				NAudio.Dsp.FastFourierTransform.FFT ( false, M, Buffer );
			}

		/*--------------------------------------------------------------\
		| FftSharp                                                      |
		\* ------------------------------------------------------------*/

		[Benchmark]
		public unsafe void FftSharp_RealFFT ()
			{
			var Buffer = new double[RealInputData.Length];

			CopySourceData ( Buffer );

			for ( int i = 0; i < RunCount; i++ )
				FftSharp.FFT.ForwardReal ( Buffer );
			}

		[Benchmark]
		public unsafe void FftSharp_RealFFT_Inverse ()
			{
			var Buffer = new Complex[RealInputData.Length / 2 + 1]; // FftSharp.FFT.InverseReal demands spectrum size be a power of 2 + 1

			CopySourceData ( Buffer );

			for ( int i = 0; i < RunCount; i++ )
				FftSharp.FFT.InverseReal ( Buffer );
			}
		}
	}
