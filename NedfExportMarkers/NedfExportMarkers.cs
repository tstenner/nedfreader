using System;
using System.IO;

internal class NedfExportMarkers
{
	private static void Main(string[] args)
	{
		foreach (var file in args)
			try
			{
				Console.WriteLine(file);
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
				Console.WriteLine($"{file}: Error: {e.Message}");
			}
		Console.WriteLine("Done, press enter to exit");
		Console.ReadLine();
	}
}
