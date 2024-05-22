using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Aelian.FFT
	{
	internal static class IndexReversal
		{
		private static int[][] _BitReverseIndices;
		private static SwapPair[][] _BitReverseSwapIndices;

		private struct SwapPair
			{
			public readonly int IndexA;
			public readonly int IndexB;

			public SwapPair ( int indexA, int indexB )
				{
				IndexA = indexA;
				IndexB = indexB;
				}
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

		public static void CalculateBitReverseIndices ()
			{
			_BitReverseIndices = new int[Constants.MaxTableDepth][];
			_BitReverseSwapIndices = new SwapPair[Constants.MaxTableDepth][];

			for ( int i = 0; i < Constants.MaxTableDepth; i++ )
				{
				var N = 1 << i;

				_BitReverseIndices[i] = new int[N];
				var Touched = new BitArray ( N );
				var Swaps = new List<SwapPair> ();

				for ( int j = 0; j < N; j++ )
					{
					var NewIndex = ReverseBitOrder ( j, i );

					_BitReverseIndices[i][j] = NewIndex;

					if ( NewIndex != j && !Touched[NewIndex] && !Touched[j] )
						Swaps.Add ( new SwapPair ( j, NewIndex ) );

					_BitReverseIndices[i][j] = NewIndex;

					Touched[j] = true;
					Touched[NewIndex] = true;
					}

				_BitReverseSwapIndices[i] = Swaps.ToArray ();
				}
			}

		/// <summary>
		/// Shuffles the selements of two arrays so that each element ends up at the index that is the bit-reverse of its original index
		/// </summary>
		/// <typeparam name="T">The array type</typeparam>
		/// <param name="arrayA">The first array</param>
		/// <param name="arrayB">The second array</param>
		/// <param name="logArraySize">The binary logarithm of the size of the arrays</param>
		[MethodImpl ( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
		public static void BitReverseArrayInPlace<T> ( Span<T> arrayA, Span<T> arrayB, int logArraySize )
			{
			var BitReverseSwaps = _BitReverseSwapIndices[logArraySize];

			foreach ( var Swap in BitReverseSwaps )
				{
				var TmpA = arrayA[Swap.IndexA];
				arrayA[Swap.IndexA] = arrayA[Swap.IndexB];
				arrayA[Swap.IndexB] = TmpA;

				var TmpB = arrayB[Swap.IndexA];
				arrayB[Swap.IndexA] = arrayB[Swap.IndexB];
				arrayB[Swap.IndexB] = TmpB;
				}
			}
		}
	}
