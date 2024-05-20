using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Aelian.FFT
	{
	internal static class IndexReversal
		{
		private static int[][] _BitReverseIndices;
		private static Tuple<int, int>[][] _BitReverseSwapIndices;

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
			_BitReverseSwapIndices = new Tuple<int, int>[Constants.MaxTableDepth][];

			for ( int i = 0; i < Constants.MaxTableDepth; i++ )
				{
				var N = 1 << i;

				_BitReverseIndices[i] = new int[N];
				var Touched = new BitArray ( N );
				var Swaps = new List<Tuple<int, int>> ();

				for ( int j = 0; j < N; j++ )
					{
					var NewIndex = ReverseBitOrder ( j, i );

					_BitReverseIndices[i][j] = NewIndex;

					if ( NewIndex != j && !Touched[NewIndex] && !Touched[j] )
						Swaps.Add ( new Tuple<int, int> ( j, NewIndex ) );

					_BitReverseIndices[i][j] = NewIndex;

					Touched[j] = true;
					Touched[NewIndex] = true;
					}

				_BitReverseSwapIndices[i] = Swaps.ToArray ();
				}
			}

		[MethodImpl ( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
		public static void BitReverseArrayInPlace<T> ( Span<T> arrayA, Span<T> arrayB, int logArraySize )
			{
			var BitReverseSwaps = _BitReverseSwapIndices[logArraySize];

			foreach ( var Swap in BitReverseSwaps )
				{
				var TmpA = arrayA[Swap.Item1];
				arrayA[Swap.Item1] = arrayA[Swap.Item2];
				arrayA[Swap.Item2] = TmpA;

				var TmpB = arrayB[Swap.Item1];
				arrayB[Swap.Item1] = arrayB[Swap.Item2];
				arrayB[Swap.Item2] = TmpB;
				}
			}
		}
	}
