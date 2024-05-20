using System;
using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

using Lomont;

namespace Benchmarks
	{
	[DisassemblyDiagnoser ( syntax: BenchmarkDotNet.Diagnosers.DisassemblySyntax.Masm, printSource: true )]
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

		[Benchmark ( Baseline = true )]
		public unsafe void Lomont_RealFFT ()
			{
			var Lomont = new LomontFFT () { A = 1, B = -1 };
			var Source = RealInputData;
			var BufferLen = Source.Length;
			var Buffer = new double[BufferLen];

			fixed ( double* pData = Buffer )
				{
				Unsafe.Copy ( pData, ref Source );

				for ( int i = 0; i < RunCount; i++ )
					{
					Lomont.RealFFT ( Buffer, true );
					}
				}
			}

		[Benchmark]
		public unsafe void Lomont_RealFFT_Inverse ()
			{
			var Lomont = new LomontFFT () { A = 1, B = -1 };
			var Source = RealInputData;
			var BufferLen = Source.Length;
			var Buffer = new double[BufferLen];

			fixed ( double* pData = Buffer )
				{
				Unsafe.Copy ( pData, ref Source );

				for ( int i = 0; i < RunCount; i++ )
					{
					Lomont.RealFFT ( Buffer, false );
					}
				}
			}

		[Benchmark]
		public unsafe void AelianRealFFT ()
			{
			var Source = RealInputData;
			var BufferLen = Source.Length;
			var Buffer = new double[BufferLen];

			fixed ( double* pData = Buffer )
				{
				Unsafe.Copy ( pData, ref Source );

				for ( int i = 0; i < RunCount; i++ )
					{
					Aelian.FFT.FastFourierTransform.RealFFT ( Buffer, true );
					}
				}
			}

		[Benchmark]
		public unsafe void AelianRealFFT_Inverse ()
			{
			var Source = RealInputData;
			var BufferLen = Source.Length;
			var Buffer = new double[BufferLen];

			fixed ( double* pData = Buffer )
				{
				Unsafe.Copy ( pData, ref Source );

				for ( int i = 0; i < RunCount; i++ )
					{
					Aelian.FFT.FastFourierTransform.RealFFT ( Buffer, false );
					}
				}
			}
		}
	}
