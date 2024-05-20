using BenchmarkDotNet.Running;

namespace Benchmarks
	{
	internal class Program
		{
		static void Main ( string[] args )
			{
			var Summary = BenchmarkRunner.Run<BenchmarkRealFft> ();
			}
		}
	}
