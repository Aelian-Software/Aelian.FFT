using System.Numerics;

namespace Tests
	{
	public class FftTestFixture : IDisposable
		{
		private const int InputSize = 2048;
		public Complex[] ComplexInputData { get; }
		public double[] RealInputData { get; }

		public FftTestFixture ()
			{
			var Rnd = new Random ( 20242024 );
			ComplexInputData = new Complex[InputSize];
			RealInputData = new double[InputSize * 2];

			for ( int i = 0; i < InputSize; i++ )
				{
				ComplexInputData[i] = new Complex
					(
					Rnd.NextDouble () * 2.0 - 1.0,
					Rnd.NextDouble () * 2.0 - 1.0
					);
				}

			for ( int i = 0; i < InputSize * 2; i++ )
				RealInputData[i] = Rnd.NextDouble () * 2.0 - 1.0;

			Aelian.FFT.FastFourierTransform.Initialize ();
			}

		public void Dispose ()
			{
			}
		}
	}