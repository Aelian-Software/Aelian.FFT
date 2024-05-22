using System.Numerics;
using System.Runtime.InteropServices;

using Xunit.Abstractions;

namespace Tests
	{
	public class ValidateOutputTests : IClassFixture<FftTestFixture>
		{
		private const int AssertPrecision = 7;
		private readonly ITestOutputHelper _Output;
		private readonly FftTestFixture _Fixture;

		public ValidateOutputTests ( ITestOutputHelper output, FftTestFixture fixture )
			{
			_Output = output;
			_Fixture = fixture;
			}

		[Fact]
		public void TestComplexAgainstLomont ()
			{
			var ComplexValues = _Fixture.ComplexInputData;

			// Aelian forward FFT

			var AelianBuffer = (Complex[]) ComplexValues.Clone ();
			Aelian.FFT.FastFourierTransform.FFT ( AelianBuffer, true );

			// Lomont forward FFT

			var Lomont = new Lomont.LomontFFT () { A = 1, B = -1 };
			var LomontBuffer = (Complex[]) ComplexValues.Clone ();
			var InterleavedLomontBuffer = MemoryMarshal.Cast<Complex, double> ( LomontBuffer );
			Lomont.FFT ( InterleavedLomontBuffer, true );

			// Verify that forward transform yielded equal (or close enough) values

			TestHelpers.AssertEqual ( LomontBuffer, AelianBuffer, AssertPrecision );

			// Inverse FFT

			Aelian.FFT.FastFourierTransform.FFT ( AelianBuffer, false );
			Lomont.FFT ( InterleavedLomontBuffer, false );

			// Verify that inverse transform yielded equal (or close enough) values

			TestHelpers.AssertEqual ( LomontBuffer, AelianBuffer, AssertPrecision );
			}

		[Fact]
		public void TestRealAgainstLomont ()
			{
			var RealValues = _Fixture.RealInputData;

			// Aelian forward FFT

			var AelianBuffer = (double[]) RealValues.Clone ();
			Aelian.FFT.FastFourierTransform.RealFFT ( AelianBuffer, true );

			// Lomont forward FFT

			var Lomont = new Lomont.LomontFFT () { A = 1, B = -1 };
			var LomontBuffer = (double[]) RealValues.Clone ();
			Lomont.RealFFT ( LomontBuffer, true );

			// Verify that forward transform yielded equal (or close enough) values

			TestHelpers.AssertEqual ( LomontBuffer, AelianBuffer, AssertPrecision );

			// Inverse FFT

			Aelian.FFT.FastFourierTransform.RealFFT ( AelianBuffer, false );
			Lomont.RealFFT ( LomontBuffer, false );

			// Verify that inverse transform yielded equal (or close enough) values

			TestHelpers.AssertEqual ( LomontBuffer, AelianBuffer, AssertPrecision );
			}
		}
	}