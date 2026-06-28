using System;

namespace Benchmarks
	{
	public class Profiling : IDisposable
		{
		private Aelian.FFT.AlignedSignalData? _IterationData; // Input: Samples, Output: complex spectrum interleaved real/imaginary values
		private Aelian.FFT.AlignedMemory<double>? _IterationSplitRealData; // Input: Even samples, Output: complex spectrum values' real components
		private Aelian.FFT.AlignedMemory<double>? _IterationSplitImaginaryData; // Input: Odd samples, Output: complex spectrum values' imaginary components

		public int N { get; }

		public Profiling ( int n )
			{
			N = n;
			}

		public void Setup ()
			{
			Console.Write ( "Initializing... " );

			Aelian.FFT.FastFourierTransform.Initialize ();

			var Rnd = new Random ( 1337 );

			_IterationData = Aelian.FFT.AlignedSignalData.AllocateFromRealSize ( N );
			_IterationSplitRealData = Aelian.FFT.AlignedMemory<double>.Allocate ( _IterationData.ComplexLength );
			_IterationSplitImaginaryData = Aelian.FFT.AlignedMemory<double>.Allocate ( _IterationData.ComplexLength );

			var IterationDataSpan = _IterationData.AsReal ();

			for ( int i = 0; i < N; i++ )
				IterationDataSpan[i] = Rnd.NextDouble () * 2.0 - 1.0;

			var IterationDataComplexSpan = _IterationData.AsComplex ();
			var IterationSplitRealDataSpan = _IterationSplitRealData.AsSpan ();
			var IterationSplitImaginaryDataSpan = _IterationSplitImaginaryData.AsSpan ();

			for ( int i = 0; i < N / 2; i++ )
				{
				IterationSplitRealDataSpan[i] = IterationDataComplexSpan[i].Real;
				IterationSplitImaginaryDataSpan[i] = IterationDataComplexSpan[i].Imaginary;
				}

			Console.WriteLine ( "Done" );
			}

		public void Run ()
			{
			Console.WriteLine ( "Running..." );

			var RealSpan = _IterationData!.AsReal ();

			while ( true )
				{
				Aelian.FFT.FastFourierTransform.RealFFT ( RealSpan, true );
				Aelian.FFT.FastFourierTransform.RealFFT ( RealSpan, false );
				}
			}

		public void Dispose ()
			{
			_IterationData?.Dispose ();
			_IterationSplitRealData?.Dispose ();
			_IterationSplitImaginaryData?.Dispose ();
			}
		}
	}
