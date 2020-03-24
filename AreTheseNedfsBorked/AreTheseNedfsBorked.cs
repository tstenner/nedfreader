using System;
using System.IO;
using System.Linq;

namespace AreTheseNedfsBorked
{
	internal class AreTheseNedfsBorked
	{
		private static void Main(string[] args)
		{
			Action<string> w = Console.WriteLine;
			foreach (var file in args)
				try
				{
					using var r = new NedfFile(file);
					w($"File: {Path.GetFileName(file)}");
					w($"NEDF Version: {r.NEDFversion}");
					w($"nchan: {r.nchan}/{r.Channelnames.Count}, nacc: {r.nacc}");
					w("Scanning first 1000 samples...");
					w($"Markers: {r.GetMarkerPairs(1000).Count()}");
					w("");
				}
				catch (Exception e)
				{
					w($"Error: {e.Message}");
				}

			Console.ReadKey();
		}
	}
}
