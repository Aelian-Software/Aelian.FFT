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
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading;

namespace Aelian.FFT;

internal static class AlignedMemoryPool<T>
	where T : unmanaged
	{
	private const nuint _MemoryAlignmentBoundaryBytes = 64;
	private const int _MaximumRetainedBuffersPerLength = 4;

	private sealed class PoolBucket
		{
		public readonly ConcurrentBag<AlignedMemory<T>> Buffers = new ();
		public int RetainedBufferCount;
		}

	private static readonly ConcurrentDictionary<int, PoolBucket> _Buckets = new ();

	public static AlignedMemoryLease<T> Rent ( int minimumLength )
		{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero ( minimumLength );

		var Length = BitOperations.RoundUpToPowerOf2 ( (uint) minimumLength );

		if ( Length == 0 || Length > int.MaxValue )
			throw new OutOfMemoryException ();

		var Bucket = _Buckets.GetOrAdd ( (int) Length, static _ => new PoolBucket () );

		if ( Bucket.Buffers.TryTake ( out var Memory ) )
			Interlocked.Decrement ( ref Bucket.RetainedBufferCount );
		else
			Memory = AlignedMemory<T>.Allocate ( (int) Length, _MemoryAlignmentBoundaryBytes );

		return new AlignedMemoryLease<T> ( Memory, Bucket, Return );
		}

	private static void Return ( AlignedMemory<T> memory, object state )
		{
		var Bucket = (PoolBucket) state;

		if ( Interlocked.Increment ( ref Bucket.RetainedBufferCount ) <= _MaximumRetainedBuffersPerLength )
			{
			Bucket.Buffers.Add ( memory );
			return;
			}

		Interlocked.Decrement ( ref Bucket.RetainedBufferCount );
		memory.Dispose ();
		}
	}

internal sealed class AlignedMemoryLease<T> : IDisposable
	where T : unmanaged
	{
	private AlignedMemory<T>? _Memory;
	private readonly object _State;
	private readonly Action<AlignedMemory<T>, object> _Return;

	public AlignedMemory<T> Memory => _Memory ?? throw new ObjectDisposedException ( nameof ( AlignedMemoryLease<T> ) );

	internal AlignedMemoryLease ( AlignedMemory<T> memory, object state, Action<AlignedMemory<T>, object> @return )
		{
		_Memory = memory;
		_State = state;
		_Return = @return;
		}

	public void Dispose ()
		{
		var Memory = Interlocked.Exchange ( ref _Memory, null );

		if ( Memory is not null )
			_Return ( Memory, _State );
		}
	}
