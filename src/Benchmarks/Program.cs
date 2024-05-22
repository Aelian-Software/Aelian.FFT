using BenchmarkDotNet.Running;

namespace Benchmarks
	{
	internal class Program
		{
		static void Main ( string[] args )
			{
			//var SummaryReal = BenchmarkRunner.Run<BenchmarkRealFft> ();
			var SummaryComplex = BenchmarkRunner.Run<BenchmarkComplexFft> ();
			}
		}
	}
