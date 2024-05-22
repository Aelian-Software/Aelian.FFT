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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Aelian.FFT
	{
	internal static class ArrayZip
		{
		private static int[][][] _UnZipCycleDecompositions;
		private static int[][][] _ZipCycleDecompositions;

		public static void CalculateUnZipCycleDecompositions ()
			{
			_UnZipCycleDecompositions = new int[Constants.MaxTableDepth + 1][][];
			_ZipCycleDecompositions = new int[Constants.MaxTableDepth + 1][][];

			for ( int i = 2; i < Constants.MaxTableDepth + 1; i++ ) // Skip 1 and 2 sized arrays
				{
				var N = 1 << i;

				// We skip indices 0 and N-1 as they never change position

				_UnZipCycleDecompositions[i] = GetCycleDecompositions ( 1, N - 2, a => MathUtils.RotateBitsRight ( a, i ) )
					.Select ( a => a.ToArray () )
					.ToArray ();

				_ZipCycleDecompositions[i] = GetCycleDecompositions ( 1, N - 2, a => MathUtils.RotateBitsLeft ( a, i ) )
					.Select ( a => a.ToArray () )
					.ToArray ();
				}
			}

		private static IEnumerable<IEnumerable<int>> GetCycleDecompositions ( int start, int length, Func<int, int> getPermutedIndex )
			{
			var Touched = new BitArray ( length );

			for ( int i = 0; i < length; i++ )
				{
				if ( Touched[i] )
					continue; // Already in another cycle

				var IndicesInCycle = GetCycle ( i + start, getPermutedIndex );

				yield return IndicesInCycle;

				foreach ( var IndexInCycle in IndicesInCycle )
					Touched.Set ( IndexInCycle - start, true );
				}
			}

		private static IEnumerable<int> GetCycleLeaders ( int start, int length, Func<int, int> getPermutedIndex ) =>
			GetCycleDecompositions ( start, length, getPermutedIndex ).Select ( a => a.First () );

		private static IEnumerable<int> GetCycle ( int startIndex, Func<int, int> getPermutedIndex )
			{
			var Index = startIndex;

			do
				{
				yield return Index;
				Index = getPermutedIndex ( Index );
				}
			while ( Index != startIndex );
			}

		/// <summary>
		/// This method rearranges the array so all even indexes end up at the start of the array
		/// and all odd indices end up at the end of the array.
		/// So, 0 1 2 3 4 5 6 7 becomes 0 2 4 6 1 3 5 7
		/// </summary>
		/// <typeparam name="T">the array data type</typeparam>
		/// <param name="elements">The array to unzip</param>
		[MethodImpl ( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
		public static void UnZipInPlacePow2<T> ( Span<T> elements ) => PermuteInPlacePow2 ( elements, _UnZipCycleDecompositions );

		/// <summary>
		/// This method is the inverse of UnZipInPlacePow2
		/// </summary>
		/// <typeparam name="T">the array data type</typeparam>
		/// <param name="elements">The array to zip</param>
		[MethodImpl ( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
		public static void ZipInPlacePow2<T> ( Span<T> elements ) => PermuteInPlacePow2 ( elements, _ZipCycleDecompositions );

		[MethodImpl ( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
		private static void PermuteInPlacePow2<T> ( Span<T> elements, int[][][] cycleDecompositions )
			{
			var N = elements.Length;
			var LogN = MathUtils.ILog2 ( N );

			Debug.Assert ( BitOperations.IsPow2 ( N ) );

			var CycleDecompositions = cycleDecompositions[LogN];

			foreach ( var UnZipCycles in CycleDecompositions )
				{
				var Index = UnZipCycles[^1];
				var Mem = elements[Index];

				for ( int i = 0; i < UnZipCycles.Length; i++ )
					{
					var NewVal = Mem;
					Index = UnZipCycles[i];
					Mem = elements[Index];
					elements[Index] = NewVal;
					}
				}
			}
		}
	}
