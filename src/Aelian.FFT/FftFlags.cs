using System;

namespace Aelian.FFT
	{
	[Flags]
	public enum FftFlags
		{
		None = 0,
		DoNotRezip = 1,
		DoNotNormalize = 2
		}
	}
