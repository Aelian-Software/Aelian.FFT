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
using System.Runtime.InteropServices;

namespace Aelian.FFT;

/// <summary>
/// Represents a strongly typed block of memory that is guaranteed to be aligned to the specified memory boundary.
/// </summary>
/// <typeparam name="T">The type of elements in the memory buffer.</typeparam>
public unsafe class AlignedMemory<T> : Disposable<AlignedMemory<T>>
	where T : unmanaged
	{
	public T* DataPointer { get; private set; }
	public int Length { get; }
	public int ByteLength { get; }
	public nuint MemoryAlignmentBoundaryBytes { get; }

	protected AlignedMemory ( int length, nuint memoryAlignmentBoundaryBytes )
		{
		Length = length;
		ByteLength = Length * sizeof ( T );
		MemoryAlignmentBoundaryBytes = memoryAlignmentBoundaryBytes;
		DataPointer = (T*) NativeMemory.AlignedAlloc ( (nuint) ByteLength, MemoryAlignmentBoundaryBytes );
		}

	/// <summary>
	/// Get a Span pointing to the allocated memory
	/// </summary>
	/// <returns>A Span pointing to the allocated memory.</returns>
	public Span<T> AsSpan () => new Span<T> ( DataPointer, Length );

	/// <summary>
	/// Get a Span pointing to the allocated memory
	/// </summary>
	/// <returns>A Span pointing from the start element to the end of the allocated memory.</returns>
	public Span<T> AsSpan ( int start ) => AsSpan ().Slice ( start );

	/// <summary>
	/// Get a Span pointing to the allocated memory
	/// </summary>
	/// <returns>A Span consisting of length elements, starting at start.</returns>
	public Span<T> AsSpan ( int start, int length ) => AsSpan ().Slice ( start, length );

	/// <summary>
	/// Get a typed Span pointing to the allocated memory
	/// </summary>
	/// <returns>A typed Span pointing to the allocated memory.</returns>
	public Span<TTo> AsSpanOf<TTo> ()
		where TTo : unmanaged
		=> MemoryMarshal.Cast<T, TTo> ( AsSpan () );

	public T[] ToArray () => AsSpan ().ToArray ();

	/// <summary>
	/// Make an exact clone of this instance of AlignedMemory<T>.
	/// </summary>
	/// <returns>An exact clone of this instance of AlignedMemory<T>.</returns>
	public AlignedMemory<T> Clone ()
		{
		var Out = new AlignedMemory<T> ( Length, MemoryAlignmentBoundaryBytes );
		CopyTo ( Out );
		return Out;
		}

	/// <summary>
	/// Copy the data in this AlignedMemory<T> instance to another instance of AlignedMemory<T>. This method will overwrite the data in the destination instance.
	/// </summary>
	/// <param name="destination">The destination AlignedMemory<T> instance.</param>
	/// <exception cref="ArgumentException">The lengths do not match.</exception>
	public void CopyTo ( AlignedMemory<T> destination )
		{
		if ( destination.Length != Length )
			throw new ArgumentException ( "Source and destination length must be equal", nameof ( destination ) );

		AsSpan ().CopyTo ( destination.AsSpan () );
		}

	/// <summary>
	/// Allocates an aligned memory buffer of the specified amount of elements and with the specified alignment boundary
	/// </summary>
	/// <param name="length">The amount of elements to allocate.</param>
	/// <param name="memoryAlignmentBoundaryBytes">The alignment, in bytes, of the memory to allocate. This must be a power of 2.</param>
	/// <returns>The aligned memory buffer.</returns>
	/// <exception cref="ArgumentException">alignment is not a power of two.</exception>
	/// <exception cref="OutOfMemoryException">Allocating the memory failed.</exception>
	public static AlignedMemory<T> Allocate ( int length, nuint memoryAlignmentBoundaryBytes = 64 )
		=> new AlignedMemory<T> ( length, memoryAlignmentBoundaryBytes );

	protected override void FreeUnmanagedResources ()
		{
		NativeMemory.AlignedFree ( DataPointer );
		DataPointer = null;
		}
	}

