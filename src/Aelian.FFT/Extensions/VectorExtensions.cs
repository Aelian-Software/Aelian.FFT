using System.Runtime.Intrinsics;

namespace Aelian.FFT.Extensions;

internal static class VectorExtensions
	{
	// 128

	public static unsafe nuint GetByteMisalignment<T> ( Vector128<T>* pointer )
		where T : unmanaged
		=> ( (uint) sizeof ( Vector128<T> ) - ( (nuint) pointer % (uint) sizeof ( Vector128<T> ) ) ) % (uint) sizeof (Vector128<T> );

	public static unsafe nuint GetMisalignment<T> ( Vector128<T>* pointer )
		where T : unmanaged
		=> GetByteMisalignment ( pointer ) / (uint) sizeof ( T );

	// 256

	public static unsafe nuint GetByteMisalignment<T> ( Vector256<T>* pointer )
		where T : unmanaged
		=> ( (uint) sizeof ( Vector256<T> ) - ( (nuint) pointer % (uint) sizeof ( Vector256<T> ) ) ) % (uint) sizeof ( Vector256<T> );

	public static unsafe nuint GetMisalignment<T> ( Vector256<T>* pointer )
		where T : unmanaged
		=> GetByteMisalignment ( pointer ) / (uint) sizeof ( T );

	// 512

	public static unsafe nuint GetByteMisalignment<T> ( Vector512<T>* pointer )
		where T : unmanaged
		=> ( (uint) sizeof ( Vector512<T> ) - ( (nuint) pointer % (uint) sizeof ( Vector512<T> ) ) ) % (uint) sizeof ( Vector512<T> );

	public static unsafe nuint GetMisalignment<T> ( Vector512<T>* pointer )
		where T : unmanaged
		=> GetByteMisalignment ( pointer ) / (uint) sizeof ( T );
	}

