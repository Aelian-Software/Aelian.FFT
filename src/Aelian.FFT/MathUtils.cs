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

using System.Numerics;
using System.Runtime.CompilerServices;

namespace Aelian.FFT
	{
	internal static class MathUtils
		{
		/// <summary>
		/// A fast (but dirty) integer binary logarithm implementation.
		/// </summary>
		/// <param name="number">The number to get the binary logarithm of.</param>
		/// <returns>The binary logarithm of the specified number.</returns>
		[MethodImpl ( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
		public static int ILog2 ( int number ) => BitOperations.TrailingZeroCount ( (nuint) number );

		[MethodImpl ( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
		public static int RotateBitsRight ( int number, int bitSize )
			=> ( number >> 1 ) | ( number & 1 ) << ( bitSize - 1 );

		[MethodImpl ( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
		public static int RotateBitsLeft ( int number, int bitSize )
			{
			var Mask = ( 1 << bitSize ) - 1;
			return ( ( number << 1 ) & Mask ) | ( ( number >> ( bitSize - 1 ) ) & 1 );
			}
		}
	}
