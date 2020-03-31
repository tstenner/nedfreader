using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

public sealed class NedfFile : IDisposable
{
	public readonly string headerxml;
	private readonly FileStream infile;
	public readonly uint nchan, nsamplestimpereeg, nacc, sfreq, nsample;
	public readonly decimal NEDFversion;
	public List<string> Channelnames { get; }

	public NedfFile(string dataFile)
	{
		if (dataFile == null) throw new ArgumentNullException(nameof(dataFile));
		var fs = new FileStream(dataFile, FileMode.Open, FileAccess.Read);
		using (var reader = new BinaryReader(fs))
			headerxml = Encoding.UTF8.GetString(reader.ReadBytes(10240).TakeWhile(x => x != 0).ToArray());

		if (headerxml[0] != '<')
			throw new ArgumentException($"'{dataFile}' does not begin with an xml header");

		// The XML 1.0 spec contains a list of valid characters for tag names:
		// https://www.w3.org/TR/2008/REC-xml-20081126/#NT-NameChar
		// Older NIC versions allowed any character in the root tag name, so we have to sanitize the "xml" to avoid parse errors
		var res = Regex.Match(headerxml, @"^\s*<([^ ]+?)[^>]*>(.+)</\1>\s*$", RegexOptions.Singleline);
		if (!res.Success)
			throw new ArgumentException("The XML header could not be recognized");
		headerxml = "<valid_xml_tag>" + res.Groups[2] + "</valid_xml_tag>";

		var indoc = new XmlDocument();
		indoc.LoadXml(headerxml);
		var el = indoc.DocumentElement;
		Trace.Assert(el != null, nameof(el) + " != null");
		NEDFversion = decimal.Parse(el.SelectSingleNode("NEDFversion")?.InnerText ?? "0",
			NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

		Trace.Assert(NEDFversion >= 1.3m || NEDFversion <= 1.4m,
			$"Untested NEDFversion {NEDFversion} in {dataFile}, proceed at your own risk.");
		var addchanstat = el.SelectSingleNode("AdditionalChannelStatus")?.InnerText ?? "OFF";
		if (addchanstat != "OFF")
			throw new ArgumentException($"Unexpected value for AdditionalChannelStatus: {addchanstat}");

		var eegset = el.SelectSingleNode("EEGSettings");
		var channels = eegset?.SelectSingleNode("EEGMontage")?.ChildNodes;
		Trace.Assert(channels != null, "No channels found");
		Channelnames = channels.Cast<XmlNode>().Select(node => node.InnerText).ToList();

		nacc = ParseXmlUInt(el, "NumberOfChannelsOfAccelerometer");
		nchan = ParseXmlUInt(eegset, "TotalNumberOfChannels");
		sfreq = ParseXmlUInt(eegset, "EEGSamplingRate");
		var eegunits = eegset.SelectSingleNode("EEGUnits")?.InnerText ?? "nV";
		if (eegunits != "nV")
			throw new ArgumentException($"Unknown EEG unit {eegunits}");

		nsample = ParseXmlUInt(eegset, "NumberOfRecordsOfEEG");

		if (el.SelectSingleNode("STIMSettings") != null)
		{
			var node = el.SelectSingleNode("STIMSettings");
			var nstim = ParseXmlUInt(node, "NumberOfStimulationChannels");
			Trace.Assert(
				nstim != 0 || (el.SelectSingleNode("StepDetails/SoftwareVersion")?.InnerText ?? "") != "NIC v2.0.8",
				"Data files recorded by NIC 2.0.8 with stimulation channels are broken, proceed at your own risk!");
			var numstimrec = ParseXmlUInt(node, "NumberOfRecordsOfStimulation");
			Trace.Assert(numstimrec >= 2 * nsample,
				$"Can't handle intermittent stimulation data {numstimrec} records, expected {2 * nsample}");
			var stimsrate = ParseXmlUInt(node, "StimulationSamplingRate");
			Trace.Assert(stimsrate == 1000, $"Unexpected stimulation sampling rate ({stimsrate}!=1000)");
			nsamplestimpereeg = 2;
		}

		Trace.Assert(ParseXmlUInt(eegset, "NumberOfPacketsLost") == 0,
			"Packets were lost while recording, aborting...");
		Trace.Assert(ParseXmlUInt(el, "AccelerometerSamplingRate") == 100, "Unexpected sampling rate of accelerometer");

		if ((el.SelectSingleNode("stepDetails/DeviceClass")?.InnerText ?? "") == "STARSTIM")
			throw new ArgumentException("Found Starstim, not sure how to handle this");

		infile = new FileStream(dataFile, FileMode.Open, FileAccess.Read);
	}

	// Normally, it should be nchaneeg + nsamplestimpereeg*nstim, but it's not.
	public uint Samplesize() => (1 + nsamplestimpereeg) * nchan * 3 + 4;
	public uint Chunkfrontlength() => nacc * 2;
	public uint Chunksize() => Samplesize() * 5 + Chunkfrontlength();

	private static uint ParseXmlUInt(XmlNode node, string xpath)
	{
		var val = node.SelectSingleNode(xpath);
		Trace.Assert(val != null, $"Header field {xpath} not found");
		var res = uint.Parse(val.InnerText, CultureInfo.InvariantCulture);
		return res;
	}

	public uint Binpos(uint sample) =>
		10240 // header
		+ Chunkfrontlength()
		+ sample / 5 * Chunksize()
		+ sample % 5 * Samplesize();

	public float[] GetData(uint startsample, uint length, int[] channelList)
	{
		if (startsample + length > nsample) throw new ArgumentOutOfRangeException(nameof(startsample));
		channelList ??= Enumerable.Range(0, (int)nchan).ToArray();
		if (channelList.Any(i => i >= nchan || i < 0))
			throw new ArgumentOutOfRangeException(nameof(channelList));
		var res = new float[channelList.Length * length];

		const double multiplier = 2400000 / (6.0 * 8388607);
		infile.Seek(Binpos(startsample), SeekOrigin.Begin);
		var chanlen = channelList.Length;
		var buffer = new byte[Samplesize()];
		for (var sample = 0; sample < length; sample++)
		{
			if (sample > 0 && startsample % 5 == 0) infile.Seek(Chunkfrontlength(), SeekOrigin.Current);
			infile.Read(buffer, 0, (int)Samplesize());
			for (var i = 0; i < chanlen; i++)
			{
				var off = 3 * channelList[i];
				var raw = (buffer[off] << 16) + (buffer[off + 1] << 8) + buffer[off + 2];
				if ((buffer[off] & (1 << 7)) != 0) raw -= 1 << 24;
				res[sample * chanlen + i] = raw == -1 ? raw : (float)(raw * multiplier);
			}

			startsample++;
		}

		return res;
	}

	public IEnumerable<(uint, uint)> GetMarkerPairs(uint maxsample = int.MaxValue)
	{
		infile.Seek(Binpos(0) - Chunkfrontlength(), SeekOrigin.Begin);
		var markers = new List<ValueTuple<uint, uint>>();

		var buffer = new byte[4];
		if (maxsample > nsample) maxsample = nsample;
		for (uint i = 0; i < maxsample; i++)
		{
			if (i % 5 == 0) infile.Seek(Chunkfrontlength(), SeekOrigin.Current);
			infile.Seek(Samplesize() - 4, SeekOrigin.Current);
			infile.Read(buffer, 0, 4);
			var t = buffer[3] | (buffer[2] << 8) | (buffer[1] << 16) | (buffer[0] << 24);
			if (t != 0)
				markers.Add(new ValueTuple<uint, uint>(i, (uint)t));
		}

		var nmarker = markers.Count;
		Trace.Assert(nmarker < nsample / 100,
			$"Unexpected high number of triggers found ({nmarker}), this could indicate a broken file ({infile.Name}).");
		return markers;
	}

	public void Dispose() => infile.Dispose();
}
