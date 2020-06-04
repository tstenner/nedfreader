using System;
using System.IO;
using NedfReader;

internal class NedfExportMarkers
{
	private static void Main(string[] args)
	{
		foreach (var file in args)
			try
			{
				Action<string> OnErr = Console.Error.WriteLine;
				Console.WriteLine(file, OnErr);
				using var r = new NedfFile(file);
				var basename = Path.GetFileNameWithoutExtension(file);
				using var outfile = new StreamWriter(basename + "_markers.csv");
				outfile.WriteLine("sample;marker");
				foreach (var (pos, value) in r.GetMarkerPairs())
				{
					outfile.Write(pos);
					outfile.Write(';');
					outfile.WriteLine(value);
				}
				outfile.Close();
			}
			catch (Exception e)
			{
				Console.Error.WriteLine($"{file}: Error: {e.Message}");
			}
		Console.WriteLine("Done, press enter to exit");
		Console.ReadLine();
	}
}
