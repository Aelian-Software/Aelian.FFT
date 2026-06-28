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
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Aelian.FFT;

internal static class ScratchArrayZip
	{
	private const byte _Reorder256 = 0xD8; // Reorder 0,2,1,3 into 0,1,2,3 (0b11_01_10_00)

	private static readonly Vector512<long> _EvenIndices =
	   Vector512.Create ( 0L, 2L, 4L, 6L, 8L, 10L, 12L, 14L );

	private static readonly Vector512<long> _OddIndices =
		Vector512.Create ( 1L, 3L, 5L, 7L, 9L, 11L, 13L, 15L );

	private static readonly Vector512<long> _FirstHalfIndices =
		Vector512.Create ( 0L, 8L, 1L, 9L, 2L, 10L, 3L, 11L );

	private static readonly Vector512<long> _SecondHalfIndices =
		Vector512.Create ( 4L, 12L, 5L, 13L, 6L, 14L, 7L, 15L );


	[MethodImpl ( MethodImplOptions.AggressiveInlining /*| MethodImplOptions.AggressiveOptimization*/ )]
	public static unsafe void UnZipToScratches 
		(
		ReadOnlySpan<double> elements,
		Span<double> evenElements,
		Span<double> oddElements 
		)
		{
		if ( ( elements.Length & 1 ) != 0 )
			throw new ArgumentException ( "Elements length must be a multiple of 2.", nameof ( elements ) );

		var HalfCount = elements.Length >> 1;

		if ( evenElements.Length != HalfCount )
			throw new ArgumentException ( "Scratch size mismatch.", nameof ( evenElements ) );

		if ( oddElements.Length != HalfCount )
			throw new ArgumentException ( "Scratch size mismatch.", nameof ( oddElements ) );

		if ( HalfCount == 0 )
			return;

		fixed ( double* pSource = elements )
		fixed ( double* pEven = evenElements )
		fixed ( double* pOdd = oddElements )
			{
			var SourceIndex = 0;
			var DestinationIndex = 0;

			if ( Avx512F.IsSupported )
				{
				var VectorizedCount = elements.Length & ~15;

				for ( ; SourceIndex < VectorizedCount; SourceIndex += 16, DestinationIndex += 8 )
					{
					var FirstHalf = Vector512.Load ( pSource + SourceIndex );
					var SecondHalf = Vector512.Load ( pSource + SourceIndex + 8 );
					var Evens = Avx512F.PermuteVar8x64x2 ( FirstHalf, _EvenIndices, SecondHalf );
					var Odds = Avx512F.PermuteVar8x64x2 ( FirstHalf, _OddIndices, SecondHalf );

					Evens.Store ( pEven + DestinationIndex );
					Odds.Store ( pOdd + DestinationIndex );
					}
				}

			// AVX2 fallback

			if ( Avx2.IsSupported )
				{
				var VectorizedCount = SourceIndex + ( ( elements.Length - SourceIndex ) & ~7 );

				for ( ; SourceIndex < VectorizedCount; SourceIndex += 8, DestinationIndex += 4 )
					{
					var FirstHalf = Avx.LoadVector256 ( pSource + SourceIndex );
					var SecondHalf = Avx.LoadVector256 ( pSource + SourceIndex + 4 );
					var Evens = Avx2.Permute4x64 ( Avx.UnpackLow ( FirstHalf, SecondHalf ), _Reorder256 );
					var Odds = Avx2.Permute4x64 ( Avx.UnpackHigh ( FirstHalf, SecondHalf ), _Reorder256 );

					Avx.Store ( pEven + DestinationIndex, Evens );
					Avx.Store ( pOdd + DestinationIndex, Odds );
					}
				}

			for ( ; DestinationIndex < HalfCount; DestinationIndex++, SourceIndex += 2 )
				{
				pEven[DestinationIndex] = pSource[SourceIndex];
				pOdd[DestinationIndex] = pSource[SourceIndex + 1];
				}
			}
		}

	[MethodImpl ( MethodImplOptions.AggressiveInlining /*| MethodImplOptions.AggressiveOptimization*/ )]
	public static unsafe void ZipFromScratches 
		(
		Span<double> elements,
		ReadOnlySpan<double> evenElements,
		ReadOnlySpan<double> oddElements 
		)
		{
		if ( ( elements.Length & 1 ) != 0 )
			throw new ArgumentException ( "Elements length must be a multiple of 2.", nameof ( elements ) );

		int HalfCount = elements.Length >> 1;

		if ( evenElements.Length != HalfCount )			
			throw new ArgumentException ( "Scratch size mismatch.", nameof ( evenElements ) );

		if ( oddElements.Length != HalfCount )
			throw new ArgumentException ( "Scratch size mismatch.", nameof ( oddElements ) );

		if ( HalfCount == 0 )
			return;

		fixed ( double* pDestination = elements )
		fixed ( double* pEven = evenElements )
		fixed ( double* pOdd = oddElements )
			{
			var SourceIndex = 0;
			var DestinationIndex = 0;

			if ( Avx512F.IsSupported )
				{
				var VectorizedCount = HalfCount & ~7;

				for ( ; SourceIndex < VectorizedCount; SourceIndex += 8, DestinationIndex += 16 )
					{
					var EvenValues = Vector512.Load ( pEven + SourceIndex );
					var OddValues = Vector512.Load ( pOdd + SourceIndex );
					var FirstHalf = Avx512F.PermuteVar8x64x2 ( EvenValues, _FirstHalfIndices, OddValues );
					var SecondHalf = Avx512F.PermuteVar8x64x2 ( EvenValues, _SecondHalfIndices, OddValues );

					FirstHalf.Store ( pDestination + DestinationIndex );
					SecondHalf.Store ( pDestination + DestinationIndex + 8 );
					}
				}

			// AVX2 fallback

			if ( Avx.IsSupported )
				{
				var VectorizedCount = SourceIndex + ( ( HalfCount - SourceIndex ) & ~3 );

				for ( ; SourceIndex < VectorizedCount; SourceIndex += 4, DestinationIndex += 8 )
					{
					var EvenValues = Avx.LoadVector256 ( pEven + SourceIndex );
					var OddValues = Avx.LoadVector256 ( pOdd + SourceIndex );
					var Low = Avx.UnpackLow ( EvenValues, OddValues );
					var High = Avx.UnpackHigh ( EvenValues, OddValues );
					var FirstHalf = Avx.Permute2x128 ( Low, High, 0x20 );
					var SecondHalf = Avx.Permute2x128 ( Low, High, 0x31 );

					Avx.Store ( pDestination + DestinationIndex, FirstHalf );
					Avx.Store ( pDestination + DestinationIndex + 4, SecondHalf );
					}
				}

			for ( ; SourceIndex < HalfCount; SourceIndex++, DestinationIndex += 2 )
				{
				pDestination[DestinationIndex] = pEven[SourceIndex];
				pDestination[DestinationIndex + 1] = pOdd[SourceIndex];
				}
			}
		}
	}

