using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Globalization;
using System.IO;
using System.Linq;
using NedfReader;

class Program
{
	static void Main(string[] args)
	{
		var filesArg = new Argument<FileInfo[]>("files", "NEDF files to process").ExistingOnly();
		filesArg.Arity = ArgumentArity.OneOrMore;
		var rootCommand = new RootCommand("Export header / marker data for NEDF files")
		{
			new Option<StreamWriter>(new string[]{"--statsfile", "-o" }, "File to save stats to instead of stdout"),
			new Option<StreamWriter>("--errlog", "File to log errors to instead of stderr"),
			new Option<uint>("--maxsamples", ()=>int.MaxValue, "Quit after this many samples, useful for sanity checks"),
			new Option<bool>("--markercsv", "Write a CSV file with markers for each supplied nedf file"),
			filesArg
		};
		rootCommand.Handler = CommandHandler.Create((StreamWriter statsfile, StreamWriter errlog, uint maxsamples, bool markercsv, FileInfo[] files) =>
		{
			var stderr = errlog ?? Console.Error;
			var statsout = statsfile ?? Console.Out;

			statsout.WriteLine("File;NEDFversion;nchan;nacc;nsample;nstim;StartDate_firstEEGTimestamp");
			foreach (var file in files)
				try
				{
					using var r = new NedfFile(file.FullName, (str) => errlog.WriteLine(str));
					string basename = Path.GetFileNameWithoutExtension(file.Name);
					int markercount = 0;
					if (markercsv)
					{
						var outfile = markercsv ? new StreamWriter(file.DirectoryName + '/' + basename + "_markers.csv") : null;
						outfile?.WriteLine("sample;marker");
						foreach ((uint pos, uint value) in r.GetMarkerPairs(maxsamples))
						{
							outfile?.WriteLine($"{pos};{value}");
							markercount++;
						}
						outfile?.Dispose();
					}
					CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
					statsout.WriteLine(string.Join(";", new object[]{
						Path.GetFileName(file.Name),
						r.NEDFversion,
						r.NChan,
						r.NAcc,
						r.NSample,
						markercount,
						r.hdr.StepDetails.StartDate_firstEEGTimestamp
					}.Select(obj=>obj.ToString())));
				}
				catch (Exception e)
				{
					Console.Error.WriteLine($"{file}: Error: {e.Message}");
				}
		});
		rootCommand.Invoke(args);
	}
}
