//
// Aelian.FFT
//
// A highly optimized fast fourier transform implementation for .NET
//
// https://github.com/Aelian-Software/Aelian.FFT
//
// MIT License
//   
// Copyright (c) 2026 Bas Tossings / Aelian
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

namespace Aelian.FFT;

public static class FastFourierTransform
	{
	private static AlignedMemory<double>[]? _RealRootsOfUnity;
	private static AlignedMemory<double>[]? _ImagRootsOfUnity;
	private static AlignedMemory<double>[]? _ImagInverseRootsOfUnity; 
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
		_RealRootsOfUnity = new AlignedMemory<double>[Constants.MaxTableDepth];
		_ImagRootsOfUnity = new AlignedMemory<double>[Constants.MaxTableDepth];
		_ImagInverseRootsOfUnity = new AlignedMemory<double>[Constants.MaxTableDepth];

		for ( int i = 0; i < Constants.MaxTableDepth; i++ )
			{
			var N = 1 << i;

			var RealRootsOfUnity = AlignedMemory<double>.Allocate ( N );
			var ImagRootsOfUnity = AlignedMemory<double>.Allocate ( N );
			var ImagInverseRootsOfUnity = AlignedMemory<double>.Allocate ( N );

			_RealRootsOfUnity[i] = RealRootsOfUnity;
			_ImagRootsOfUnity[i] = ImagRootsOfUnity;
			_ImagInverseRootsOfUnity[i] = ImagInverseRootsOfUnity;

			var RealRootsOfUnitySpan = RealRootsOfUnity.AsSpan ();
			var ImagRootsOfUnitySpan = ImagRootsOfUnity.AsSpan ();
			var ImagInverseRootsOfUnitySpan = ImagInverseRootsOfUnity.AsSpan ();

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
				RealRootsOfUnitySpan[k] = RootsOfUnity.Real;
				ImagRootsOfUnitySpan[k] = RootsOfUnity.Imaginary;
				ImagInverseRootsOfUnitySpan[k] = -RootsOfUnity.Imaginary;
				}
			}
		}

	static readonly Vector256<long> _VecReverse256 = Vector256.Create ( new long[] { 3, 2, 1, 0 } );

	[MethodImpl ( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
	private static Vector256<double> Reverse ( in Vector256<double> vec )
		{
		return Vector256.Shuffle ( vec, _VecReverse256 );
		}

	static readonly Vector512<long> _VecReverse512 = Vector512.Create ( new long[] { 7, 6, 5, 4, 3, 2, 1, 0 } );

	[MethodImpl ( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
	private static Vector512<double> Reverse ( in Vector512<double> vec )
		{
		return Vector512.Shuffle ( vec, _VecReverse512 );
		}

	/// <summary>
	/// Compute the forward or inverse Fourier Transform of complex-valued data.
	/// The data is modified in place.
	/// </summary>
	/// <param name="buffer">The complex-valued source data. Will be overwritten by the output data. Length must be a power of 2.</param>
	/// <param name="forward">Specifies whether to perform a forward or inverse transform.</param>
	/// <exception cref="ArgumentException">Buffer length is not a power of 2</exception>
	public static void FFT ( Span<Complex> buffer, bool forward )
		{
		var n = buffer.Length;

		ArgumentOutOfRangeException.ThrowIfLessThan ( n, 8, nameof ( buffer ) );

		if ( !BitOperations.IsPow2 ( n ) )
			throw new ArgumentException ( "Buffer size must be a power of 2", nameof ( buffer ) );

		var UnZippedBuffer = MemoryMarshal.Cast<Complex, double> ( buffer );

		ArrayZip.UnZipInPlacePow2 ( UnZippedBuffer ); // Unzip

		var RealValues = UnZippedBuffer.Slice ( 0, n );
		var ImagValues = UnZippedBuffer.Slice ( n, n );

		FFT ( RealValues, ImagValues, forward );

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
	public static unsafe void FFT ( Span<double> complexRealValues, Span<double> complexImagValues, bool forward, double normalizeFactor = 1.0 )
		{
		if ( _RealRootsOfUnity is null || _ImagRootsOfUnity is null || _ImagInverseRootsOfUnity is null )
			throw new InvalidOperationException ( "FastFourierTransform.Initialize () has not yet been called" );

		var n = complexRealValues.Length;

		if ( n != complexImagValues.Length )
			throw new ArgumentException ( "Imaginary and real buffer sizes must be the same", nameof ( complexImagValues ) );

		ArgumentOutOfRangeException.ThrowIfLessThan ( n, 4, nameof ( complexRealValues ) );

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

		fixed ( double* pComplexRealValues = complexRealValues )
		fixed ( double* pComplexImagValues = complexImagValues )
			{
			for ( int s = 1; s <= LogN && s < ( _Vector128SizeShift + 1 ); s++ )
				{
				var m = 1 << s;
				var HalfM = m >> 1;

				var pIterRealRootsOfUnityStart = RealRootsOfUnity[s].DataPointer;
				var pIterImagRootsOfUnityStart = ImagRootsOfUnity[s].DataPointer;

				for ( int k = 0; k < n; k += m )
					{
					IndexEven = k;
					IndexOdd = IndexEven + HalfM;

					var pComplexRealValueEven = pComplexRealValues + IndexEven;
					var pComplexRealValueOdd = pComplexRealValues + IndexOdd;
					var pComplexImagValueEven = pComplexImagValues + IndexEven;
					var pComplexImagValueOdd = pComplexImagValues + IndexOdd;
					var pIterRealRootsOfUnity = pIterRealRootsOfUnityStart;
					var pIterImagRootsOfUnity = pIterImagRootsOfUnityStart;

					// Process j == 0 differently, since at that index, WmReal is always 1 and WmImag is always 0

					EvenReal = *pComplexRealValueEven;
					EvenImag = *pComplexImagValueEven;
					OddReal = *pComplexRealValueOdd;
					OddImag = *pComplexImagValueOdd;

					*pComplexRealValueEven = EvenReal + OddReal;
					*pComplexImagValueEven = EvenImag + OddImag;
					*pComplexRealValueOdd = EvenReal - OddReal;
					*pComplexImagValueOdd = EvenImag - OddImag;

					pComplexRealValueEven++;
					pComplexRealValueOdd++;
					pComplexImagValueEven++;
					pComplexImagValueOdd++;

					// After that, continue as normal

					for 
						(
						int j = 1; j < HalfM; j++,  
						pIterRealRootsOfUnity++, 
						pIterImagRootsOfUnity++,
						pComplexRealValueEven++,
						pComplexRealValueOdd++,
						pComplexImagValueEven++,
						pComplexImagValueOdd++
						)
						{
						EvenReal = *pComplexRealValueEven;
						EvenImag = *pComplexImagValueEven;
						OddReal = *pComplexRealValueOdd;
						OddImag = *pComplexImagValueOdd;
						WmReal = *pIterRealRootsOfUnity;
						WmImag = *pIterImagRootsOfUnity;

						TReal = WmReal * OddReal - WmImag * OddImag;
						TImag = WmImag * OddReal + WmReal * OddImag;

						*pComplexRealValueEven = EvenReal + TReal;
						*pComplexImagValueEven = EvenImag + TImag;
						*pComplexRealValueOdd = EvenReal - TReal;
						*pComplexImagValueOdd = EvenImag - TImag;
						}
					}
				}

			// Vectorized

			// 128

				{
				var pVec128RealValues = (Vector128<double>*) pComplexRealValues;
				var pVec128ImagValues = (Vector128<double>*) pComplexImagValues;

				int VecIndexEven, VecIndexOdd;
				Vector128<double> VecEvenReal, VecEvenImag, VecOddReal, VecOddImag, VecWmReal, VecWmImag, VecTReal, VecTImag;

				const int s = _Vector128SizeShift + 1;

				var m = 1 << s;
				var HalfM = m >> 1;
				var VectorizedOpCount = HalfM >> _Vector128SizeShift;

				var pVecRealRootsOfUnityStart = (Vector128<double>*) RealRootsOfUnity[s].DataPointer;
				var pVecImagRootsOfUnityStart = (Vector128<double>*) ImagRootsOfUnity[s].DataPointer;

				for ( int k = 0; k < n; k += m )
					{
					VecIndexEven = k >> _Vector128SizeShift;
					VecIndexOdd = VecIndexEven + VectorizedOpCount;

					var pVec128RealValueEven = pVec128RealValues + VecIndexEven;
					var pVec128ImagValueEven = pVec128ImagValues + VecIndexEven;
					var pVec128RealValueOdd = pVec128RealValues + VecIndexOdd;
					var pVec128ImagValueOdd = pVec128ImagValues + VecIndexOdd;
					var pVecRealRootsOfUnity = pVecRealRootsOfUnityStart;
					var pVecImagRootsOfUnity = pVecImagRootsOfUnityStart;

					for 
						(
						int i = 0; i < VectorizedOpCount; i++, 
						pVecRealRootsOfUnity++, 
						pVecImagRootsOfUnity++,
						pVec128RealValueEven++,
						pVec128ImagValueEven++,
						pVec128RealValueOdd++,
						pVec128ImagValueOdd++
						)
						{
						VecEvenReal = *pVec128RealValueEven;
						VecEvenImag = *pVec128ImagValueEven;
						VecOddReal = *pVec128RealValueOdd;
						VecOddImag = *pVec128ImagValueOdd;
						VecWmReal = *pVecRealRootsOfUnity;
						VecWmImag = *pVecImagRootsOfUnity;

						VecTReal = VecWmReal * VecOddReal - VecWmImag * VecOddImag;
						VecTImag = VecWmImag * VecOddReal + VecWmReal * VecOddImag;

						*pVec128RealValueEven = VecEvenReal + VecTReal;
						*pVec128ImagValueEven = VecEvenImag + VecTImag;
						*pVec128RealValueOdd = VecEvenReal - VecTReal;
						*pVec128ImagValueOdd = VecEvenImag - VecTImag;
						}
					}
				}

			// 256

				{
				var pVec256RealValues = (Vector256<double>*) pComplexRealValues;
				var pVec256ImagValues = (Vector256<double>*) pComplexImagValues;

				int VecIndexEven, VecIndexOdd;
				Vector256<double> VecEvenReal, VecEvenImag, VecOddReal, VecOddImag, VecWmReal, VecWmImag, VecTReal, VecTImag;

				const int s = _Vector256SizeShift + 1;

				var m = 1 << s;
				var HalfM = m >> 1;
				var VectorizedOpCount = HalfM >> _Vector256SizeShift;

				var pVecRealRootsOfUnityStart = (Vector256<double>*) RealRootsOfUnity[s].DataPointer;
				var pVecImagRootsOfUnityStart = (Vector256<double>*) ImagRootsOfUnity[s].DataPointer;

				for ( int k = 0; k < n; k += m )
					{
					VecIndexEven = k >> _Vector256SizeShift;
					VecIndexOdd = VecIndexEven + VectorizedOpCount;

					var pVec256RealValueEven = pVec256RealValues + VecIndexEven;
					var pVec256ImagValueEven = pVec256ImagValues + VecIndexEven;
					var pVec256RealValueOdd = pVec256RealValues + VecIndexOdd;
					var pVec256ImagValueOdd = pVec256ImagValues + VecIndexOdd;
					var pVecRealRootsOfUnity = pVecRealRootsOfUnityStart;
					var pVecImagRootsOfUnity = pVecImagRootsOfUnityStart;

					for 
						( 
						int i = 0; i < VectorizedOpCount; i++, 
						pVecRealRootsOfUnity++, 
						pVecImagRootsOfUnity++,
						pVec256RealValueEven++,
						pVec256ImagValueEven++,
						pVec256RealValueOdd++,
						pVec256ImagValueOdd++
						)
						{
						VecEvenReal = *pVec256RealValueEven;
						VecEvenImag = *pVec256ImagValueEven;
						VecOddReal = *pVec256RealValueOdd;
						VecOddImag = *pVec256ImagValueOdd;
						VecWmReal = *pVecRealRootsOfUnity;
						VecWmImag = *pVecImagRootsOfUnity;

						VecTReal = VecWmReal * VecOddReal - VecWmImag * VecOddImag;
						VecTImag = VecWmImag * VecOddReal + VecWmReal * VecOddImag;

						*pVec256RealValueEven = VecEvenReal + VecTReal;
						*pVec256ImagValueEven = VecEvenImag + VecTImag;
						*pVec256RealValueOdd = VecEvenReal - VecTReal;
						*pVec256ImagValueOdd = VecEvenImag - VecTImag;
						}
					}
				}

			// 512

			var pVec512RealValues = (Vector512<double>*) pComplexRealValues;
			var pVec512ImagValues = (Vector512<double>*) pComplexImagValues;

				{
				int VecIndexEven, VecIndexOdd;
				Vector512<double> VecEvenReal, VecEvenImag, VecOddReal, VecOddImag, VecWmReal, VecWmImag, VecTReal, VecTImag;

				for ( int s = ( _Vector512SizeShift + 1 ); s <= LogN; s++ )
					{
					var m = 1 << s;
					var HalfM = m >> 1;
					var VectorizedOpCount = HalfM >> _Vector512SizeShift;

					var pVecRealRootsOfUnityStart = (Vector512<double>*) RealRootsOfUnity[s].DataPointer;
					var pVecImagRootsOfUnityStart = (Vector512<double>*) ImagRootsOfUnity[s].DataPointer;

					for ( int k = 0; k < n; k += m )
						{
						VecIndexEven = k >> _Vector512SizeShift;
						VecIndexOdd = VecIndexEven + VectorizedOpCount;

						var pVec512RealValueEven = pVec512RealValues + VecIndexEven;
						var pVec512ImagValueEven = pVec512ImagValues + VecIndexEven;
						var pVec512RealValueOdd = pVec512RealValues + VecIndexOdd;
						var pVec512ImagValueOdd = pVec512ImagValues + VecIndexOdd;
						var pVecRealRootsOfUnity = pVecRealRootsOfUnityStart;
						var pVecImagRootsOfUnity = pVecImagRootsOfUnityStart;

						for 
							(
							int i = 0; i < VectorizedOpCount; i++, 							
							pVecRealRootsOfUnity++, 
							pVecImagRootsOfUnity++,
							pVec512RealValueEven++,
							pVec512ImagValueEven++,
							pVec512RealValueOdd++,
							pVec512ImagValueOdd++
							)
							{
							VecEvenReal = *pVec512RealValueEven;
							VecEvenImag = *pVec512ImagValueEven;
							VecOddReal = *pVec512RealValueOdd;
							VecOddImag = *pVec512ImagValueOdd;
							VecWmReal = *pVecRealRootsOfUnity;
							VecWmImag = *pVecImagRootsOfUnity;

							VecTReal = VecWmReal * VecOddReal - VecWmImag * VecOddImag;
							VecTImag = VecWmImag * VecOddReal + VecWmReal * VecOddImag;

							*pVec512RealValueEven = VecEvenReal + VecTReal;
							*pVec512ImagValueEven = VecEvenImag + VecTImag;
							*pVec512RealValueOdd = VecEvenReal - VecTReal;
							*pVec512ImagValueOdd = VecEvenImag - VecTImag;
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

				var pVec512RealValue = pVec512RealValues;
				var pVec512ImagValue = pVec512ImagValues;

				for 
					(
					int i = 0; i < VectorizedOpCount; i++,
					pVec512RealValue++,
					pVec512ImagValue++
					)
					{
					*pVec512RealValue *= Divisor;
					*pVec512ImagValue *= Divisor;
					}
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

	private static readonly AlignedMemory<double> _DebugScratchA = AlignedMemory<double>.Allocate ( 1024 * 16 );
	private static readonly AlignedMemory<double> _DebugScratchB = AlignedMemory<double>.Allocate ( 1024 * 16 );

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

#if true
		var RealValues = _DebugScratchA.AsSpan ().Slice ( 0, N );
		var ImagValues = _DebugScratchB.AsSpan ().Slice ( 0, N );
		ScratchArrayZip.UnZipToScratches ( buffer, RealValues, ImagValues );

		var NormalizeFactor = ( flags & FftFlags.DoNotNormalize ) != 0 ? ( N * 2.0 ) : 1.0; // TODO: This is wonky, we should be able to skip normalization alltogether. More research needed.
		RealFFT ( RealValues, ImagValues, forward, NormalizeFactor );

		ScratchArrayZip.ZipFromScratches ( buffer, RealValues, ImagValues );
#else
		ArrayZip.UnZipInPlacePow2 ( buffer ); // Unzip

		var RealValues = buffer.Slice ( 0, N );
		var ImagValues = buffer.Slice ( N, N );

		var NormalizeFactor = ( flags & FftFlags.DoNotNormalize ) != 0 ? ( N * 2.0 ) : 1.0; // TODO: This is wonky, we should be able to skip normalization alltogether. More research needed.
		RealFFT ( RealValues, ImagValues, forward, NormalizeFactor );

		ArrayZip.ZipInPlacePow2 ( buffer ); // Re-zip
#endif
		}

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
	public static unsafe void RealFFT ( Span<double> complexRealValues, Span<double> complexImagValues, bool forward, double normalizeFactor = 1.0 )
		{
		if ( _RealRootsOfUnity is null || _ImagRootsOfUnity is null || _ImagInverseRootsOfUnity is null )
			throw new InvalidOperationException ( "FastFourierTransform.Initialize () has not yet been called" );

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

			fixed ( Vector512<double>* pVecRealValues = VecRealValues )
			fixed ( Vector512<double>* pVecImagValues = VecImagValues )
			fixed ( Vector512<double>* pShiftVecRealValues = ShiftVecRealValues )
			fixed ( Vector512<double>* pShiftVecImagValues = ShiftVecImagValues )
			fixed ( Vector512<double>* pVecRealRootsOfUnity = VecRealRootsOfUnity )
			fixed ( Vector512<double>* pVecImagRootsOfUnity = VecImagRootsOfUnity )
				{
				var pVecRealRootsOfUnityCur = pVecRealRootsOfUnity + 1;
				var pVecImagRootsOfUnityCur = pVecImagRootsOfUnity + 1;
				var pVecRealValue = pVecRealValues + 1;
				var pVecImagValue = pVecImagValues + 1;
				var pShiftVecRealValue = pShiftVecRealValues + OppositeK;
				var pShiftVecImagValue = pShiftVecImagValues + OppositeK;

				for ( var k = 1; k < VectorizedOpCount; k++ )
					{
					KReal = *pVecRealValue;
					KImag = *pVecImagValue;
					OppositeKReal = Reverse ( *pShiftVecRealValue );
					OppositeKImag = Reverse ( *pShiftVecImagValue );

					RealRootOfUnity = *pVecRealRootsOfUnityCur;
					ImagRootOfUnity = *pVecImagRootsOfUnityCur;

					a = ( KReal - OppositeKReal ) * ImagRootOfUnity;
					b = ( KImag + OppositeKImag ) * RealRootOfUnity;
					c = ( KReal - OppositeKReal ) * RealRootOfUnity;
					d = ( KImag + OppositeKImag ) * ImagRootOfUnity;
					e = KReal + OppositeKReal;
					f = KImag - OppositeKImag;

					*pVecRealValue = VecHalf * ( e + VecSign * ( a + b ) );
					*pVecImagValue = VecHalf * ( f + VecSign * ( d - c ) );

					*pShiftVecRealValue = Reverse ( VecHalf * ( e - VecSign * ( b + a ) ) );
					*pShiftVecImagValue = Reverse ( VecHalf * ( VecSign * ( d - c ) - f ) );

					pVecRealRootsOfUnityCur++;
					pVecImagRootsOfUnityCur++;
					pVecRealValue++;
					pVecImagValue++;
					pShiftVecRealValue--;
					pShiftVecImagValue--;
					}
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
	}

