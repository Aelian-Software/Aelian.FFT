using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Aelian.FFT
	{
	public class FastFourierTransform
		{
		private static double[][] _RealRootsOfUnity;
		private static double[][] _RealInverseRootsOfUnity;
		private static double[][] _ImagRootsOfUnity;
		private static double[][] _ImagInverseRootsOfUnity;

		public static void Initialize () 
			{
			// Precalculate tables

			CalculateRootsOfUnity ();
			IndexReversal.CalculateBitReverseIndices ();
			ArrayZip.CalculateUnZipCycleDecompositions ();
			}

		private static void CalculateRootsOfUnity ()
			{
			_RealRootsOfUnity = new double[Constants.MaxTableDepth][];
			_ImagRootsOfUnity = new double[Constants.MaxTableDepth][];
			_RealInverseRootsOfUnity = new double[Constants.MaxTableDepth][];
			_ImagInverseRootsOfUnity = new double[Constants.MaxTableDepth][];

			for ( int i = 0; i < Constants.MaxTableDepth; i++ )
				{
				var N = 1 << i;
				_RealRootsOfUnity[i] = new double[N];
				_ImagRootsOfUnity[i] = new double[N];
				_RealInverseRootsOfUnity[i] = new double[N];
				_ImagInverseRootsOfUnity[i] = new double[N];

				// Calculate Nth roots of unity:

				for ( int k = 0; k < N; k++ )
					{
					var NegTwoKPiOverN = ( -2.0 * k * Math.PI ) / N; // Note the -2.0 instead of 2.0, 

					var RootsOfUnity = new Complex
						(
						Math.Cos ( NegTwoKPiOverN ),
						Math.Sin ( NegTwoKPiOverN )
						);

					// This is the same as _RootsOfUnity[i][k] with the imaginary part negated:
					var InverseRootsOfUnity = 1.0 / RootsOfUnity;

					// Split into real/imaginary arrays
					_RealRootsOfUnity[i][k] = RootsOfUnity.Real;
					_ImagRootsOfUnity[i][k] = RootsOfUnity.Imaginary;
					_RealInverseRootsOfUnity[i][k] = InverseRootsOfUnity.Real;
					_ImagInverseRootsOfUnity[i][k] = InverseRootsOfUnity.Imaginary;
					}
				}
			}

		static readonly Vector256<long> _VecReverse = Vector256.Create ( new long[] { 3, 2, 1, 0 } );

		[MethodImpl ( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
		private static Vector256<double> Reverse ( Vector256<double> vec )
			{
			//return Vector256.Shuffle ( vec, _VecReverse ); // Slow in .NET 7. Should perform better in .NET 8?
			return Vector256.Create ( vec[3], vec[2], vec[1], vec[0] );
			}

		//========================================================================
		// VectorizedInPlaceIterativeFFT
		// Vectorized iterative Radix-2 Cooley-Tukey FFT
		//========================================================================

		[Flags]
		public enum FftFlags
			{
			None = 0,
			DoNotRezip = 1,
			DoNotNormalize = 2
			}

		public static void FFT ( Span<Complex> buffer, bool forward, FftFlags flags = FftFlags.None )
			{
			var n = buffer.Length;

			if ( !BitOperations.IsPow2 ( n ) )
				throw new ArgumentException ( "Buffer size must be a power of 2", nameof ( buffer ) );

			var UnZippedBuffer = MemoryMarshal.Cast<Complex, double> ( buffer );
			var RealValues = UnZippedBuffer.Slice ( 0, n );
			var ImagValues = UnZippedBuffer.Slice ( n, n );

			ArrayZip.UnZipInPlacePow2 ( UnZippedBuffer ); // Unzip

			FFT ( RealValues, ImagValues, forward );

			if ( !flags.HasFlag ( FftFlags.DoNotRezip ) )
				ArrayZip.ZipInPlacePow2 ( UnZippedBuffer ); // Re-zip
			}

		public static void FFT ( Span<double> complexRealValues, Span<double> complexImagValues, bool forward, double normalizeFactor = 1.0 )
			{
			var n = complexRealValues.Length;

			const int VectorSizeShift = 2;
			const int VectorSize = 1 << VectorSizeShift;
			Debug.Assert ( Vector<double>.Count == VectorSize );

			if ( n != complexImagValues.Length )
				throw new ArgumentException ( "Imaginary and real buffer sizes must be the same", nameof ( complexImagValues ) );

			if ( !BitOperations.IsPow2 ( n ) )
				throw new ArgumentException ( "Buffer size must be a power of 2", nameof ( complexRealValues ) );

			var LogN = MathUtils.ILog2 ( n );

			Debug.Assert ( LogN + 1 < Constants.MaxTableDepth, $"Constants.MaxTableDepth is too low to process DFT of size {n}. Should be at least {LogN + 2}" );

			var RealRootsOfUnity = forward ? _RealRootsOfUnity : _RealInverseRootsOfUnity;
			var ImagRootsOfUnity = forward ? _ImagRootsOfUnity : _ImagInverseRootsOfUnity;

			IndexReversal.BitReverseArrayInPlace ( complexRealValues, complexImagValues, LogN );

			var VecRealValues = MemoryMarshal.Cast<double, Vector<double>> ( complexRealValues );
			var VecImagValues = MemoryMarshal.Cast<double, Vector<double>> ( complexImagValues );

			// Do the iterations that are too short to vectorize the non-SIMD way

			double EvenReal, EvenImag, OddReal, OddImag, WmReal, WmImag, TReal, TImag;
			int IndexEven, IndexOdd;

			for ( int s = 1; s <= LogN && s < 3; s++ )
				{
				var IterRealRootsOfUnity = RealRootsOfUnity[s].AsSpan ();
				var IterImagRootsOfUnity = ImagRootsOfUnity[s].AsSpan ();

				var m = 1 << s;
				var HalfM = m >> 1;

				for ( int k = 0; k < n; k += m )
					{
					IndexEven = k;
					IndexOdd = IndexEven + HalfM;

					// Process j == 0 differently, since at that index, WmReal is always 1 and WmImag is always 0

					EvenReal = complexRealValues[IndexEven];
					EvenImag = complexImagValues[IndexEven];
					OddReal = complexRealValues[IndexOdd];
					OddImag = complexImagValues[IndexOdd];

					complexRealValues[IndexEven] = EvenReal + OddReal;
					complexImagValues[IndexEven] = EvenImag + OddImag;
					complexRealValues[IndexOdd] = EvenReal - OddReal;
					complexImagValues[IndexOdd] = EvenImag - OddImag;

					IndexEven++;
					IndexOdd++;

					// After that, continue as normal

					for ( int j = 1; j < HalfM; j++, IndexEven++, IndexOdd++ )
						{
						EvenReal = complexRealValues[IndexEven];
						EvenImag = complexImagValues[IndexEven];
						OddReal = complexRealValues[IndexOdd];
						OddImag = complexImagValues[IndexOdd];
						WmReal = IterRealRootsOfUnity[j];
						WmImag = IterImagRootsOfUnity[j];

						TReal = WmReal * OddReal - WmImag * OddImag;
						TImag = WmImag * OddReal + WmReal * OddImag;

						complexRealValues[IndexEven] = EvenReal + TReal;
						complexImagValues[IndexEven] = EvenImag + TImag;
						complexRealValues[IndexOdd] = EvenReal - TReal;
						complexImagValues[IndexOdd] = EvenImag - TImag;
						}

					}
				}

			// Vectorized

			int VecIndexEven, VecIndexOdd;
			Vector<double> VecEvenReal, VecEvenImag, VecOddReal, VecOddImag, VecWmReal, VecWmImag, VecTReal, VecTImag;

			for ( int s = 3; s <= LogN; s++ )
				{
				var IterRealRootsOfUnity = RealRootsOfUnity[s].AsSpan ();
				var IterImagRootsOfUnity = ImagRootsOfUnity[s].AsSpan ();
				var VecRealRootsOfUnity = MemoryMarshal.Cast<double, Vector<double>> ( IterRealRootsOfUnity );
				var VecImagRootsOfUnity = MemoryMarshal.Cast<double, Vector<double>> ( IterImagRootsOfUnity );

				var m = 1 << s;
				var HalfM = m >> 1;
				var VectorizedOpCount = HalfM >> VectorSizeShift;

				for ( int k = 0; k < n; k += m )
					{
					VecIndexEven = k >> VectorSizeShift;
					VecIndexOdd = VecIndexEven + ( HalfM >> VectorSizeShift );

					for ( int i = 0; i < VectorizedOpCount; i++, VecIndexEven++, VecIndexOdd++ )
						{
						VecEvenReal = VecRealValues[VecIndexEven];
						VecEvenImag = VecImagValues[VecIndexEven];
						VecOddReal = VecRealValues[VecIndexOdd];
						VecOddImag = VecImagValues[VecIndexOdd];
						VecWmReal = VecRealRootsOfUnity[i];
						VecWmImag = VecImagRootsOfUnity[i];

						VecTReal = VecWmReal * VecOddReal - VecWmImag * VecOddImag;
						VecTImag = VecWmImag * VecOddReal + VecWmReal * VecOddImag;

						VecRealValues[VecIndexEven] = VecEvenReal + VecTReal;
						VecImagValues[VecIndexEven] = VecEvenImag + VecTImag;
						VecRealValues[VecIndexOdd] = VecEvenReal - VecTReal;
						VecImagValues[VecIndexOdd] = VecEvenImag - VecTImag;
						}
					}
				}

			// Scale the result when doing a reverse transform

			if ( !forward )
				{
				// TODO: handle cases where n < 4
				var VectorizedOpCount = n >> VectorSizeShift;
				var Divisor = new Vector<double> ( ( 1.0 / n ) * normalizeFactor );

				Debug.Assert ( n % VectorSize == 0 );

				for ( int i = 0; i < VectorizedOpCount; i++ )
					{
					VecRealValues[i] *= Divisor;
					VecImagValues[i] *= Divisor;
					}
				}
			}

		//========================================================================
		// RealFFT
		// Vectorized iterative Radix-2 Cooley-Tukey FFT for real valued data
		//========================================================================

		public static void RealFFT ( Span<double> buffer, bool forward, FftFlags flags = FftFlags.None )
			{
			var ComplexBuffer = MemoryMarshal.Cast<double, Complex> ( buffer );
			var N = ComplexBuffer.Length;
			var LogN = MathUtils.ILog2 ( N );
			var HalfN = N >> 1;
			var Sign = forward ? 1.0 : -1.0;
			const double Half = 0.5;

			Debug.Assert ( LogN + 1 < Constants.MaxTableDepth, $"Constants.MaxTableDepth is too small to process DFT of size {N}. Should be at least {LogN + 2}" );

			if ( N < 4 )
				throw new NotSupportedException ( "buffer length must be at least 8" );

			var RealRootsOfUnity = forward ? _RealRootsOfUnity : _RealInverseRootsOfUnity;
			var ImagRootsOfUnity = forward ? _ImagRootsOfUnity : _ImagInverseRootsOfUnity;
			var IterRealRootsOfUnity = RealRootsOfUnity[LogN + 1].AsSpan ();
			var IterImagRootsOfUnity = ImagRootsOfUnity[LogN + 1].AsSpan ();

			ArrayZip.UnZipInPlacePow2 ( buffer ); // Unzip

			var RealValues = buffer.Slice ( 0, N );
			var ImagValues = buffer.Slice ( N, N );

			if ( forward )
				FFT ( RealValues, ImagValues, true );

			// Split the mixed spectra

			const int VectorSizeShift = 2;
			const int VectorSize = 1 << VectorSizeShift;
			
			Debug.Assert ( Vector256<double>.Count == VectorSize );

			var VecRealValues = MemoryMarshal.Cast<double, Vector256<double>> ( RealValues );
			var VecImagValues = MemoryMarshal.Cast<double, Vector256<double>> ( ImagValues );
			var VecRealRootsOfUnity = MemoryMarshal.Cast<double, Vector256<double>> ( IterRealRootsOfUnity );
			var VecImagRootsOfUnity = MemoryMarshal.Cast<double, Vector256<double>> ( IterImagRootsOfUnity );
			var ShiftVecRealValues = MemoryMarshal.Cast<double, Vector256<double>> ( RealValues.Slice ( HalfN + 1, HalfN - 4 ) );
			var ShiftVecImagValues = MemoryMarshal.Cast<double, Vector256<double>> ( ImagValues.Slice ( HalfN + 1, HalfN - 4 ) );

			var VectorizedOpCount = HalfN >> VectorSizeShift;
			var VecHalf = Vector256.Create ( Half );
			var VecSign = Vector256.Create ( Sign );

			Debug.Assert ( VectorizedOpCount > 0 );

			var OppositeK = VectorizedOpCount - 2;

			// Handle k = 0 ( complex index 0 ... 3 )

				{
				var KReal = VecRealValues[0];
				var KImag = VecImagValues[0];
				var OppositeKReal = Vector256.Create ( 0, RealValues[N - 1], RealValues[N - 2], RealValues[N - 3] );
				var OppositeKImag = Vector256.Create ( 0, ImagValues[N - 1], ImagValues[N - 2], ImagValues[N - 3] );

				var RealRootOfUnity = VecRealRootsOfUnity[0];
				var ImagRootOfUnity = VecImagRootsOfUnity[0];

				var a = ( KReal - OppositeKReal ) * ImagRootOfUnity;
				var b = ( KImag + OppositeKImag ) * RealRootOfUnity;
				var c = ( KReal - OppositeKReal ) * RealRootOfUnity;
				var d = ( KImag + OppositeKImag ) * ImagRootOfUnity;
				var e = KReal + OppositeKReal;
				var f = KImag - OppositeKImag;

				var VecReal = VecHalf * ( e + VecSign * ( a + b ) );
				var VecImag = VecHalf * ( f + VecSign * ( d - c ) );

				// Leave [0] alone
				RealValues[1] = VecReal[1];
				RealValues[2] = VecReal[2];
				RealValues[3] = VecReal[3];
				ImagValues[1] = VecImag[1];
				ImagValues[2] = VecImag[2];
				ImagValues[3] = VecImag[3];

				var VecOppositeReal = VecHalf * ( e - VecSign * ( b + a ) );
				var VecOppositeImag = VecHalf * ( VecSign * ( d - c ) - f );

				// Leave [N] alone
				RealValues[N - 1] = VecOppositeReal[1];
				RealValues[N - 2] = VecOppositeReal[2];
				RealValues[N - 3] = VecOppositeReal[3];
				ImagValues[N - 1] = VecOppositeImag[1];
				ImagValues[N - 2] = VecOppositeImag[2];
				ImagValues[N - 3] = VecOppositeImag[3];
				}

			// Handle k > 0 ( complex index > 3 )

				{
				Vector256<double> KReal, KImag, OppositeKReal, OppositeKImag, RealRootOfUnity, ImagRootOfUnity, a, b, c, d, e, f;

				for ( var k = 1; k < VectorizedOpCount; k++, OppositeK-- )
					{
					KReal = VecRealValues[k];
					KImag = VecImagValues[k];
					OppositeKReal = Reverse ( ShiftVecRealValues[OppositeK] );
					OppositeKImag = Reverse ( ShiftVecImagValues[OppositeK] );

					RealRootOfUnity = VecRealRootsOfUnity[k];
					ImagRootOfUnity = VecImagRootsOfUnity[k];

					a = ( KReal - OppositeKReal ) * ImagRootOfUnity;
					b = ( KImag + OppositeKImag ) * RealRootOfUnity;
					c = ( KReal - OppositeKReal ) * RealRootOfUnity;
					d = ( KImag + OppositeKImag ) * ImagRootOfUnity;
					e = KReal + OppositeKReal;
					f = KImag - OppositeKImag;

					VecRealValues[k] = VecHalf * ( e + VecSign * ( a + b ) );
					VecImagValues[k] = VecHalf * ( f + VecSign * ( d - c ) );

					ShiftVecRealValues[OppositeK] = Reverse ( VecHalf * ( e - VecSign * ( b + a ) ) );
					ShiftVecImagValues[OppositeK] = Reverse ( VecHalf * ( VecSign * ( d - c ) - f ) );
					}
				}

			// Handle complex index = HalfN

			ImagValues[HalfN] = -ImagValues[HalfN];

			// Handle DC/nyquist values

			if ( forward )
				{
				var Real0 = RealValues[0];
				RealValues[0] = RealValues[0] + ImagValues[0]; // DC value
				ImagValues[0] = Real0 - ImagValues[0]; // Nyquist
				}
			else
				{
				var Real0 = RealValues[0];
				RealValues[0] = Half * ( Real0 + ImagValues[0] );
				ImagValues[0] = Half * ( Real0 - ImagValues[0] );

				var NormalizeFactor = flags.HasFlag ( FftFlags.DoNotNormalize ) ? ( N * 2.0 ) : 1.0; // TODO: This is wonky, we should be able to skip normalization alltogether. More research needed.

				FFT ( RealValues, ImagValues, false, NormalizeFactor );
				}

			if ( !flags.HasFlag ( FftFlags.DoNotRezip ) )
				ArrayZip.ZipInPlacePow2 ( buffer ); // Re-zip
			}
		}
	}
