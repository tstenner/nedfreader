using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace NedfReader
{
	[Serializable]
	public sealed class StepDetails
	{
		public string SoftwareVersion, DeviceClass, StepName;
		public ulong StartDate_firstEEGTimestamp;
	}
	[Serializable]
	public sealed class EEGSettings
	{
		public uint TotalNumberOfChannels, EEGSamplingRate, NumberOfRecordsOfEEG, NumberOfPacketsLost;
		[XmlElement(IsNullable = true)]
		public string EEGUnits;
		public EEGMontage EEGMontage;
	}
	[Serializable]
	public sealed class STIMSettings
	{
		public uint NumberOfStimulationChannels, NumberOfRecordsOfStimulation, StimulationSamplingRate;
		public StepDetails StepDetails;
	}
	[Serializable]
	public sealed class EEGMontage : IXmlSerializable
	{

		[XmlAnyElement]
		public List<string> channelnames = new List<string>();

		public XmlSchema GetSchema() => null;

		public void ReadXml(XmlReader reader)
		{
			if (reader is null)
				throw new ArgumentNullException(nameof(reader));
			while (reader.Read() && (reader.NodeType != XmlNodeType.EndElement || reader.Name != "EEGMontage"))
				if (reader.NodeType == XmlNodeType.Text)
					channelnames.Add(reader.Value);
			reader.Read();
		}

		public void WriteXml(XmlWriter writer)
		{
		}
	}
	[Serializable]
	[XmlRoot("valid_xml_tag")]
	public sealed class NedfHeader
	{
		public decimal NEDFversion;
		public string AdditionalChannelStatus, AccelerometerUnits;
		public uint NumberOfChannelsOfAccelerometer, AccelerometerSamplingRate;
		public EEGSettings EEGSettings;
		public STIMSettings STIMSettings;
		public StepDetails StepDetails;
	}

	public sealed class NedfFile : IDisposable
	{
		public readonly NedfHeader hdr;
		public readonly string headerxml;
		private readonly FileStream infile;
		public readonly uint NSampleStimPerEEG;
		public uint NSample => hdr.EEGSettings.NumberOfRecordsOfEEG;
		public uint SFreq => hdr.EEGSettings.EEGSamplingRate;
		public uint NChan => hdr.EEGSettings.TotalNumberOfChannels;
		public uint NAcc => hdr.NumberOfChannelsOfAccelerometer;
		public readonly decimal NEDFversion;
		public List<string> Channelnames => hdr.EEGSettings.EEGMontage.channelnames;

		private readonly Action<string> SuspiciousFileCallback;

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters")]
		public NedfFile(string dataFile, Action<string> suspiciousFileCallback = null)
		{
			SuspiciousFileCallback = suspiciousFileCallback;
			if (dataFile is null)
				throw new ArgumentNullException(nameof(dataFile));
			infile = new FileStream(dataFile, FileMode.Open, FileAccess.Read);
			headerxml = Encoding.UTF8.GetString(new BinaryReader(infile).ReadBytes(10240)).Trim('\0');

			if (headerxml[0] != '<')
				throw new ArgumentException($"'{dataFile}' does not begin with an xml header");

			// The XML 1.0 spec contains a list of valid characters for tag names:
			// https://www.w3.org/TR/2008/REC-xml-20081126/#NT-NameChar
			// Older NIC versions allowed any character in the root tag name, so we have to sanitize the "xml" to avoid parse errors
			var res = Regex.Match(headerxml, @"^\s*<([^ ]+?)[^>]*>(.+)</\1>\s*$", RegexOptions.Singleline);
			if (!res.Success)
				throw new ArgumentException("The XML header could not be recognized");
			headerxml = "<valid_xml_tag>" + res.Groups[2] + "</valid_xml_tag>";

			using (var reader = XmlReader.Create(new StringReader(headerxml)))
				hdr = (NedfHeader)new XmlSerializer(typeof(NedfHeader)).Deserialize(reader);

			NEDFversion = hdr.NEDFversion;
			if (NEDFversion < 1.3m || NEDFversion > 1.4m)
				SuspiciousFileCallback?.Invoke($"Untested NEDFversion {NEDFversion} in {dataFile}, proceed at your own risk.");
			if ((hdr.AdditionalChannelStatus ?? "OFF") != "OFF")
				throw new ArgumentException($"Unexpected value for AdditionalChannelStatus: {hdr.AdditionalChannelStatus}");

			string eegunits = hdr.EEGSettings.EEGUnits ?? "nV";
			if (eegunits != "nV")
				throw new ArgumentException($"Unknown EEG unit {eegunits??"null"}");

			var node = hdr.STIMSettings;
			if (!(node is null))
			{
				if (node.NumberOfStimulationChannels != 0 && hdr.StepDetails.SoftwareVersion == "NIC v2.0.8")
					SuspiciousFileCallback?.Invoke("Data files recorded by NIC 2.0.8 with stimulation channels are broken, proceed at your own risk!");
				uint numstimrec = node.NumberOfRecordsOfStimulation;
				if (numstimrec < 2 * NSample)
					throw new Exception($"Can't handle intermittent stimulation data {numstimrec} records, expected {2 * NSample}");
				uint stimsrate = node.StimulationSamplingRate;
				if (stimsrate != 1000)
					throw new Exception($"Unexpected stimulation sampling rate ({stimsrate}!=1000)");
				NSampleStimPerEEG = 2;
			}

			if (hdr.EEGSettings.NumberOfPacketsLost > 0)
				throw new Exception("Packets were lost while recording, aborting...");
			if (hdr.AccelerometerSamplingRate != 100)
				throw new Exception("Unexpected sampling rate of accelerometer");

			if (hdr.StepDetails.DeviceClass == "STARSTIM")
				SuspiciousFileCallback?.Invoke("Found Starstim, not sure how to handle this");
		}

		// Normally, it should be nchaneeg + nsamplestimpereeg*nstim, but it's not.
		public uint Samplesize() => (1 + NSampleStimPerEEG) * NChan * 3 + 4;
		public uint Chunkfrontlength() => NAcc * 2;
		public uint Chunksize() => Samplesize() * 5 + Chunkfrontlength();

		public uint Binpos(uint sample) =>
			10240 // header
			+ Chunkfrontlength()
			+ sample / 5 * Chunksize()
			+ sample % 5 * Samplesize();

		public float[] GetData(uint startsample, uint length, int[] channelList)
		{
			if (startsample + length > NSample)
				throw new ArgumentOutOfRangeException(nameof(startsample));
			channelList ??= Enumerable.Range(0, (int)NChan).ToArray();
			if (channelList.Any(i => i >= NChan || i < 0))
				throw new ArgumentOutOfRangeException(nameof(channelList));
			var res = new float[channelList.Length * length];

			const double multiplier = 2400000 / (6.0 * 8388607);
			infile.Seek(Binpos(startsample), SeekOrigin.Begin);
			int chanlen = channelList.Length;
			var buffer = new byte[Samplesize()];
			for (int sample = 0; sample < length; sample++)
			{
				if (sample > 0 && startsample % 5 == 0)
					infile.Seek(Chunkfrontlength(), SeekOrigin.Current);
				infile.Read(buffer, 0, (int)Samplesize());
				for (int i = 0; i < chanlen; i++)
				{
					int off = 3 * channelList[i];
					int raw = (buffer[off] << 16) + (buffer[off + 1] << 8) + buffer[off + 2];
					if ((buffer[off] & (1 << 7)) != 0)
						raw -= 1 << 24;
					res[sample * chanlen + i] = raw == -1 ? raw : (float)(raw * multiplier);
				}

				startsample++;
			}

			return res;
		}

		public IEnumerable<(uint, uint)> GetMarkerPairs(uint maxsample = int.MaxValue)
		{
			infile.Seek(Binpos(0) - Chunkfrontlength(), SeekOrigin.Begin);

			uint consecutive = 0, maxconsecutive = 0;
			var buffer = new byte[4];
			if (maxsample > NSample)
				maxsample = NSample;
			for (uint i = 0; i < maxsample; i++)
			{
				if (i % 5 == 0)
					infile.Seek(Chunkfrontlength(), SeekOrigin.Current);
				infile.Seek(Samplesize() - 4, SeekOrigin.Current);
				infile.Read(buffer, 0, 4);
				int t = buffer[3] | (buffer[2] << 8) | (buffer[1] << 16) | (buffer[0] << 24);
				if (t != 0)
				{
					yield return new ValueTuple<uint, uint>(i, (uint)t);
					consecutive++;
				}
				else
				{
					/* This ignores consecutive markers at the end of the file as
					there's no sample without marker.
					However, some nedf files suddenly have no markers anymore and
					then thousands of markers at the end. The EEG data can be read
					correctly and both NIC and the EEGLAB plugin read the same data
					so this is not considered a bug.
					*/
					maxconsecutive = consecutive > maxconsecutive ? consecutive : maxconsecutive;
					consecutive = 0;
				}
			}

			if (maxconsecutive > 5)
				SuspiciousFileCallback?.Invoke($"Unexpected trigger density found ({maxconsecutive} consecutive, {consecutive} at the end), this could indicate a broken file ({infile.Name}).");
			yield break;
		}

		public void Dispose() => infile.Dispose();
	}

}
