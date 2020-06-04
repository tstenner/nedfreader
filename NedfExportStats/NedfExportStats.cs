using System;
using System.IO;
using System.Linq;
using NedfReader;

internal class NedfExportStats
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "<Pending>")]
	private static void Main(string[] args)
	{
		Action<string> w = Console.WriteLine;
		if (args.Length == 0)
			w("No files submitted as arguments...");
		else
			w($"File;NEDFversion;nchan;nacc;nsample;nstim1k;StartDate_firstEEGTimestamp");

		foreach (string file in args)
			try
			{
				using var r = new NedfFile(file, (str) => Console.Error.WriteLine(file + ": " + str));
				w(string.Join(";", new string[]{
						Path.GetFileName(file),
						r.NEDFversion.ToString(),
						r.NChan.ToString(),
						r.NAcc.ToString(),
						r.NSample.ToString(),
						r.GetMarkerPairs(1000).Count().ToString(),
						r.hdr.StepDetails.StartDate_firstEEGTimestamp.ToString()
					}));
			}
			catch (Exception e)
			{
				Console.Error.WriteLine(file);
				Console.Error.WriteLine(e.Message);
				Console.Error.WriteLine(e.StackTrace);
				Console.Error.WriteLine();
			}
		try
		{
			Console.ReadKey();
		}
		catch (System.InvalidOperationException) { }

	}
}
