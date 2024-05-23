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
using System.Numerics;
using System.Runtime.InteropServices;

namespace Aelian.FFT
	{
	/// <summary>
	/// Helper class for accessing data that can change from real-valued to complex-valued and vice versa, to support working with in-place FFT algorithms.
	/// </summary>
	public class SignalData
		{
		private Memory<double> _Data;

		/// <summary>
		/// The length of the data in real-valued samples
		/// </summary>
		public int RealLength => _Data.Length;

		/// <summary>
		/// The length of the data in complex values
		/// </summary>
		public int ComplexLength => _Data.Length >> 1;

		private SignalData ( int doubleSize )
			: this ( new Memory<double> ( new double[doubleSize] ) )
			{
			}

		private SignalData ( Memory<double> data )
			{
			_Data = data;
			}

		/// <summary>
		/// Create a SignalData instance from an existing memory instance. The data will be attached, not copied.
		/// </summary>
		/// <param name="data">The data to attach to.</param>
		/// <returns></returns>
		public static SignalData FromMemory ( Memory<double> data ) => new SignalData ( data );

		/// <summary>
		/// Create a SignalData instance that can fit a specified number of real-valued samples.
		/// </summary>
		/// <param name="realSize">The number of real-valued samples the SignalData instance should be able to fit.</param>
		/// <returns></returns>
		public static SignalData CreateFromRealSize ( int realSize ) => new SignalData ( realSize );

		/// <summary>
		/// Create a SignalData instance that can fit a specified number of complex values.
		/// </summary>
		/// <param name="complexSize">The number of complex values the SignalData instance should be able to fit.</param>
		/// <returns></returns>
		public static SignalData CreateFromComplexSize ( int complexSize ) => new SignalData ( complexSize << 1 );

		/// <summary>
		/// Get a Span pointing to the signal data, interpreted as real-valued samples.
		/// </summary>
		/// <returns>A Span pointing to the signal data, interpreted as real-valued samples.</returns>
		public Span<double> AsReal () => _Data.Span;

		/// <summary>
		/// Get a Span pointing to the signal data, interpreted as complex values.
		/// </summary>
		/// <returns>A Span pointing to the signal data, interpreted as complex values.</returns>
		public Span<Complex> AsComplex () => MemoryMarshal.Cast<double, Complex> ( _Data.Span );

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
		/// Make an exact clone of this instance of SignalData.
		/// </summary>
		/// <returns>An exact clone of this instance of SignalData.</returns>
		public SignalData Clone () => new SignalData ( _Data.ToArray () );

		/// <summary>
		/// Copy the data in this SignalData instance to another instance of SignalData. This method will overwrite the signal data in the destination instance.
		/// </summary>
		/// <param name="destination">The destination SignalData instance.</param>
		/// <exception cref="ArgumentException">The signal data lengths do not match.</exception>
		public void CopyTo ( SignalData destination )
			{
			if ( destination.RealLength != RealLength )
				throw new ArgumentException ( "Source and destination length must be equal", nameof ( destination ) );

			_Data.CopyTo ( destination._Data );
			}
		}
	}
