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

namespace Aelian.FFT
	{
	/// <summary>
	/// Base class for disposable classes
	/// </summary>
	/// <typeparam name="T">The class that needs to be disposable</typeparam>
	public abstract class Disposable<T> : IDisposable
		where T : Disposable<T>
		{
		public bool IsDisposed { get; private set; }

		protected virtual void Dispose ( bool disposing )
			{
			if ( !IsDisposed )
				{
				if ( disposing )
					DisposeManagedState ();

				FreeUnmanagedResources ();
				NullLargeFields ();

				IsDisposed = true;
				}
			}

		/// <summary>
		/// Dispose managed state (managed objects)
		/// </summary>
		protected virtual void DisposeManagedState () { }

		/// <summary>
		/// Free unmanaged resources (unmanaged objects)
		/// </summary>
		protected virtual void FreeUnmanagedResources () { }

		/// <summary>
		/// Set large fields to null
		/// </summary>
		protected virtual void NullLargeFields () { }

		~Disposable ()
			{
			Dispose ( disposing: false );
			}

		public void Dispose ()
			{
			Dispose ( disposing: true );
			GC.SuppressFinalize ( this );
			}
		}
	}
