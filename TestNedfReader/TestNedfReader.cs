using System;
using System.Diagnostics;
using System.Linq;

namespace TestNedfReader
{
	internal class TestNedfReader
	{
		private static void Main(string[] args)
		{
			var datafile = args.Length > 0
				? args[0]
				: @"C:\Users\stenner-t\Desktop\raw\20180703194842_fVP02_S_T3_C_P-S_Task.nedf";
			{
				var r = new NedfFile(datafile);
				Console.WriteLine($"NEDF Version: {r.NEDFversion}");
				Console.WriteLine($"nchan: {r.nchan}/{r.channelnames.Count}, nacc: {r.nacc}");
				r.GetData(249, 5, null);
				var markers = r.GetMarkerPairs().ToList();
				Console.WriteLine($"Markers: {markers.Count}");
				if (markers.Count > 100)
				{
					Console.WriteLine("Very high number of markers found, continue?");
					Console.ReadKey();
				}
				else
				{
					markers.ForEach(m => Console.WriteLine($"Marker: {m.Item1}->{m.Item2}"));
				}

				Console.WriteLine(
					$"Positions: 0->{r.Binpos(0)}, 100->{r.Binpos(100)}, {r.nsample}->{r.Binpos(r.nsample)}");
				Console.WriteLine($"First channel first sample: {r.GetData(0, 1, null)[0]}");
				Console.WriteLine($"First channel, last sample: {r.GetData(r.nsample - 1, 1, null)[0]}");
				var watch = Stopwatch.StartNew();
				r.GetData(0, r.nsample, null);
				watch.Stop();
				Console.WriteLine($"First read time: {watch.ElapsedMilliseconds}");
				watch.Restart();
				r.GetData(0, r.nsample, null);
				watch.Stop();
				Console.WriteLine($"Second read time: {watch.ElapsedMilliseconds}");
			}
			Console.ReadKey();
		}
	}
}