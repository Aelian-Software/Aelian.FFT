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
using System.Numerics;
using System.Runtime.InteropServices;

namespace Aelian.FFT
	{
	/// <summary>
	/// Helper class for allocating and accessing data that can change from real-valued to 
	/// complex-valued and vice versa, to support working with in-place FFT algorithms.
	/// The data is guaranteed to be aligned to a memory boundary of 64 bytes for optimal
	/// SIMD performance.
	/// </summary>
	/// <remarks>
	/// You always need to dispose instances of this class after use or it will leak memory!
	/// </remarks>
	public unsafe class AlignedSignalData : Disposable<AlignedSignalData>
		{
		private const nuint _MemoryAlignmentBoundaryBytes = 64;
		private void* _DataPointer;
		private int _DoubleLength;

		/// <summary>
		/// The length of the data in real-valued samples
		/// </summary>
		public int RealLength => _DoubleLength;

		/// <summary>
		/// The length of the data in complex values
		/// </summary>
		public int ComplexLength => _DoubleLength >> 1;

		/// <summary>
		/// The length of the data in bytes
		/// </summary>
		public int ByteLength => _DoubleLength * sizeof ( double );

		private AlignedSignalData ( int doubleLength )
			{
			_DoubleLength = doubleLength;

			var ByteLength = sizeof ( double ) * _DoubleLength;
			_DataPointer = NativeMemory.AlignedAlloc ( (nuint) ByteLength, _MemoryAlignmentBoundaryBytes );
			}

		/// <summary>
		/// Create a AlignedSignalData instance that can fit a specified number of real-valued samples.
		/// </summary>
		/// <param name="realSize">The number of real-valued samples the AlignedSignalData instance should be able to fit.</param>
		/// <returns></returns>
		public static AlignedSignalData AllocateFromRealSize ( int realSize ) => new AlignedSignalData ( realSize );

		/// <summary>
		/// Create a AlignedSignalData instance that can fit a specified number of complex values.
		/// </summary>
		/// <param name="complexSize">The number of complex values the AlignedSignalData instance should be able to fit.</param>
		/// <returns></returns>
		public static AlignedSignalData AllocateFromComplexSize ( int complexSize ) => new AlignedSignalData ( complexSize << 1 );

		/// <summary>
		/// Get a Span pointing to the raw signal data bytes
		/// </summary>
		/// <returns>A Span pointing to the raw signal data bytes.</returns>
		public Span<byte> AsRawData () => new Span<byte> ( _DataPointer, ByteLength );

		/// <summary>
		/// Get a Span pointing to the signal data, interpreted as real-valued samples.
		/// </summary>
		/// <returns>A Span pointing to the signal data, interpreted as real-valued samples.</returns>
		public Span<double> AsReal () => new Span<double> ( _DataPointer, RealLength );

		/// <summary>
		/// Get a Span pointing to the signal data, interpreted as complex values.
		/// </summary>
		/// <returns>A Span pointing to the signal data, interpreted as complex values.</returns>
		public Span<Complex> AsComplex () => new Span<Complex> ( _DataPointer, ComplexLength );

		/// <summary>
		/// Copy the signal data to an array of real-valued samples.
		/// </summary>
		/// <returns>An array of real-valued samples.</returns>
		public double[] ToRealArray () => AsReal ().ToArray ();

		/// <summary>
		/// Copy the signal data to an array of complex values.
		/// </summary>
		/// <returns>An array of complex values.</returns>
		public Complex[] ToComplexArray () => AsComplex ().ToArray ();

		/// <summary>
		/// Make an exact clone of this instance of AlignedSignalData.
		/// </summary>
		/// <returns>An exact clone of this instance of AlignedSignalData.</returns>
		public AlignedSignalData Clone ()
			{
			var Out = new AlignedSignalData ( _DoubleLength );
			CopyTo ( Out );
			return Out;
			}

		/// <summary>
		/// Copy the data in this AlignedSignalData instance to another instance of AlignedSignalData. This method will overwrite the signal data in the destination instance.
		/// </summary>
		/// <param name="destination">The destination AlignedSignalData instance.</param>
		/// <exception cref="ArgumentException">The signal data lengths do not match.</exception>
		public void CopyTo ( AlignedSignalData destination )
			{
			if ( destination.ByteLength != ByteLength )
				throw new ArgumentException ( "Source and destination length must be equal", nameof ( destination ) );

			AsRawData ().CopyTo ( destination.AsRawData () );
			}

		protected override void FreeUnmanagedResources ()
			{
			NativeMemory.AlignedFree ( _DataPointer );
			_DataPointer = null;
			}
		}
	}
