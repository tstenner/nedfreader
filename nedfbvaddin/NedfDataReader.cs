using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using BrainVision.Interfaces;
using BrainVision.Support;
using NedfReader;

namespace BrainVision.Analyzer.Readers
{
	[Reader(ID, "NEDF Data Reader", "NEDF Data Reader", 0, 1000000)]
	public sealed class NedfDataReader : IEEGRawFileReader, IEEGData, IDisposable
	{
		public const string ID = "9df2871b-278a-47c4-bd41-0582d874daa9";
		private string filename;
		private NedfFile datafile;
		private IEEGProperties eegProperties;
		private IEEGStorage eegStorage;

		public void Init(IStructuredStorage storage, IEEGData parent)
		{
			if (storage == null)
				throw new ArgumentNullException(nameof(storage));
			try
			{
				eegStorage = storage as IEEGStorage;
				filename = storage.GetStreamText("DataPath");
				datafile = new NedfFile(filename);
				eegProperties = GetProperties();
			}
			catch (Exception ex)
			{
				throw new AnalyzerException("NedfDataReader::Init", "Reader error", ex.Message, ex);
			}
		}

		public IEEGProperties GetProperties() => eegStorage != null ? (eegProperties ??= ComponentFactory.CreateProperties(eegStorage)) : null;

		public float[] GetData(uint nPosition, uint nPoints, int[] channelList) => datafile.GetData(nPosition, nPoints, channelList);

		public IEEGMarker[] GetMarkers(uint nPosition, uint nPoints) => eegStorage?.GetMarkers(nPosition, nPoints);

		public IEEGMarker GetMarker(EEGMarkerIndex markerIndex) => eegStorage?.GetMarker(markerIndex);

		public EEGMarkerIndex[] GetMarkerIndexTable() => eegStorage?.GetMarkerIndexTable();

		public IStructuredStorage GetStorage() => eegStorage;

		public int GetTransientDataOffset() => 0;

		public int OpenRawFile(string sFilename)
		{
			if (string.IsNullOrEmpty(sFilename) || !System.IO.File.Exists(sFilename) || Path.GetExtension(sFilename) != ".nedf")
				return -1;
			filename = sFilename;
			try
			{
				using var nedffile = new NedfFile(filename);
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
				using var file = new NedfFile(filename, (warning)=>MessageBox.Show(warning, filename));
				string dataName = "Tristans Raw Data";
				var markers = file.GetMarkerPairs().ToList();
				if (markers.Count > file.NSample / 10)
					dataName = "Corrupt Data File";
				else
					ComponentFactory.SaveChangedMarkers(storage, markers.Select(pair =>
					{
						var m = ComponentFactory.CreateChangeMarker();
						m.Channel = -1;
						(uint pos, var val) = pair;
						m.Position = pos;
						m.Points = 1;
						m.Type = "Stimulus";
						m.Visible = true;
						m.Description = val.ToString(CultureInfo.InvariantCulture);
						return m;
					}).ToList());

				var coords = ElectrodeCoordinates.GetDefaultCoordinates();
				props.Channels.AddRange(file.Channelnames.Select(chname =>
				{
					var ch = props.CreateChannel();
					ch.Name = chname;
					ch.DataUnit = DataUnit.Microvolt;
					if (coords.ContainsKey(chname)) ch.Coords = coords[chname];
					return ch;
				}));
				props.AveragedSegments = 0;
				props.DatasetLength = file.NSample;
				props.Datatype = DataType.TimeDomain;
				props.SegmentationType = SegmentationType.NotSegmented;
				props.SamplingInterval = 1000000.0 / file.SFreq;
				props.Rereferenced = false;
				props.Save(storage);
				storage.SetStreamTextA("Name", dataName);
				storage.SetStreamText("NameW", dataName);
				storage.SetStreamTextA("DataPath", filename);
				storage.SetStreamText("DataPathW", filename);
				storage.SetGuid(new Guid(ID));
				storage.SetStreamText("InternalDataXML", file.headerxml);
			}
			catch (Exception ex)
			{
				throw new AnalyzerException("NedfDataReader::CreateStorageEntries", "Reader error", ex.Message, ex);
			}
		}

		public void Dispose() => datafile?.Dispose();
	}
}
