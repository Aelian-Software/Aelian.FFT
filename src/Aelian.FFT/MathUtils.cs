using System.Numerics;
using System.Runtime.CompilerServices;

namespace Aelian.FFT
	{
	internal static class MathUtils
		{
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
