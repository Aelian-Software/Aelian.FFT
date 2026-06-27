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
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Aelian.FFT;

internal static class ArrayZip
	{
	private static int[]? _UnZipCycleDecompositions;
	private static int[]? _ZipCycleDecompositions;

	public static void CalculateUnZipCycleDecompositions ()
		{
		var HeaderLength = Constants.MaxTableDepth + 2;
		var UnZipCycleDecompositions = new List<int> ( HeaderLength );
		var ZipCycleDecompositions = new List<int> ( HeaderLength );

		for ( int i = 0; i < HeaderLength; i++ )
			{
			UnZipCycleDecompositions.Add ( HeaderLength );
			ZipCycleDecompositions.Add ( HeaderLength );
			}

		for ( int i = 2; i < Constants.MaxTableDepth + 1; i++ ) // Skip 1 and 2 sized arrays
			{
			var N = 1 << i;

			UnZipCycleDecompositions[i] = UnZipCycleDecompositions.Count;
			ZipCycleDecompositions[i] = ZipCycleDecompositions.Count;

			// We skip indices 0 and N-1 as they never change position

			AddCycleDecompositions ( UnZipCycleDecompositions, N, i, true );
			AddCycleDecompositions ( ZipCycleDecompositions, N, i, false );
			}

		UnZipCycleDecompositions[Constants.MaxTableDepth + 1] = UnZipCycleDecompositions.Count;
		ZipCycleDecompositions[Constants.MaxTableDepth + 1] = ZipCycleDecompositions.Count;

		_UnZipCycleDecompositions = UnZipCycleDecompositions.ToArray ();
		_ZipCycleDecompositions = ZipCycleDecompositions.ToArray ();
		}

	private static void AddCycleDecompositions ( List<int> cycleDecompositions, int N, int bitCount, bool unZip )
		{
		for ( int CycleLeader = 1; CycleLeader < N - 1; CycleLeader++ )
			{
			var Index = unZip
				? MathUtils.RotateBitsRight ( CycleLeader, bitCount )
				: MathUtils.RotateBitsLeft ( CycleLeader, bitCount );

			while ( Index != CycleLeader && Index > CycleLeader )
				Index = unZip
					? MathUtils.RotateBitsRight ( Index, bitCount )
					: MathUtils.RotateBitsLeft ( Index, bitCount );

			if ( Index != CycleLeader )
				continue;

			var CycleHeaderIndex = cycleDecompositions.Count;
			var CycleLength = 0;

			cycleDecompositions.Add ( 0 );
			cycleDecompositions.Add ( 0 );

			Index = CycleLeader;

			do
				{
				cycleDecompositions.Add ( Index );
				CycleLength++;

				Index = unZip
					? MathUtils.RotateBitsRight ( Index, bitCount )
					: MathUtils.RotateBitsLeft ( Index, bitCount );
				}
			while ( Index != CycleLeader );

			cycleDecompositions[CycleHeaderIndex] = CycleLength;
			cycleDecompositions[CycleHeaderIndex + 1] = cycleDecompositions[^1];
			}
		}

	/// <summary>
	/// This method rearranges the array so all even indexes end up at the start of the array
	/// and all odd indices end up at the end of the array.
	/// So, 0 1 2 3 4 5 6 7 becomes 0 2 4 6 1 3 5 7
	/// </summary>
	/// <typeparam name="T">the array data type</typeparam>
	/// <param name="elements">The array to unzip</param>
	[MethodImpl ( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
	public static void UnZipInPlacePow2<T> ( Span<T> elements )
		{
		if ( _UnZipCycleDecompositions is null )
			throw new InvalidOperationException ( "CalculateUnZipCycleDecompositions has not yet been called" );

		PermuteInPlacePow2 ( elements, _UnZipCycleDecompositions );
		}

	/// <summary>
	/// This method is the inverse of UnZipInPlacePow2
	/// </summary>
	/// <typeparam name="T">the array data type</typeparam>
	/// <param name="elements">The array to zip</param>
	[MethodImpl ( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
	public static void ZipInPlacePow2<T> ( Span<T> elements )
		{
		if ( _ZipCycleDecompositions is null )
			throw new InvalidOperationException ( "CalculateUnZipCycleDecompositions has not yet been called" );

		PermuteInPlacePow2 ( elements, _ZipCycleDecompositions );
		}

	[MethodImpl ( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
	private static unsafe void PermuteInPlacePow2<T> ( Span<T> elements, int[] cycleDecompositions )
		{
		var N = elements.Length;
		var LogN = MathUtils.ILog2 ( N );

		Debug.Assert ( BitOperations.IsPow2 ( N ) );

		fixed ( int* pCycleDecompositions = cycleDecompositions )
			{
			var CycleDecomposition = pCycleDecompositions + pCycleDecompositions[LogN];
			var EndCycleDecomposition = pCycleDecompositions + pCycleDecompositions[LogN + 1];

			while ( CycleDecomposition < EndCycleDecomposition )
				{
				var CycleLength = *CycleDecomposition++;
				var Index = *CycleDecomposition++;
				var Mem = elements[Index];

				for ( int i = 0; i < CycleLength; i++ )
					{
					var NewVal = Mem;
					Index = *CycleDecomposition++;
					Mem = elements[Index];
					elements[Index] = NewVal;
					}
				}
			}
		}

	// Slow. Do not use! Only here for reference & testing.
	[MethodImpl ( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
	public static void ZipInPlacePow2NoLut<T> ( Span<T> elements )
		{
		var N = elements.Length;
		var LogN = MathUtils.ILog2 ( N );

		for ( int CycleLeader = 1; CycleLeader < N - 1; CycleLeader++ )
			{
			var Index = MathUtils.RotateBitsLeft ( CycleLeader, LogN );

			while ( Index != CycleLeader && Index > CycleLeader )
				Index = MathUtils.RotateBitsLeft ( Index, LogN );

			if ( Index != CycleLeader )
				continue;

			var Mem = elements[CycleLeader];
			Index = CycleLeader;

			do
				{
				Index = MathUtils.RotateBitsLeft ( Index, LogN );

				var NewVal = Mem;
				Mem = elements[Index];
				elements[Index] = NewVal;
				}
			while ( Index != CycleLeader );
			}
		}

	// Slow. Do not use! Only here for reference & testing.
	[MethodImpl ( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
	public static void UnZipInPlacePow2NoLut<T> ( Span<T> elements )
		{
		var N = elements.Length;
		var LogN = MathUtils.ILog2 ( N );

		for ( int CycleLeader = 1; CycleLeader < N - 1; CycleLeader++ )
			{
			var Index = MathUtils.RotateBitsRight ( CycleLeader, LogN );

			while ( Index != CycleLeader && Index > CycleLeader )
				Index = MathUtils.RotateBitsRight ( Index, LogN );

			if ( Index != CycleLeader )
				continue;

			var Mem = elements[CycleLeader];
			Index = CycleLeader;

			do
				{
				Index = MathUtils.RotateBitsRight ( Index, LogN );

				var NewVal = Mem;
				Mem = elements[Index];
				elements[Index] = NewVal;
				}
			while ( Index != CycleLeader );
			}
		}
	}

