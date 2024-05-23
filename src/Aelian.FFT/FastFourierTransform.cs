//
// Aelian.FFT
//
// A highly optimized fast fourier transform implementation for .NET
//
// https://github.com/Aelian-Software/Aelian.FFT
//
// MIT License
//   
// Copyright (c) 2024 Bas Tossings / Aelian
//   
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//   
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//   
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Aelian.FFT
	{
	public static class FastFourierTransform
		{
		private static double[][] _RealRootsOfUnity;
		private static double[][] _ImagRootsOfUnity;
		private static double[][] _ImagInverseRootsOfUnity; 
		// _RealInverseRootsOfUnity is identical to _RealRootsOfUnity, so no need to store it separately

		private const int _Vector128SizeShift = 1;
		private const int _Vector256SizeShift = 2;
		private const int _Vector512SizeShift = 3;

		private static int _Vector512Count = Vector512<double>.Count;
		private static int _Vector512MaxIndex = Vector512<double>.Count - 1;


		/// <summary>
		/// Call this function once when your application is loading
		/// </summary>
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
			_ImagInverseRootsOfUnity = new double[Constants.MaxTableDepth][];

			for ( int i = 0; i < Constants.MaxTableDepth; i++ )
				{
				var N = 1 << i;
				_RealRootsOfUnity[i] = new double[N];
				_ImagRootsOfUnity[i] = new double[N];
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

					// This is the same as RootsOfUnity with the imaginary part negated:
					// var InverseRootsOfUnity = 1.0 / RootsOfUnity;

					// Split into real/imaginary arrays
					_RealRootsOfUnity[i][k] = RootsOfUnity.Real;
					_ImagRootsOfUnity[i][k] = RootsOfUnity.Imaginary;
					_ImagInverseRootsOfUnity[i][k] = -RootsOfUnity.Imaginary;
					}
				}
			}

		static readonly Vector256<long> _VecReverse256 = Vector256.Create ( new long[] { 3, 2, 1, 0 } );

		[MethodImpl ( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
		private static Vector256<double> Reverse ( Vector256<double> vec )
			{
			return Vector256.Shuffle ( vec, _VecReverse256 );
			}

		static readonly Vector512<long> _VecReverse512 = Vector512.Create ( new long[] { 7, 6, 5, 4, 3, 2, 1, 0 } );

		[MethodImpl ( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
		private static Vector512<double> Reverse ( Vector512<double> vec )
			{
			return Vector512.Shuffle ( vec, _VecReverse512 );
			}

		/// <summary>
		/// Compute the forward or inverse Fourier Transform of complex-valued data.
		/// The data is modified in place.
		/// </summary>
		/// <param name="buffer">The complex-valued source data. Will be overwritten by the output data. Length must be a power of 2.</param>
		/// <param name="forward">Specifies whether to perform a forward or inverse transform.</param>
		/// <param name="flags">Specifies how to process the output data, default is None.</param>
		/// <exception cref="ArgumentException">Buffer length is not a power of 2</exception>
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

		/// <summary>
		/// Compute the forward or inverse Fourier Transform of complex-valued data.
		/// The data is modified in place.
		/// </summary>
		/// <param name="complexRealValues">The real value parts of the complex numbers. Length must be a power of 2 and equal to the length of complexImagValues.</param>
		/// <param name="complexImagValues">The imaginary value parts of the complex numbers. Length must be a power of 2 and equal to the length of complexRealValues.</param>
		/// <param name="forward">Specifies whether to perform a forward or inverse transform.</param>
		/// <param name="normalizeFactor">Normalization factor, should be left at the default of 1.0 in most cases.</param>
		/// <exception cref="ArgumentException">Buffer length is not a power of 2 or buffer lenghts do not match.</exception>
		public static void FFT ( Span<double> complexRealValues, Span<double> complexImagValues, bool forward, double normalizeFactor = 1.0 )
			{
			var n = complexRealValues.Length;

			if ( n != complexImagValues.Length )
				throw new ArgumentException ( "Imaginary and real buffer sizes must be the same", nameof ( complexImagValues ) );

			if ( !BitOperations.IsPow2 ( n ) )
				throw new ArgumentException ( "Buffer size must be a power of 2", nameof ( complexRealValues ) );

			var LogN = MathUtils.ILog2 ( n );

			if ( LogN + 1 >= Constants.MaxTableDepth )
				throw new ArgumentException ( $"Constants.MaxTableDepth is too low to process DFT of size {n}. Should be at least {LogN + 2}" );

			var RealRootsOfUnity = _RealRootsOfUnity;
			var ImagRootsOfUnity = forward ? _ImagRootsOfUnity : _ImagInverseRootsOfUnity;

			IndexReversal.BitReverseArrayInPlace ( complexRealValues, complexImagValues, LogN );

			// Do the iterations that are too short to vectorize the non-SIMD way

			double EvenReal, EvenImag, OddReal, OddImag, WmReal, WmImag, TReal, TImag;
			int IndexEven, IndexOdd;

			for ( int s = 1; s <= LogN && s < ( _Vector128SizeShift + 1 ); s++ )
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

			// 128

				{
				var Vec128RealValues = MemoryMarshal.Cast<double, Vector128<double>> ( complexRealValues );
				var Vec128ImagValues = MemoryMarshal.Cast<double, Vector128<double>> ( complexImagValues );

				int VecIndexEven, VecIndexOdd;
				Vector128<double> VecEvenReal, VecEvenImag, VecOddReal, VecOddImag, VecWmReal, VecWmImag, VecTReal, VecTImag;

				const int s = _Vector128SizeShift + 1;
				var IterRealRootsOfUnity = RealRootsOfUnity[s].AsSpan ();
				var IterImagRootsOfUnity = ImagRootsOfUnity[s].AsSpan ();
				var VecRealRootsOfUnity = MemoryMarshal.Cast<double, Vector128<double>> ( IterRealRootsOfUnity );
				var VecImagRootsOfUnity = MemoryMarshal.Cast<double, Vector128<double>> ( IterImagRootsOfUnity );

				var m = 1 << s;
				var HalfM = m >> 1;
				var VectorizedOpCount = HalfM >> _Vector128SizeShift;

				for ( int k = 0; k < n; k += m )
					{
					VecIndexEven = k >> _Vector128SizeShift;
					VecIndexOdd = VecIndexEven + ( HalfM >> _Vector128SizeShift );

					for ( int i = 0; i < VectorizedOpCount; i++, VecIndexEven++, VecIndexOdd++ )
						{
						VecEvenReal = Vec128RealValues[VecIndexEven];
						VecEvenImag = Vec128ImagValues[VecIndexEven];
						VecOddReal = Vec128RealValues[VecIndexOdd];
						VecOddImag = Vec128ImagValues[VecIndexOdd];
						VecWmReal = VecRealRootsOfUnity[i];
						VecWmImag = VecImagRootsOfUnity[i];

						VecTReal = VecWmReal * VecOddReal - VecWmImag * VecOddImag;
						VecTImag = VecWmImag * VecOddReal + VecWmReal * VecOddImag;

						Vec128RealValues[VecIndexEven] = VecEvenReal + VecTReal;
						Vec128ImagValues[VecIndexEven] = VecEvenImag + VecTImag;
						Vec128RealValues[VecIndexOdd] = VecEvenReal - VecTReal;
						Vec128ImagValues[VecIndexOdd] = VecEvenImag - VecTImag;
						}
					}
				}

			// 256

				{
				var Vec256RealValues = MemoryMarshal.Cast<double, Vector256<double>> ( complexRealValues );
				var Vec256ImagValues = MemoryMarshal.Cast<double, Vector256<double>> ( complexImagValues );

				int VecIndexEven, VecIndexOdd;
				Vector256<double> VecEvenReal, VecEvenImag, VecOddReal, VecOddImag, VecWmReal, VecWmImag, VecTReal, VecTImag;

				const int s = _Vector256SizeShift + 1;
				var IterRealRootsOfUnity = RealRootsOfUnity[s].AsSpan ();
				var IterImagRootsOfUnity = ImagRootsOfUnity[s].AsSpan ();
				var VecRealRootsOfUnity = MemoryMarshal.Cast<double, Vector256<double>> ( IterRealRootsOfUnity );
				var VecImagRootsOfUnity = MemoryMarshal.Cast<double, Vector256<double>> ( IterImagRootsOfUnity );

				var m = 1 << s;
				var HalfM = m >> 1;
				var VectorizedOpCount = HalfM >> _Vector256SizeShift;

				for ( int k = 0; k < n; k += m )
					{
					VecIndexEven = k >> _Vector256SizeShift;
					VecIndexOdd = VecIndexEven + ( HalfM >> _Vector256SizeShift );

					for ( int i = 0; i < VectorizedOpCount; i++, VecIndexEven++, VecIndexOdd++ )
						{
						VecEvenReal = Vec256RealValues[VecIndexEven];
						VecEvenImag = Vec256ImagValues[VecIndexEven];
						VecOddReal = Vec256RealValues[VecIndexOdd];
						VecOddImag = Vec256ImagValues[VecIndexOdd];
						VecWmReal = VecRealRootsOfUnity[i];
						VecWmImag = VecImagRootsOfUnity[i];

						VecTReal = VecWmReal * VecOddReal - VecWmImag * VecOddImag;
						VecTImag = VecWmImag * VecOddReal + VecWmReal * VecOddImag;

						Vec256RealValues[VecIndexEven] = VecEvenReal + VecTReal;
						Vec256ImagValues[VecIndexEven] = VecEvenImag + VecTImag;
						Vec256RealValues[VecIndexOdd] = VecEvenReal - VecTReal;
						Vec256ImagValues[VecIndexOdd] = VecEvenImag - VecTImag;
						}
					}
				}

			// 512

			var Vec512RealValues = MemoryMarshal.Cast<double, Vector512<double>> ( complexRealValues );
			var Vec512ImagValues = MemoryMarshal.Cast<double, Vector512<double>> ( complexImagValues );

				{
				int VecIndexEven, VecIndexOdd;
				Vector512<double> VecEvenReal, VecEvenImag, VecOddReal, VecOddImag, VecWmReal, VecWmImag, VecTReal, VecTImag;

				for ( int s = ( _Vector512SizeShift + 1 ); s <= LogN; s++ )
					{
					var IterRealRootsOfUnity = RealRootsOfUnity[s].AsSpan ();
					var IterImagRootsOfUnity = ImagRootsOfUnity[s].AsSpan ();
					var VecRealRootsOfUnity = MemoryMarshal.Cast<double, Vector512<double>> ( IterRealRootsOfUnity );
					var VecImagRootsOfUnity = MemoryMarshal.Cast<double, Vector512<double>> ( IterImagRootsOfUnity );

					var m = 1 << s;
					var HalfM = m >> 1;
					var VectorizedOpCount = HalfM >> _Vector512SizeShift;

					for ( int k = 0; k < n; k += m )
						{
						VecIndexEven = k >> _Vector512SizeShift;
						VecIndexOdd = VecIndexEven + ( HalfM >> _Vector512SizeShift );

						for ( int i = 0; i < VectorizedOpCount; i++, VecIndexEven++, VecIndexOdd++ )
							{
							VecEvenReal = Vec512RealValues[VecIndexEven];
							VecEvenImag = Vec512ImagValues[VecIndexEven];
							VecOddReal = Vec512RealValues[VecIndexOdd];
							VecOddImag = Vec512ImagValues[VecIndexOdd];
							VecWmReal = VecRealRootsOfUnity[i];
							VecWmImag = VecImagRootsOfUnity[i];

							VecTReal = VecWmReal * VecOddReal - VecWmImag * VecOddImag;
							VecTImag = VecWmImag * VecOddReal + VecWmReal * VecOddImag;

							Vec512RealValues[VecIndexEven] = VecEvenReal + VecTReal;
							Vec512ImagValues[VecIndexEven] = VecEvenImag + VecTImag;
							Vec512RealValues[VecIndexOdd] = VecEvenReal - VecTReal;
							Vec512ImagValues[VecIndexOdd] = VecEvenImag - VecTImag;
							}
						}
					}
				}

			// Scale the result when doing a reverse transform

			const int VectorSize = 1 << _Vector512SizeShift;

			if ( !forward )
				{
				// TODO: handle cases where n < 8
				var VectorizedOpCount = n >> _Vector512SizeShift;
				var Divisor = Vector512.Create<double> ( ( 1.0 / n ) * normalizeFactor );

				Debug.Assert ( n % VectorSize == 0 );

				for ( int i = 0; i < VectorizedOpCount; i++ )
					{
					Vec512RealValues[i] *= Divisor;
					Vec512ImagValues[i] *= Divisor;
					}
				}
			}

		//=================================================================================
		// RealFFT
		// Vectorized in-place iterative Radix-2 Cooley-Tukey FFT for real valued data
		//=================================================================================

		/// <summary>
		/// Compute the forward or inverse Fourier Transform of real-valued data.
		/// The data is modified in place.
		/// 
		/// Note that a forward transform takes real-valued data (where 2 subsequent real values are described by a complex value's real and imaginary parts respectively) as input and outputs complex valued data,
		/// whereas an inverse transform takes complex valued data as input and outputs real-valued data (where 2 subsequent real values are described by a complex value's real and imaginary parts respectively).
		/// 
		/// In case of a forward transform, the real and imaginary output values at index 0 will be the DC and Nyquist components respectively.
		/// 
		/// Note that a forward transform using this method only returns half of the symmetrical spectrum (the mirrored other half is identical)
		/// </summary>
		/// <param name="buffer">The real-valued source data. Will be overwritten by the output data. Length must be a power of 2 and at least 16.</param>
		/// <param name="forward">Specifies whether to perform a forward or inverse transform.</param>
		/// <param name="flags">Specifies how to process the output data, default is None.</param>
		/// <exception cref="NotSupportedException">Buffer length is shorter than 16.</exception>
		public static void RealFFT ( Span<Complex> buffer, bool forward, FftFlags flags = FftFlags.None )
			=> RealFFT ( MemoryMarshal.Cast<Complex, double> ( buffer ), forward, flags );

		/// <summary>
		/// Compute the forward or inverse Fourier Transform of real-valued data.
		/// The data is modified in place.
		/// 
		/// Note that a forward transform takes real-valued data as inputs and outputs complex valued data as interleaved real and imaginary values,
		/// whereas an inverse transform takes complex valued data as interleaved real and imaginary values as inputs and outputs real-valued data.
		/// 
		/// In case of a forward transform, the real and imaginary output values at index 0 will be the DC and Nyquist components respectively.
		/// 
		/// Note that a forward transform using this method only returns half of the symmetrical spectrum (the mirrored other half is identical)
		/// </summary>
		/// <param name="complexRealValues">The real value parts of the complex numbers in case of an inverse transform, or even samples in case of a forward transform. Length must be a power of 2 and equal to the length of complexImagValues.</param>
		/// <param name="complexImagValues">The imaginary value parts of the complex numbers in case of an inverse transform, or odd samples in case of a forward transform. Length must be a power of 2 and equal to the length of complexRealValues.</param>
		/// <param name="forward">Specifies whether to perform a forward or inverse transform.</param>
		/// <param name="normalizeFactor">Normalization factor, should be left at the default of 1.0 in most cases.</param>
		/// <exception cref="NotSupportedException">Buffer length is shorter than 16.</exception>
		public static void RealFFT ( Span<double> complexRealValues, Span<double> complexImagValues, bool forward, double normalizeFactor = 1.0 )
			{
			var N = complexRealValues.Length;
			var LogN = MathUtils.ILog2 ( N );
			var HalfN = N >> 1;
			var Sign = forward ? 1.0 : -1.0;
			const double Half = 0.5;

			Debug.Assert ( LogN + 1 < Constants.MaxTableDepth, $"Constants.MaxTableDepth is too small to process DFT of size {N}. Should be at least {LogN + 2}" );

			if ( N < 8 )
				throw new NotSupportedException ( "buffer length must be at least 8" );

			var RealRootsOfUnity = _RealRootsOfUnity;
			var ImagRootsOfUnity = forward ? _ImagRootsOfUnity : _ImagInverseRootsOfUnity;
			var IterRealRootsOfUnity = RealRootsOfUnity[LogN + 1].AsSpan ();
			var IterImagRootsOfUnity = ImagRootsOfUnity[LogN + 1].AsSpan ();
			var RealValues = complexRealValues;
			var ImagValues = complexImagValues;

			if ( forward )
				FFT ( RealValues, ImagValues, true );

			// Split the mixed spectra

			var VecRealValues = MemoryMarshal.Cast<double, Vector512<double>> ( RealValues );
			var VecImagValues = MemoryMarshal.Cast<double, Vector512<double>> ( ImagValues );
			var VecRealRootsOfUnity = MemoryMarshal.Cast<double, Vector512<double>> ( IterRealRootsOfUnity );
			var VecImagRootsOfUnity = MemoryMarshal.Cast<double, Vector512<double>> ( IterImagRootsOfUnity );
			var ShiftVecRealValues = MemoryMarshal.Cast<double, Vector512<double>> ( RealValues.Slice ( HalfN + 1, HalfN - _Vector512Count ) );
			var ShiftVecImagValues = MemoryMarshal.Cast<double, Vector512<double>> ( ImagValues.Slice ( HalfN + 1, HalfN - _Vector512Count ) );

			var VectorizedOpCount = HalfN >> _Vector512SizeShift;
			var VecHalf = Vector512.Create ( Half );
			var VecSign = Vector512.Create ( Sign );

			Debug.Assert ( VectorizedOpCount > 0 );

			var OppositeK = VectorizedOpCount - 2;

			// Handle k = 0 ( complex index 0 ... VectorMaxIndex )

				{
				var KReal = VecRealValues[0];
				var KImag = VecImagValues[0];

				var OppositeKReal = Vector512.Create ( 0, RealValues[N - 1], RealValues[N - 2], RealValues[N - 3], RealValues[N - 4], RealValues[N - 5], RealValues[N - 6], RealValues[N - 7] );
				var OppositeKImag = Vector512.Create ( 0, ImagValues[N - 1], ImagValues[N - 2], ImagValues[N - 3], ImagValues[N - 4], ImagValues[N - 5], ImagValues[N - 6], ImagValues[N - 7] );

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
				RealValues[4] = VecReal[4];
				RealValues[5] = VecReal[5];
				RealValues[6] = VecReal[6];
				RealValues[7] = VecReal[7];
				ImagValues[1] = VecImag[1];
				ImagValues[2] = VecImag[2];
				ImagValues[3] = VecImag[3];
				ImagValues[4] = VecImag[4];
				ImagValues[5] = VecImag[5];
				ImagValues[6] = VecImag[6];
				ImagValues[7] = VecImag[7];

				var VecOppositeReal = VecHalf * ( e - VecSign * ( b + a ) );
				var VecOppositeImag = VecHalf * ( VecSign * ( d - c ) - f );

				// Leave [N] alone
				RealValues[N - 1] = VecOppositeReal[1];
				RealValues[N - 2] = VecOppositeReal[2];
				RealValues[N - 3] = VecOppositeReal[3];
				RealValues[N - 4] = VecOppositeReal[4];
				RealValues[N - 5] = VecOppositeReal[5];
				RealValues[N - 6] = VecOppositeReal[6];
				RealValues[N - 7] = VecOppositeReal[7];
				ImagValues[N - 1] = VecOppositeImag[1];
				ImagValues[N - 2] = VecOppositeImag[2];
				ImagValues[N - 3] = VecOppositeImag[3];
				ImagValues[N - 4] = VecOppositeImag[4];
				ImagValues[N - 5] = VecOppositeImag[5];
				ImagValues[N - 6] = VecOppositeImag[6];
				ImagValues[N - 7] = VecOppositeImag[7];
				}

			// Handle k > 0 ( complex index > VectorMaxIndex )

				{
				Vector512<double> KReal, KImag, OppositeKReal, OppositeKImag, RealRootOfUnity, ImagRootOfUnity, a, b, c, d, e, f;

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

				FFT ( RealValues, ImagValues, false, normalizeFactor );
				}
			}

		/// <summary>
		/// Compute the forward or inverse Fourier Transform of real-valued data.
		/// The data is modified in place.
		/// 
		/// Note that a forward transform takes real-valued data as inputs and outputs complex valued data as interleaved real and imaginary values,
		/// whereas an inverse transform takes complex valued data as interleaved real and imaginary values as inputs and outputs real-valued data.
		/// 
		/// In case of a forward transform, the output values at index 0 and 1 will be the DC and Nyquist components respectively.
		/// 
		/// Note that a forward transform using this method only returns half of the symmetrical spectrum (the mirrored other half is identical)
		/// </summary>
		/// <param name="buffer">The real-valued or interleaved complex-valued source data. Will be overwritten by the output data. Length must be a power of 2 and at least 16.</param>
		/// <param name="forward">Specifies whether to perform a forward or inverse transform.</param>
		/// <param name="flags">Specifies how to process the output data, default is None.</param>
		/// <exception cref="NotSupportedException">Buffer length is shorter than 16.</exception>
		public static void RealFFT ( Span<double> buffer, bool forward, FftFlags flags = FftFlags.None )
			{
			var N = buffer.Length >> 1;

			ArrayZip.UnZipInPlacePow2 ( buffer ); // Unzip

			var RealValues = buffer.Slice ( 0, N );
			var ImagValues = buffer.Slice ( N, N );

			var NormalizeFactor = flags.HasFlag ( FftFlags.DoNotNormalize ) ? ( N * 2.0 ) : 1.0; // TODO: This is wonky, we should be able to skip normalization alltogether. More research needed.
			RealFFT ( RealValues, ImagValues, forward, NormalizeFactor );

			if ( !flags.HasFlag ( FftFlags.DoNotRezip ) )
				ArrayZip.ZipInPlacePow2 ( buffer ); // Re-zip
			}
		}
	}
