using System;
using System.Diagnostics;
using System.Linq;
using BrainVision.Analyzer.Readers;

namespace TestNedfReader
{
	internal class TestNedfReader
	{
		private static void Main(string[] args)
		{
			uint samples;
			var datafile = args.Length > 0
				? args[0]
				: @"C:\Users\stenner-t\Desktop\raw\20180703194842_fVP02_S_T3_C_P-S_Task.nedf";
			{
				var r = new NedfFile(datafile);
				samples = r.nsample;
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

				//markers.ForEach(m=>Console.WriteLine($"Marker: {m.Item1}->{m.Item2}"));
				Console.WriteLine($"Positions: 0->{r.Binpos(0)}, 100->{r.Binpos(100)}, {samples}->{r.Binpos(samples)}");
				var watch = Stopwatch.StartNew();
				r.GetData(0, samples, null);
				watch.Stop();
				Console.WriteLine($"First read time: {watch.ElapsedMilliseconds}");
				watch.Restart();
				r.GetData(0, samples, null);
				watch.Stop();
				Console.WriteLine($"Second read time: {watch.ElapsedMilliseconds}");
			}
			{
				var reader = new NedfDataReader();
				Console.WriteLine($"File is ok: {reader.OpenRawFile(datafile)}");
				Console.WriteLine($"First channel first sample: {reader.GetData(0, 1, null)[0]}");
				Console.WriteLine($"First channel, last sample: {reader.GetData(samples - 1, 1, null)[0]}");
			}
			Console.ReadKey();
		}
	}
}
