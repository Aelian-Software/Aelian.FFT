using BenchmarkDotNet.Running;

namespace Benchmarks
	{
	internal class Program
		{
		static void Main ( string[] args )
			{
#if !DEBUG
			var SummaryReal = BenchmarkRunner.Run<BenchmarkRealFft> ();
			var SummaryComplex = BenchmarkRunner.Run<BenchmarkComplexFft> ();
#else
			var Profiling = new Profiling ( 4096 );
			Profiling.Setup ();
			Profiling.Run ();
#endif
			}
		}
	}
