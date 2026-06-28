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
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Aelian.FFT;

internal static class IndexReversal
	{
	private static SwapPair[][]? _BitReverseSwapIndices;

	private readonly struct SwapPair
		{
		public readonly int IndexA;
		public readonly int IndexB;

		public SwapPair ( int indexA, int indexB )
			{
			IndexA = indexA;
			IndexB = indexB;
			}

		public override string ToString () => $"{IndexA} <-> {IndexB}";
		}

	private static int ReverseBitOrder ( int input, int bitCount )
		{
		int Output = 0;
		int BitIndex = bitCount;

		while ( BitIndex > 0 )
			{
			if ( ( input & ( 1 << ( bitCount - BitIndex ) ) ) != 0 )
				Output |= 1 << ( BitIndex - 1 );

			BitIndex--;
			}

		return Output;
		}

	private static int FastReverseBitOrder ( int input, int bitCount )
		{
		int Output = 0;
		
		for ( int i = 0; i < bitCount; i++ )
			{
			Output = ( Output << 1 ) | ( input & 1 );
			input >>= 1;
			}

		return Output;
		}

	public static void CalculateBitReverseIndices ()
		{
		_BitReverseSwapIndices = new SwapPair[Constants.MaxTableDepth][];

		for ( int Depth = 0; Depth < Constants.MaxTableDepth; Depth++ )
			{
			int N = 1 << Depth;
			var Indices = new int[N];
			var Swaps = new List<SwapPair> ( N / 2 );

			for ( int i = 0; i < N; i++ )
				{
				int Reversed = ReverseBitOrder ( i, Depth );
				Indices[i] = Reversed;

				if ( Reversed > i )
					Swaps.Add ( new SwapPair ( i, Reversed ) );
				}

			_BitReverseSwapIndices[Depth] = Swaps.ToArray ();
			}
		}

	/// <summary>
	/// Shuffles the elements of two arrays so that each element ends up at the index that is the bit-reverse of its original index
	/// </summary>
	/// <typeparam name="T">The array type</typeparam>
	/// <param name="arrayA">The first array</param>
	/// <param name="arrayB">The second array</param>
	/// <param name="logArraySize">The binary logarithm of the size of the arrays</param>
	[MethodImpl ( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
	public static unsafe void BitReverseArrayInPlace<T> ( Span<T> arrayA, Span<T> arrayB, int logArraySize )
		where T : unmanaged
		{
		if ( _BitReverseSwapIndices is null )
			throw new InvalidOperationException ( "IndexReversal.CalculateBitReverseIndices () has not yet been called" );

		var BitReverseSwaps = _BitReverseSwapIndices[logArraySize];
		var PairCount = BitReverseSwaps.Length;

		fixed ( SwapPair* pPairs = BitReverseSwaps )
		fixed ( T* pArrayA = arrayA )
		fixed ( T* pArrayB = arrayB )
			{
			var pPair = pPairs;

			for ( int i = 0; i < PairCount; i++, pPair++ )
				{
				var IndexA = pPair->IndexA;
				var IndexB = pPair->IndexB;

				var TmpA = pArrayA[IndexA];
				pArrayA[IndexA] = pArrayA[IndexB];
				pArrayA[IndexB] = TmpA;

				var TmpB = pArrayB[IndexA];
				pArrayB[IndexA] = pArrayB[IndexB];
				pArrayB[IndexB] = TmpB;
				}
			}
		}

	/// <summary>
	/// Shuffles the elements of two double arrays so that each element ends up at the index that is the bit-reverse of its original index
	/// </summary>
	/// <param name="arrayA">The first array</param>
	/// <param name="arrayB">The second array</param>
	/// <param name="logArraySize">The binary logarithm of the size of the arrays</param>
	[MethodImpl ( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
	public static unsafe void BitReverseArrayInPlace ( Span<double> arrayA, Span<double> arrayB, int logArraySize )
		{
		if ( _BitReverseSwapIndices is null )
			throw new InvalidOperationException ( "IndexReversal.CalculateBitReverseIndices () has not yet been called" );

		var BitReverseSwaps = _BitReverseSwapIndices[logArraySize];
		var PairCount = BitReverseSwaps.Length;

		fixed ( SwapPair* pPairs = BitReverseSwaps )
		fixed ( double* pArrayA = arrayA )
		fixed ( double* pArrayB = arrayB )
			{
			var pPair = pPairs;

			for ( int i = 0; i < PairCount; i++, pPair++ )
				{
				var IndexA = pPair->IndexA;
				var IndexB = pPair->IndexB;

				var TmpA = pArrayA[IndexA];
				pArrayA[IndexA] = pArrayA[IndexB];
				pArrayA[IndexB] = TmpA;

				var TmpB = pArrayB[IndexA];
				pArrayB[IndexA] = pArrayB[IndexB];
				pArrayB[IndexB] = TmpB;
				}
			}
		}

	/// <summary>
	/// Shuffles the elements of two arrays so that each element ends up at the index that is the bit-reverse of its original index
	/// </summary>
	/// <typeparam name="T">The array type</typeparam>
	/// <param name="arrayA">The first array</param>
	/// <param name="arrayB">The second array</param>
	/// <param name="logArraySize">The binary logarithm of the size of the arrays</param>
	[MethodImpl ( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
	public static void BitReverseArrayInPlaceNoLut<T> (	Span<T> arrayA,	Span<T> arrayB,	int logArraySize )
		where T : unmanaged
		{
		int N = 1 << logArraySize;

		if ( (uint) N > (uint) arrayA.Length || (uint) N > (uint) arrayB.Length )
			throw new ArgumentException ( "The spans are shorter than the transform size." );

		ref T RefArrayA = ref System.Runtime.InteropServices.MemoryMarshal.GetReference ( arrayA );
		ref T RefArrayB = ref System.Runtime.InteropServices.MemoryMarshal.GetReference ( arrayB );

		int j = 0;

		for ( int i = 1; i < N - 1; i++ )
			{
			int Bit = N >> 1;

			while ( ( j & Bit ) != 0 )
				{
				j ^= Bit;
				Bit >>= 1;
				}

			j ^= Bit;

			if ( i >= j )
				continue;

			ref T AI = ref Unsafe.Add ( ref RefArrayA, i );
			ref T AJ = ref Unsafe.Add ( ref RefArrayA, j );
			
			T TempA = AI;
			AI = AJ;
			AJ = TempA;

			ref T BI = ref Unsafe.Add ( ref RefArrayB, i );
			ref T BJ = ref Unsafe.Add ( ref RefArrayB, j );
			
			T TempB = BI;
			BI = BJ;
			BJ = TempB;
			}
		}
	}

