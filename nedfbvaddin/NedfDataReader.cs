using System;
using System.IO;
using System.Linq;
using BrainVision.Interfaces;
using BrainVision.Support;

namespace BrainVision.Analyzer.Readers
{
	[Reader(Guid, "NEDF Data Reader", "NEDF Data Reader", 0, 1000000)]
	public class NedfDataReader : IEEGRawFileReader, IEEGData, IDisposable
	{
		public const string Guid = "9df2871b-278a-47c4-bd41-0582d874daa9";
		private string dataFile;
		private IEEGProperties eegProperties;
		private IEEGStorage eegStorage;

		public void Dispose() { }

		public void Init(IStructuredStorage storage, IEEGData parent)
		{
			try
			{
				eegStorage = storage as IEEGStorage;
				dataFile = storage.GetStreamText("DataPath");
				eegProperties = GetProperties();
			}
			catch (Exception ex)
			{
				throw new AnalyzerException("NedfDataReader::Init", "Reader error", ex.Message, ex);
			}
		}

		public IEEGProperties GetProperties()
		{
			if (eegStorage == null)
				return null;
			return eegProperties = eegProperties ?? ComponentFactory.CreateProperties(eegStorage);
		}

		public float[] GetData(uint nPosition, uint nPoints, int[] channelList)
		{
			using (var f = new NedfFile(dataFile))
			{
				return f.GetData(nPosition, nPoints, channelList);
			}
		}

		public IEEGMarker[] GetMarkers(uint nPosition, uint nPoints) => eegStorage?.GetMarkers(nPosition, nPoints);

		public IEEGMarker GetMarker(EEGMarkerIndex markerIndex) => eegStorage?.GetMarker(markerIndex);

		public EEGMarkerIndex[] GetMarkerIndexTable() => eegStorage?.GetMarkerIndexTable();

		public IStructuredStorage GetStorage() => eegStorage;

		public int GetTransientDataOffset() => 0;

		public int OpenRawFile(string sFilename)
		{
			if (string.IsNullOrEmpty(sFilename) || !File.Exists(sFilename) || Path.GetExtension(sFilename) != ".nedf")
				return -1;
			dataFile = sFilename;
			try
			{
				using (new NedfFile(dataFile)) { }
			}
			catch (Exception e)
			{
				MessageDisplay.ShowError("NedfDataReader", "Error reading file", e.Message);
				return -1;
			}

			return 1;
		}


		void IEEGRawFileReader.CreateStorageEntries(
			IStructuredStorage storage,
			int nSequence)
		{
			try
			{
				var props = ComponentFactory.CreateChangeProperties();
				using (var file = new NedfFile(dataFile))
				{
					var dataName = "Tristans Raw Data";
					var markers = file.GetMarkerPairs().ToList();
					if (markers.Count > file.nsample / 10)
						dataName = "Corrupt Data File";
					else
						ComponentFactory.SaveChangedMarkers(storage, markers.Select(pair =>
						{
							var m = ComponentFactory.CreateChangeMarker();
							m.Channel = -1;
							(var pos, var val) = pair;
							m.Position = pos;
							m.Points = 1;
							m.Type = "Stimulus";
							m.Visible = true;
							m.Description = val.ToString();
							return m;
						}).ToList());

					var coords = ElectrodeCoordinates.GetDefaultCoordinates();
					props.Channels.AddRange(file.channelnames.Select(chname =>
					{
						var ch = props.CreateChannel();
						ch.Name = chname;
						ch.DataUnit = DataUnit.Microvolt;
						if (coords.ContainsKey(chname)) ch.Coords = coords[chname];
						return ch;
					}));
					props.AveragedSegments = 0;
					props.DatasetLength = file.nsample;
					props.Datatype = DataType.TimeDomain;
					props.SegmentationType = SegmentationType.NotSegmented;
					props.SamplingInterval = 1000000.0 / file.sfreq;
					props.Rereferenced = false;
					props.Save(storage);
					storage.SetStreamTextA("Name", dataName);
					storage.SetStreamText("NameW", dataName);
					storage.SetStreamTextA("DataPath", dataFile);
					storage.SetStreamText("DataPathW", dataFile);
					storage.SetGuid(new Guid(Guid));
					storage.SetStreamText("InternalDataXML", file.headerxml);
				}
			}
			catch (Exception ex)
			{
				throw new AnalyzerException("NedfDataReader::CreateStorageEntries", "Reader error", ex.Message, ex);
			}
		}
	}
}
