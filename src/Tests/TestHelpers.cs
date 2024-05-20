using System.Numerics;

using Xunit.Abstractions;

namespace Tests
	{
	internal static class TestHelpers
		{
		public static void OutputComplexNumbers ( IReadOnlyList<Complex> numbers, ITestOutputHelper output )
			{
			foreach ( var Number in numbers )
				output.WriteLine ( $"\t{Number}" );
			}

		public static void AssertEqual ( IReadOnlyList<Complex> expected, IReadOnlyList<Complex> actual, int precision )
			{
			Assert.Equal ( expected.Count, actual.Count );

			for ( int i = 0; i < expected.Count; i++ )
				AssertEqual ( expected[i], actual[i], precision );
			}

		public static void AssertEqual ( IReadOnlyList<double> expected, IReadOnlyList<double> actual, int precision )
			{
			Assert.Equal ( expected.Count, actual.Count );

			for ( int i = 0; i < expected.Count; i++ )
				Assert.Equal ( expected[i], actual[i], precision );
			}

		public static void AssertEqual ( Complex expected, Complex actual, int precision )
			{
			Assert.Equal ( expected.Real, actual.Real, precision );
			Assert.Equal ( expected.Imaginary, actual.Imaginary, precision );
			}
		}
	}
