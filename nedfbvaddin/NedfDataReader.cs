using System;
using System.IO;
using System.Linq;
using BrainVision.Interfaces;
using BrainVision.Support;

namespace BrainVision.Analyzer.Readers
{
	[Reader(Guid, "NEDF Data Reader", "NEDF Data Reader", 0, 1)]
	public class NedfDataReader : IEEGRawFileReader, IEEGData, IDisposable
	{
		public const string Guid = "9df2871b-278a-47c4-bd41-0582d874daa9";
		private string dataFile;
		private IEEGProperties eegProperties;
		private IEEGStorage eegStorage;
		private NedfFile file;

		public void Dispose() { }

		public void Init(IStructuredStorage storage, IEEGData parent)
		{
			try
			{
				eegStorage = storage as IEEGStorage;
				dataFile = storage.GetStreamText("DataPath");
				file = new NedfFile(dataFile);
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
			if (file == null) file = new NedfFile(dataFile);
			return file.GetData(nPosition, nPoints, channelList);
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
				file = new NedfFile(dataFile);
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
				file = new NedfFile(dataFile);
				ComponentFactory.SaveChangedMarkers(storage, file.GetMarkerPairs().Select(pair =>
				{
					var m = ComponentFactory.CreateChangeMarker();
					m.Channel = -1;
					var (pos, val) = pair;
					m.Position = pos;
					m.Points = 1;
					m.Type = "Stimulus";
					m.Visible = true;
					m.Description = val.ToString();
					return m;
				}).ToList());

				props.Channels.AddRange(file.channelnames.Select(chname =>
				{
					var ch = props.CreateChannel();
					ch.Name = chname;
					ch.DataUnit = DataUnit.Microvolt;
					return ch;
				}));
				props.AveragedSegments = 0;
				props.DatasetLength = file.nsample;
				props.Datatype = DataType.TimeDomain;
				props.SegmentationType = SegmentationType.NotSegmented;
				props.SamplingInterval = 1000000.0 / file.sfreq;
				props.Rereferenced = false;
				props.Save(storage);
				storage.SetStreamTextA("Name", "Tristans Raw Data");
				storage.SetStreamText("NameW", "Tristans Raw Data");
				storage.SetStreamTextA("DataPath", dataFile);
				storage.SetStreamText("DataPathW", dataFile);
				storage.SetGuid(new Guid(Guid));
				storage.SetStreamText("InternalDataXML", file.headerxml);
			}
			catch (Exception ex)
			{
				throw new AnalyzerException("NedfDataReader::CreateStorageEntries", "Reader error", ex.Message, ex);
			}
		}
	}
}
