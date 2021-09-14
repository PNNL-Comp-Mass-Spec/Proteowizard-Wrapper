/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using pwiz.CLI.cv;
using pwiz.CLI.data;
using pwiz.CLI.msdata;
using pwiz.CLI.analysis;

// ReSharper disable UnusedMember.Global
namespace pwiz.ProteowizardWrapper
{
#pragma warning disable 1591
    /// <summary>
    /// This is our wrapper class for ProteoWizard's MSData file reader interface.
    ///
    /// Performance measurements can be made here, see notes below on enabling that.
    ///
    /// When performance measurement is enabled, the GetLog() method can be called
    /// after read operations have been completed. This returns a handy CSV-formatted
    /// report on file read performance.
    /// </summary>
    internal class MsDataFileImpl : IDisposable
    {
        #region PNNL Added functions

        /// <summary>
        /// This static constructor ensures that the Assembly Resolver is added prior to actually using this class.
        /// </summary>
        /// <remarks>This code is executed prior to the instance constructor</remarks>
        static MsDataFileImpl()
        {
            pwiz.ProteowizardWrapper.DependencyLoader.AddAssemblyResolver();
        }

        /// <summary>
        /// Get the list of CVParams for the specified chromatogram
        /// </summary>
        /// <param name="chromIndex"></param>
        /// <returns></returns>
        /// <remarks>Use of this method requires the calling project to reference pwiz_bindings_cli.dll</remarks>
        public CVParamList GetChromatogramCVParams(int chromIndex)
        {
            return ChromatogramList.chromatogram(chromIndex).cvParams;
        }

        /// <summary>
        /// Get the ProteoWizard native chromatogram object for the specified spectrum
        /// </summary>
        /// <param name="chromIndex"></param>
        /// <returns></returns>
        /// <remarks>Use of this method requires the calling project to reference pwiz_bindings_cli.dll</remarks>
        public Chromatogram GetChromatogramObject(int chromIndex)
        {
            return ChromatogramList.chromatogram(chromIndex, true);
        }

        /// <summary>
        /// Get the list of CVParams for the specified spectrum
        /// </summary>
        /// <param name="scanIndex"></param>
        /// <returns></returns>
        /// <remarks>
        /// Use of this method requires the calling project to reference pwiz_bindings_cli.dll
        /// Alternatively, use <see cref="GetSpectrumCVParamData"/>
        /// </remarks>
        public CVParamList GetSpectrumCVParams(int scanIndex)
        {
            return GetPwizSpectrum(scanIndex, false).cvParams;
        }

        /// <summary>
        /// Get the list of CVParams for the specified spectrum
        /// </summary>
        /// <param name="scanIndex"></param>
        /// <returns>List of CVParamData structs</returns>
        public List<CVParamData> GetSpectrumCVParamData(int scanIndex)
        {
            var cvParams = CopyCVParamData(GetSpectrumCVParams(scanIndex));

            return cvParams;
        }

        /// <summary>
        /// Get a container describing the scan (or scans) associated with the given spectrum
        /// </summary>
        /// <param name="scanIndex"></param>
        /// <returns>Scan info container</returns>
        /// <remarks>Useful for obtaining the filter string, scan start time, ion injection time, etc.</remarks>
        public SpectrumScanContainer GetSpectrumScanInfo(int scanIndex)
        {
            var spec = GetPwizSpectrum(scanIndex, false);
            var scanList = spec.scanList;
            var scanInfo = new SpectrumScanContainer()
            {
                CVParams = CopyCVParamData(scanList.cvParams)
            };

            foreach (var scan in scanList.scans)
            {
                var scanData = new SpectrumScanData
                {
                    CVParams = CopyCVParamData(scan.cvParams)
                };

                foreach (var userParam in scan.userParams)
                {
                    scanData.UserParams.Add(new KeyValuePair<string, string>(userParam.name, userParam.value));
                }

                foreach (var scanWindow in scan.scanWindows)
                {
                    scanData.ScanWindowList.AddRange(CopyCVParamData(scanWindow.cvParams));
                }

                scanInfo.Scans.Add(scanData);
            }

            return scanInfo;
        }

        private static CVParamData CopyCVParamData(CVParam param)
        {
            var paramCopy = new CVParamData
            {
                CVId = (int)param.cvid,
                CVName = param.cvid.ToString(),
                Name = param.name,
                Value = param.value,
                UnitsID = (int)param.units,
                UnitsName = param.unitsName
            };
            return paramCopy;
        }

        private static List<CVParamData> CopyCVParamData(CVParamList paramList)
        {
            var cvParams = new List<CVParamData>();

            foreach (var param in paramList)
            {
                cvParams.Add(CopyCVParamData(param));
            }

            return cvParams;
        }

        /// <summary>
        /// Get the ProteoWizard native spectrum object for the specified spectrum.
        /// </summary>
        /// <param name="scanIndex"></param>
        /// <returns></returns>
        /// <remarks>Use of this method requires the calling project to reference pwiz_bindings_cli.dll</remarks>
        public Spectrum GetSpectrumObject(int scanIndex)
        {
            return GetPwizSpectrum(scanIndex, getBinaryData: true);
        }

        /// <summary>
        /// List of MSConvert-style filter strings to apply to the spectrum list.
        /// </summary>
        /// <remarks>If the filter count is greater than 0, the default handling of the spectrumList using the optional constructor parameters is disabled.</remarks>
        public readonly List<string> Filters = new();

        /// <summary>
        /// Uses the centroiding/peak picking algorithm that the vendor libraries provide, if available; otherwise uses a low-quality centroiding algorithm.
        /// </summary>
        public const string VendorCentroiding = "peakPicking true 1-";
        private bool _useVendorCentroiding;

        /// <summary>
        /// Continuous Wavelet Transform peak picker - high-quality peak picking, may be slow with some high-res data.
        /// </summary>
        public const string CwtCentroiding = "peakPicking cwt snr=1.0 peakSpace=0.1 msLevel=1-";
        private bool _useCwtCentroiding;

        /// <summary>
        /// Add/remove Vendor Centroiding to the filter list. Call <see cref="RedoFilters()"/> if calling this after reading any spectra.
        /// </summary>
        public bool UseVendorCentroiding
        {
            get => Filters.Contains(VendorCentroiding);
            set
            {
                if (_useVendorCentroiding != value)
                {
                    _useVendorCentroiding = value;
                    if (value)
                    {
                        Filters.Add(VendorCentroiding);
                    }
                    else
                    {
                        Filters.Remove(VendorCentroiding);
                    }
                }
            }
        }

        /// <summary>
        /// Add/remove CWT Centroiding to the filter list. Call <see cref="RedoFilters()"/> if calling this after reading any spectra.
        /// </summary>
        public bool UseCwtCentroiding
        {
            get => Filters.Contains(CwtCentroiding);
            set
            {
                if (_useCwtCentroiding != value)
                {
                    _useCwtCentroiding = value;
                    if (value)
                    {
                        Filters.Add(CwtCentroiding);
                    }
                    else
                    {
                        Filters.Remove(CwtCentroiding);
                    }
                }
            }
        }

        /// <summary>
        /// Force the reload of the spectrum list, reapplying any specified filters.
        /// </summary>
        public void RedoFilters()
        {
            _spectrumList = null;
            if (_spectrumListBase == null)
            {
                _spectrumListBase = _msDataFile.run.spectrumList;
            }
            _msDataFile.run.spectrumList = _spectrumListBase;
        }
        #endregion

        private static readonly ReaderList FULL_READER_LIST = ReaderList.FullReaderList;

        // Cached disposable objects
        private MSData _msDataFile;
        private readonly ReaderConfig _config;
        private SpectrumList _spectrumList;
        // For storing the unwrapped spectrumList, in case modification/unwrapping is needed
        private SpectrumList _spectrumListBase;
        private ChromatogramList _chromatogramList;
        private MsDataScanCache _scanCache;
        private readonly LockMassParameters _lockmassParameters; // For Waters lockmass correction
        private int? _lockmassFunction;  // For Waters lockmass correction
        private readonly MethodInfo _binaryDataArrayGetData;

        private readonly bool _requireVendorCentroidedMS1;
        private readonly bool _requireVendorCentroidedMS2;

        private DetailLevel _detailMsLevel = DetailLevel.InstantMetadata;

        private DetailLevel _detailStartTime = DetailLevel.InstantMetadata;

        private DetailLevel _detailDriftTime = DetailLevel.InstantMetadata;

        private double[] ToArray(BinaryDataArray binaryDataArray)
        {
            // Original code
            //return binaryDataArray.data.ToArray();

            // BinaryDataArray.get_data() problem fix
            // Pre-Nov. 7th, 2018 pwiz_binding_cli.dll: binaryDataArray.data returns pwiz.CLI.msdata.BinaryData, is a semi-automatic wrapper for a C++ vector, which implements IList<double>
            // Pre-Nov. 7th, 2018 pwiz_binding_cli.dll: binaryDataArray.data returns pwiz.CLI.util.BinaryData implements IList<double>, but also provides other optimization functions
            // The best way to access this before was binaryDataArray.data.ToArray()
            // In the future, this could be changed to binaryDataArray.data.Storage.ToArray(), but that may lead to more data copying than just using the IEnumerable<double> interface
            // Both versions implement IList<double>, so I can get the object via reflection and cast it to an IList<double> (or IEnumerable<double>).

            // Call via reflection to avoid issues of the ProteoWizardWrapper compiled reference vs. the ProteoWizard compiled DLL
            var dataObj = _binaryDataArrayGetData?.Invoke(binaryDataArray, null);
            if (dataObj != null && dataObj is IEnumerable<double> data)
            {
                return data.ToArray();
            }

            return new double[0];
        }

        private static float[] ToFloatArray(IList<double> list)
        {
            var result = new float[list.Count];
            for (var i = 0; i < result.Length; i++)
                result[i] = (float)list[i];
            return result;
        }

        private float[] ToFloatArray(BinaryDataArray binaryDataArray)
        {
            // BinaryDataArray.get_data() problem fix
            // Pre-Nov. 7th, 2018 pwiz_binding_cli.dll: binaryDataArray.data returns pwiz.CLI.msdata.BinaryData, is a semi-automatic wrapper for a C++ vector, which implements IList<double>
            // Pre-Nov. 7th, 2018 pwiz_binding_cli.dll: binaryDataArray.data returns pwiz.CLI.util.BinaryData implements IList<double>, but also provides other optimization functions
            // The best way to access this before was binaryDataArray.data.ToArray()
            // In the future, this could be changed to binaryDataArray.data.Storage.ToArray(), but that may lead to more data copying than just using the IEnumerable<double> interface
            // Both versions implement IList<double>, so I can get the object via reflection and cast it to an IList<double> (or IEnumerable<double>).

            // Call via reflection to avoid issues of the ProteoWizardWrapper compiled reference vs. the ProteoWizard compiled DLL
            var dataObj = _binaryDataArrayGetData?.Invoke(binaryDataArray, null);
            if (dataObj != null && dataObj is IList<double> data)
            {
                return ToFloatArray(data);
            }

            return new float[0];
        }

        /// <summary>
        /// Returns the file id of the specified file (as an array, which typically only has one item)
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string[] ReadIds(string path)
        {
            return FULL_READER_LIST.readIds(path);
        }

        public const string PREFIX_TOTAL = "SRM TIC "; // Not L10N
        public const string PREFIX_SINGLE = "SRM SIC "; // Not L10N
        public const string PREFIX_PRECURSOR = "SIM SIC "; // Not L10N

        /// <summary>
        /// Return false if id starts with "+ "
        /// Return true  if id starts with "- "
        /// Otherwise, return null
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static bool? IsNegativeChargeIdNullable(string id)
        {
            if (id.StartsWith("+ ")) // Not L10N
                return false;
            if (id.StartsWith("- ")) // Not L10N
                return true;
            return null;
        }

        /// <summary>
        /// Return true if the id starts with "SRM SIC" or "SIM SIC"
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static bool IsSingleIonCurrentId(string id)
        {
            if (IsNegativeChargeIdNullable(id).HasValue)
                id = id.Substring(2);
            return id.StartsWith(PREFIX_SINGLE) || id.StartsWith(PREFIX_PRECURSOR);
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="path">Data file path</param>
        /// <param name="sampleIndex">Sample index, typically 0</param>
        /// <param name="lockmassParameters">Lock mass parameters (used for Waters datasets)</param>
        /// <param name="simAsSpectra">Whether to treat SIM data as spectra, default false</param>
        /// <param name="srmAsSpectra">Whether to treat SRM data as spectra, default false</param>
        /// <param name="acceptZeroLengthSpectra">Whether to accept zero-length spectra, default true</param>
        /// <param name="requireVendorCentroidedMS1">True to return centroided MS1 spectra</param>
        /// <param name="requireVendorCentroidedMS2">True to return centroided MS2 spectra</param>
        public MsDataFileImpl(
            string path,
            int sampleIndex = 0,
            LockMassParameters lockmassParameters = null,
            bool simAsSpectra = false,
            bool srmAsSpectra = false,
            bool acceptZeroLengthSpectra = true,
            bool requireVendorCentroidedMS1 = false,
            bool requireVendorCentroidedMS2 = false)
        {
            FilePath = path;
            _msDataFile = new MSData();
            _config = new ReaderConfig { simAsSpectra = simAsSpectra, srmAsSpectra = srmAsSpectra, acceptZeroLengthSpectra = acceptZeroLengthSpectra };
            _lockmassParameters = lockmassParameters;
            InitializeReader(path, _msDataFile, sampleIndex, _config);
            _requireVendorCentroidedMS1 = requireVendorCentroidedMS1;
            _requireVendorCentroidedMS2 = requireVendorCentroidedMS2;

            // BinaryDataArray.get_data() problem fix
            // Pre-Nov. 7th, 2018 pwiz_binding_cli.dll: bda.data returns pwiz.CLI.msdata.BinaryData,  is a semi-automatic wrapper for a C++ vector, which implements IList<double>
            // Pre-Nov. 7th, 2018 pwiz_binding_cli.dll: bda.data returns pwiz.CLI.util.BinaryData implements IList<double>, but also provides other optimization functions
            // The best way to access this before was bda.data.ToArray()
            // In the future, this could be changed to bda.data.Storage.ToArray(), but that may lead to more data copying than just using the IEnumerable<double> interface
            // Both versions implement IList<double>, so I can get the object via reflection and cast it to an IList<double> (or IEnumerable<double>).

            // Get the MethodInfo for BinaryDataArray.data property accessor
            _binaryDataArrayGetData = typeof(BinaryDataArray).GetProperty("data", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)?.GetMethod;
        }

        /// <summary>
        /// Initialize the reader
        /// </summary>
        /// <param name="path"></param>
        /// <param name="msDataFile"></param>
        /// <param name="sampleIndex"></param>
        /// <param name="config"></param>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptions()]
        public void InitializeReader(string path, MSData msDataFile, int sampleIndex, ReaderConfig config)
        {
            try
            {
                FULL_READER_LIST.read(path, msDataFile, sampleIndex, config);
            }
            catch (AccessViolationException)
            {
                throw new Exception("Critical error opening dataset with ProteoWizard. The data is corrupt or in an unsupported format: " + path);
            }
        }

        /// <summary>
        /// Call this method to enable caching recently read spectra
        /// </summary>
        /// <param name="cacheSize"></param>
        public void EnableCaching(int? cacheSize)
        {
            if (cacheSize == null || cacheSize.Value <= 0)
            {
                _scanCache = new MsDataScanCache();
            }
            else
            {
                _scanCache = new MsDataScanCache(cacheSize.Value);
            }
        }

        /// <summary>
        /// Disable the spectrum data caching
        /// </summary>
        public void DisableCaching()
        {
            _scanCache.Clear();
            _scanCache = null;
        }

        /// <summary>
        /// The Run ID
        /// </summary>
        public string RunId => _msDataFile.run.id;

        /// <summary>
        /// The run start time
        /// </summary>
        public DateTime? RunStartTime
        {
            get
            {
                var stampText = _msDataFile.run.startTimeStamp;
                if (!DateTime.TryParse(stampText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var runStartTime) &&
                    !DateTime.TryParse(stampText, out runStartTime))
                    return null;
                return runStartTime;
            }
        }

        /// <summary>
        /// Data and Instrument Configuration information
        /// </summary>
        public MsDataConfigInfo ConfigInfo
        {
            get
            {
                var spectra = SpectrumList.size();
                var ionSource = string.Empty;
                var analyzer = string.Empty;
                var detector = string.Empty;
                foreach (var ic in _msDataFile.instrumentConfigurationList)
                {
                    GetInstrumentConfig(ic, out var instrumentIonSource, out var instrumentAnalyzer, out var instrumentDetector);

                    if (ionSource.Length > 0)
                        ionSource += ", "; // Not L10N
                    ionSource += instrumentIonSource;

                    if (analyzer.Length > 0)
                        analyzer += ", "; // Not L10N
                    analyzer += instrumentAnalyzer;

                    if (detector.Length > 0)
                        detector += ", "; // Not L10N
                    detector += instrumentDetector;
                }

                var contentTypeSet = new HashSet<string>();
                foreach (var term in _msDataFile.fileDescription.fileContent.cvParams)
                    contentTypeSet.Add(term.name);
                var contentTypes = contentTypeSet.ToArray();
                Array.Sort(contentTypes);
                var contentType = String.Join(", ", contentTypes); // Not L10N

                return new MsDataConfigInfo
                {
                    Analyzer = analyzer,
                    ContentType = contentType,
                    Detector = detector,
                    IonSource = ionSource,
                    Spectra = spectra
                };
            }
        }

        private static void GetInstrumentConfig(InstrumentConfiguration ic, out string ionSource, out string analyzer, out string detector)
        {
            // ReSharper disable CollectionNeverQueried.Local  (why does ReSharper warn on this?)
            var ionSources = new SortedDictionary<int, string>();
            var analyzers = new SortedDictionary<int, string>();
            var detectors = new SortedDictionary<int, string>();
            // ReSharper restore CollectionNeverQueried.Local

            foreach (var c in ic.componentList)
            {
                CVParam term;
                switch (c.type)
                {
                    case ComponentType.ComponentType_Source:
                        term = c.cvParamChild(CVID.MS_ionization_type);
                        if (!term.empty())
                            ionSources.Add(c.order, term.name);
                        else
                        {
                            // If we did not find the ion source in a CVParam it may be in a UserParam
                            var uParam = c.userParam("msIonisation"); // Not L10N
                            if (HasInfo(uParam))
                            {
                                ionSources.Add(c.order, uParam.value);
                            }
                        }
                        break;
                    case ComponentType.ComponentType_Analyzer:
                        term = c.cvParamChild(CVID.MS_mass_analyzer_type);
                        if (!term.empty())
                            analyzers.Add(c.order, term.name);
                        else
                        {
                            // If we did not find the analyzer in a CVParam it may be in a UserParam
                            var uParam = c.userParam("msMassAnalyzer"); // Not L10N
                            if (HasInfo(uParam))
                            {
                                analyzers.Add(c.order, uParam.value);
                            }
                        }
                        break;
                    case ComponentType.ComponentType_Detector:
                        term = c.cvParamChild(CVID.MS_detector_type);
                        if (!term.empty())
                            detectors.Add(c.order, term.name);
                        else
                        {
                            // If we did not find the detector in a CVParam it may be in a UserParam
                            var uParam = c.userParam("msDetector"); // Not L10N
                            if (HasInfo(uParam))
                            {
                                detectors.Add(c.order, uParam.value);
                            }
                        }
                        break;
                }
            }

            ionSource = String.Join("/", new List<string>(ionSources.Values).ToArray()); // Not L10N

            analyzer = String.Join("/", new List<string>(analyzers.Values).ToArray()); // Not L10N

            detector = String.Join("/", new List<string>(detectors.Values).ToArray()); // Not L10N
        }

        /// <summary>
        /// Check if the file has be processed by the specified software
        /// </summary>
        /// <param name="softwareName"></param>
        /// <returns></returns>
        public bool IsProcessedBy(string softwareName)
        {
            foreach (var softwareApp in _msDataFile.softwareList)
            {
                if (softwareApp.id.Contains(softwareName))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// If the spectrum is a Waters Lockmass spectrum
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public bool IsWatersLockmassSpectrum(MsDataSpectrum s)
        {
            return _lockmassFunction.HasValue && (s.WatersFunctionNumber >= _lockmassFunction.Value);
        }

        /// <summary>
        /// Record any instrument info found in the file, along with any Waters lockmass info we have
        /// </summary>
        public IEnumerable<MsInstrumentConfigInfo> GetInstrumentConfigInfoList()
        {
            IList<MsInstrumentConfigInfo> configList = new List<MsInstrumentConfigInfo>();

            foreach (var ic in _msDataFile.instrumentConfigurationList)
            {
                string instrumentModel = null;

                var param = ic.cvParamChild(CVID.MS_instrument_model);
                if (!param.empty() && param.cvid != CVID.MS_instrument_model)
                {
                    instrumentModel = param.name;
                }
                if (instrumentModel == null)
                {
                    // If we did not find the instrument model in a CVParam it may be in a UserParam
                    var uParam = ic.userParam("msModel"); // Not L10N
                    if (HasInfo(uParam))
                    {
                        instrumentModel = uParam.value;
                    }
                }

                // get the ionization type, analyzer and detector
                GetInstrumentConfig(ic, out var ionization, out var analyzer, out var detector);

                if (instrumentModel != null || ionization != null || analyzer != null || detector != null)
                {
                    configList.Add(new MsInstrumentConfigInfo(instrumentModel, ionization, analyzer, detector));
                }
            }
            return configList;
        }

        private static bool HasInfo(UserParam uParam)
        {
            return !uParam.empty() && !String.IsNullOrEmpty(uParam.value) &&
                   !String.Equals("unknown", uParam.value.ToString().ToLowerInvariant()); // Not L10N
        }

        public bool IsABFile
        {
            get { return _msDataFile.fileDescription.sourceFiles.Any(source => source.hasCVParam(CVID.MS_ABI_WIFF_format)); }
        }

        public bool IsMzWiffXml => IsProcessedBy("mzWiff");

        public bool IsAgilentFile
        {
            get { return _msDataFile.fileDescription.sourceFiles.Any(source => source.hasCVParam(CVID.MS_Agilent_MassHunter_format)); }
        }

        public bool IsThermoFile
        {
            get { return _msDataFile.fileDescription.sourceFiles.Any(source => source.hasCVParam(CVID.MS_Thermo_RAW_format)); }
        }

        public bool IsWatersFile
        {
            get { return _msDataFile.fileDescription.sourceFiles.Any(source => source.hasCVParam(CVID.MS_Waters_raw_format)); }
        }

        public bool IsWatersLockmassCorrectionCandidate
        {
            get
            {
                try
                {
                    // Has to be a .raw file, not just an mzML translation of one
                    return (FilePath.ToLowerInvariant().EndsWith(".raw")) && // Not L10N
                        IsWatersFile &&
                        _msDataFile.run.spectrumList != null &&
                        !_msDataFile.run.spectrumList.empty() &&
                        !HasChromatogramData &&
                        !HasDriftTimeSpectra && // CDT reader doesn't handle lockmass correction as of Nov 2015
                        !HasSrmSpectra;
                }
                catch (Exception)
                {
                    // Whatever that was, it wasn't a Waters file
                    return false;
                }
            }
        }

        public bool IsShimadzuFile
        {
            get { return _msDataFile.fileDescription.sourceFiles.Any(source => source.hasCVParam(CVID.MS_Shimadzu_Biotech_nativeID_format)); }
        }

        private ChromatogramList ChromatogramList
        {
            get
            {
                return _chromatogramList = _chromatogramList ??
                    _msDataFile.run.chromatogramList;
            }
        }

        private SpectrumList SpectrumList
        {
            get
            {
                if (_spectrumList == null)
                {
                    if (_spectrumListBase == null)
                    {
                        _spectrumListBase = _msDataFile.run.spectrumList;
                    }

                    if (Filters.Count == 0)
                    {
                        // CONSIDER(bspratt): there is no acceptable wrapping order when both centroiding and lockmass are needed at the same time
                        // (For now, this can't happen in practice, as Waters offers no centroiding, but someday this may force pwiz API rework)
                        var centroidLevel = new List<int>();
                        _spectrumList = _msDataFile.run.spectrumList;
                        var hasSrmSpectra = HasSrmSpectraInList(_spectrumList);
                        if (!hasSrmSpectra)
                        {
                            if (_requireVendorCentroidedMS1)
                                centroidLevel.Add(1);
                            if (_requireVendorCentroidedMS2)
                                centroidLevel.Add(2);
                        }
                        if (centroidLevel.Any())
                        {
                            _spectrumList = new SpectrumList_PeakPicker(_spectrumList,
                                new VendorOnlyPeakDetector(),
                                // Throws an exception when no vendor centroiding available
                                true, centroidLevel.ToArray());
                        }

                        _lockmassFunction = null;
                        if (_lockmassParameters != null && !_lockmassParameters.IsEmpty)
                        {
                            _spectrumList = new SpectrumList_LockmassRefiner(_spectrumList,
                                _lockmassParameters.LockmassPositive ?? 0,
                                _lockmassParameters.LockmassNegative ?? 0,
                                _lockmassParameters.LockmassTolerance ?? LockMassParameters.LOCKMASS_TOLERANCE_DEFAULT);
                            if (_spectrumList.size() > 0 && !hasSrmSpectra)
                            {
                                // If the first seen spectrum has MS1 data and function > 1 assume it's the lock spray function,
                                // and thus to be omitted from chromatogram extraction.
                                // N.B. for msE data we will always assume function 3 and greater are to be omitted
                                // CONSIDER(bspratt) I really wish there was some way to communicate decisions like this to the user
                                using (var spectrum = _spectrumList.spectrum(0, DetailLevel.FullMetadata))
                                {
                                    if (GetMsLevel(spectrum) == 1)
                                    {
                                        var function =
                                            MsDataSpectrum.WatersFunctionNumberFromId(id.abbreviate(spectrum.id));
                                        if (function > 1)
                                            _lockmassFunction = function;
                                        // Ignore all scans in this function for chromatogram extraction purposes
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        SpectrumListFactory.wrap(_msDataFile, Filters);
                        _spectrumList = _msDataFile.run.spectrumList;
                    }
                }
                return _spectrumList;
            }
        }

        public int ChromatogramCount => ChromatogramList?.size() ?? 0;

        public string GetChromatogramId(int index, out int indexId)
        {
            using (var cid = ChromatogramList.chromatogramIdentity(index))
            {
                indexId = cid.index;
                return cid.id;
            }
        }

        public void GetChromatogram(int chromIndex, out string id,
            out float[] timeArray, out float[] intensityArray)
        {
            using (var chrom = ChromatogramList.chromatogram(chromIndex, true))
            {
                id = chrom.id;

                // Original code
                //timeArray = ToFloatArray(chrom.binaryDataArrays[0].data);
                //intensityArray = ToFloatArray(chrom.binaryDataArrays[1].data);

                // BinaryDataArray.get_data() problem fix
                timeArray = ToFloatArray(chrom.binaryDataArrays[0]);
                intensityArray = ToFloatArray(chrom.binaryDataArrays[1]);
            }
        }

        /// <summary>
        /// Gets the retention times from the first chromatogram in the data file.
        /// Returns null if there are no chromatograms in the file.
        /// </summary>
        public double[] GetScanTimes()
        {
            if (ChromatogramList == null || ChromatogramList.empty())
            {
                return null;
            }
            using (var chromatogram = ChromatogramList.chromatogram(0, true))
            {
                if (chromatogram == null)
                {
                    return null;
                }
                var timeIntensityPairList = new TimeIntensityPairList();
                chromatogram.getTimeIntensityPairs(ref timeIntensityPairList);
                var times = new double[timeIntensityPairList.Count];
                for (var i = 0; i < times.Length; i++)
                {
                    times[i] = timeIntensityPairList[i].time;
                }
                return times;
            }
        }

        public double[] GetTotalIonCurrent()
        {
            if (ChromatogramList == null)
            {
                return null;
            }
            using (var chromatogram = ChromatogramList.chromatogram(0, true))
            {
                if (chromatogram == null)
                {
                    return null;
                }
                var timeIntensityPairList = new TimeIntensityPairList();
                chromatogram.getTimeIntensityPairs(ref timeIntensityPairList);
                var intensities = new double[timeIntensityPairList.Count];
                for (var i = 0; i < intensities.Length; i++)
                {
                    intensities[i] = timeIntensityPairList[i].intensity;
                }
                return intensities;
            }
        }

        /// <summary>
        /// Walks the spectrum list, and fills in the retention time and MS level of each scan.
        /// Some data files do not have any chromatograms in them, so GetScanTimes
        /// cannot be used.
        /// </summary>
        /// <param name="times">Output: scan times (in minutes)</param>
        /// <param name="msLevels">Output: MS Levels (1 for MS1, 2 for MS/MS, etc.)</param>
        /// <param name="progressDelegate">
        /// Delegate method for reporting progress while iterating over the spectra;
        /// The first value is spectra processed; the second value is total spectra
        /// </param>
        /// <remarks>See also the overloaded version that accepts a CancellationToken</remarks>
        /// <param name="useAlternateMethod">
        /// When false, use the default method for retrieving spectrum info; this could lead to an exception if the spectrum is empty (has no ions)
        /// When true, use an alternate method to retrieve the spectrum info (DetailLevel.InstantMetadata)
        /// </param>
        public void GetScanTimesAndMsLevels(out double[] times, out byte[] msLevels, Action<int, int> progressDelegate = null, bool useAlternateMethod = false)
        {
            var cancellationToken = new CancellationToken();
            GetScanTimesAndMsLevels(cancellationToken, out times, out msLevels, progressDelegate, useAlternateMethod);
        }

        /// <summary>
        /// Walks the spectrum list, and fills in the retention time and MS level of each scan.
        /// Some data files do not have any chromatograms in them, so GetScanTimes
        /// cannot be used.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="times">Output: scan times (in minutes)</param>
        /// <param name="msLevels">Output: MS Levels (1 for MS1, 2 for MS/MS, etc.)</param>
        /// <param name="progressDelegate">
        /// Delegate method for reporting progress while iterating over the spectra;
        /// The first value is spectra processed; the second value is total spectra
        /// </param>
        /// <param name="useAlternateMethod">
        /// When false, use the default method for retrieving spectrum info; this could lead to an exception if the spectrum is empty (has no ions)
        /// When true, use an alternate method to retrieve the spectrum info (DetailLevel.InstantMetadata)
        /// </param>
        public void GetScanTimesAndMsLevels(
            CancellationToken cancellationToken,
            out double[] times,
            out byte[] msLevels,
            Action<int, int> progressDelegate = null,
            bool useAlternateMethod = false)
        {
            // Assure that the progress delegate is not null
            if (progressDelegate == null)
                progressDelegate = delegate { };

            var spectrumCount = SpectrumCount;
            times = new double[spectrumCount];
            msLevels = new byte[spectrumCount];
            for (var i = 0; i < spectrumCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (useAlternateMethod)
                {
                    using (var spectrum = SpectrumList.spectrum(i, DetailLevel.InstantMetadata))
                    {
                        times[i] = spectrum.scanList.scans[0].cvParam(CVID.MS_scan_start_time).timeInSeconds();
                        msLevels[i] = (byte)(int)spectrum.cvParam(CVID.MS_ms_level).value;
                    }
                }
                else
                {
                    using (var spectrum = SpectrumList.spectrum(i, false))
                    {
                        times[i] = spectrum.scanList.scans[0].cvParam(CVID.MS_scan_start_time).timeInSeconds();
                        msLevels[i] = (byte)(int)spectrum.cvParam(CVID.MS_ms_level).value;
                    }
                }

                progressDelegate(i, spectrumCount);
            }
        }

        public int SpectrumCount => SpectrumList?.size() ?? 0;

        [Obsolete("Use the SpectrumCount property instead")]
        public int GetSpectrumCount()
        {
            return SpectrumCount;
        }

        public int GetSpectrumIndex(string id)
        {
            var index = SpectrumList.findAbbreviated(id);
            if (0 > index || index >= SpectrumList.size())
                return -1;
            return index;
        }

        /// <summary>
        /// Populate parallel arrays with m/z and intensity values
        /// </summary>
        /// <param name="spectrumIndex"></param>
        /// <param name="mzArray"></param>
        /// <param name="intensityArray"></param>
        public void GetSpectrum(int spectrumIndex, out double[] mzArray, out double[] intensityArray)
        {
            var spectrum = GetSpectrum(spectrumIndex);
            mzArray = spectrum.Mzs;
            intensityArray = spectrum.Intensities;
        }

        /// <summary>
        /// Returns an MsDataSpectrum object representing the spectrum requested.
        /// </summary>
        /// <param name="spectrumIndex"></param>
        /// <param name="getBinaryData"></param>
        /// <returns></returns>
        /// <remarks>
        /// If you need direct access to CVParams, and are using MSDataFileReader, try using "GetSpectrumObject" instead.
        /// </remarks>
        public MsDataSpectrum GetSpectrum(int spectrumIndex, bool getBinaryData = true)
        {
            if (_scanCache != null)
            {
                // Check the scan for this cache
                var success = _scanCache.TryGetSpectrum(spectrumIndex, out MsDataSpectrum returnSpectrum);

                if (!success || (!returnSpectrum.BinaryDataLoaded && getBinaryData))
                {
                    // Spectrum not in the cache (or is in the cache but does not have binary data)
                    // Pull it from the file
                    returnSpectrum = GetSpectrum(GetPwizSpectrum(spectrumIndex, getBinaryData), spectrumIndex);
                    // add it to the cache
                    _scanCache.Add(spectrumIndex, returnSpectrum);
                }
                return returnSpectrum;
            }
            using (var spectrum = GetPwizSpectrum(spectrumIndex, getBinaryData))
            {
                return GetSpectrum(spectrum, spectrumIndex);
            }
        }

        /// <summary>
        /// The last read spectrum index
        /// </summary>
        private int _lastRetrievedSpectrumIndex = -1;

        /// <summary>
        /// How many times a single spectrum has been read twice in a row
        /// </summary>
        private int _sequentialDuplicateSpectrumReadCount;

        /// <summary>
        /// The maximum number of time a single spectrum can be read twice in a row before we enable a cache for sanity.
        /// </summary>
        private const int DuplicateSpectrumReadThreshold = 10;

        /// <summary>
        /// Read the native ProteoWizard spectrum, using caching if enabled, and enabling a 1-spectrum cache if the number of sequential duplicate reads passes a certain threshold.
        /// </summary>
        /// <param name="spectrumIndex"></param>
        /// <param name="getBinaryData"></param>
        /// <returns></returns>
        private Spectrum GetPwizSpectrum(int spectrumIndex, bool getBinaryData = true)
        {
            if (_scanCache != null)
            {
                var success2 = _scanCache.TryGetSpectrum(spectrumIndex, out Spectrum spectrum);
                if (!success2 || (spectrum.binaryDataArrays.Count <= 1 && getBinaryData))
                {
                    spectrum = SpectrumList.spectrum(spectrumIndex, getBinaryData);
                    _scanCache.Add(spectrumIndex, spectrum);
                }
                return spectrum;
            }
            if (_sequentialDuplicateSpectrumReadCount > DuplicateSpectrumReadThreshold)
            {
                _scanCache = new MsDataScanCache(1);
            }
            if (_lastRetrievedSpectrumIndex == spectrumIndex)
            {
                _sequentialDuplicateSpectrumReadCount++;
            }
            _lastRetrievedSpectrumIndex = spectrumIndex;
            return SpectrumList.spectrum(spectrumIndex, getBinaryData);
        }

        private MsDataSpectrum GetSpectrum(Spectrum spectrum, int spectrumIndex)
        {
            if (spectrum == null)
            {
                return new MsDataSpectrum
                {
                    Centroided = true,
                    Mzs = new double[0],
                    Intensities = new double[0]
                };
            }
            var idText = spectrum.id;
            if (idText.Trim().Length == 0)
            {
                throw new ArgumentException(string.Format("Empty spectrum ID (and index = {0}) for scan {1}", // Not L10N
                    spectrum.index, spectrumIndex));
            }

            var msDataSpectrum = new MsDataSpectrum
            {
                Id = id.abbreviate(idText),
                NativeId = idText,
                Level = GetMsLevel(spectrum) ?? 0,
                Index = spectrum.index,
                RetentionTime = GetStartTime(spectrum),
                DriftTimeMsec = GetDriftTimeMsec(spectrum),
                Precursors = GetPrecursors(spectrum),
                Centroided = IsCentroided(spectrum),
                NegativeCharge = NegativePolarity(spectrum)
            };

            if (spectrum.binaryDataArrays.Count <= 1)
            {
                msDataSpectrum.Mzs = new double[0];
                msDataSpectrum.Intensities = new double[0];
                msDataSpectrum.BinaryDataLoaded = false;
            }
            else
            {
                try
                {
                    msDataSpectrum.Mzs = ToArray(spectrum.getMZArray());
                    msDataSpectrum.Intensities = ToArray(spectrum.getIntensityArray());

                    if (msDataSpectrum.Level == 1 && _config.simAsSpectra &&
                            spectrum.scanList.scans[0].scanWindows.Count > 0)
                    {
                        msDataSpectrum.Precursors = GetMs1Precursors(spectrum);
                    }

                    msDataSpectrum.BinaryDataLoaded = true;

                    return msDataSpectrum;
                }
                catch (NullReferenceException)
                {
                }
            }
            return msDataSpectrum;
        }

        /// <summary>
        /// Get SpectrumIDs for all spectra in the run
        /// </summary>
        /// <returns>List of NativeIds</returns>
        public List<string> GetSpectrumIdList()
        {
            var spectrumIDs = new List<string>();

            var spectrumCount = SpectrumList.size();

            for (var spectrumIndex = 0; spectrumIndex < spectrumCount; spectrumIndex++)
            {
                var nativeId = SpectrumList.spectrumIdentity(spectrumIndex).id;
                spectrumIDs.Add(nativeId);
            }

            return spectrumIDs;
        }

        /// <summary>
        /// Return the typical NativeId for a scan number in a thermo .raw file
        /// </summary>
        /// <param name="scanNumber"></param>
        /// <returns></returns>
        public string GetThermoNativeId(int scanNumber)
        {
            return string.Format("controllerType=0 controllerNumber=1 scan={0}", scanNumber);
        }

        /// <summary>
        /// Return a mapping from Frame and Scan number to spectrumIndex
        /// </summary>
        /// <returns>Dictionary where keys are KeyValuePairs of Frame,Scan and values are the spectrumIndex for each scan</returns>
        public Dictionary<KeyValuePair<int, int>, int> GetUimfFrameScanPairToIndexMapping()
        {
            // frame=1 scan=1 frameType=1
            var reNativeIdMatcher = new Regex(@"frame=(?<FrameNumber>\d+) scan=(?<ScanNumber>\d+)", RegexOptions.Compiled);

            var spectrumIds = GetSpectrumIdList();

            // Walk through the spectra
            var frameScanPairToIndexMap = new Dictionary<KeyValuePair<int, int>, int>();

            for (var spectrumIndex = 0; spectrumIndex < spectrumIds.Count; spectrumIndex++)
            {
                var match = reNativeIdMatcher.Match(spectrumIds[spectrumIndex]);

                if (!match.Success)
                {
                    throw new Exception(string.Format("NativeId did not match the expected format: {0}", spectrumIds[spectrumIndex]));
                }

                var frameNumber = int.Parse(match.Groups["FrameNumber"].Value);
                var scanNumber = int.Parse(match.Groups["ScanNumber"].Value);

                frameScanPairToIndexMap.Add(new KeyValuePair<int, int>(frameNumber, scanNumber), spectrumIndex);
            }

            return frameScanPairToIndexMap;
        }

        /// <summary>
        /// Return a mapping from scan number to spectrumIndex
        /// </summary>
        /// <returns>Dictionary where keys are scan number and values are the spectrumIndex for each scan</returns>
        /// <remarks>
        /// Works for Thermo .raw files, Bruker .D folders, Bruker/Agilent .yep files, Agilent MassHunter data, Waters .raw folders, and Shimadzu data
        /// For UIMF files use <see cref="GetUimfFrameScanPairToIndexMapping"/></remarks>
        public Dictionary<int, int> GetScanToIndexMapping()
        {
            // MGF, PKL, merged DTA files. Index is the spectrum number in the file, starting from 0.
            // index=5
            // This function is not appropriate for those files because ProteoWizard does not support extracting / reading actual scan numbers from those files
            var reIndexBasedMatcher = new Regex(@"index=(?<ScanIndex>\d+)", RegexOptions.Compiled);

            // Wiff files
            // sample=1 period=1 cycle=1 experiment=1
            // This function is not appropriate for Wiff files either because sample, period, cycle, and experiment all increment
            var reWiffFileMatcher = new Regex(@"sample=(?<Sample>\d+) period=(?<Period>\d+) cycle=(?<Cycle>\d+) experiment=(?<Experiment>\d+)", RegexOptions.Compiled);

            var reNativeIdMatchers = new List<Regex> {
                // Thermo .raw files
                // controllerType=0 controllerNumber=1 scan=15
                new(@"controllerType=(?<ControllerType>\d+) controllerNumber=(?<ControllerNumber>\d+) scan=(?<ScanNumber>\d+)", RegexOptions.Compiled),

                // Waters .raw folders
                // function=5 process=2 scan=15
                new(@"function=(?<Function>\d+) process=(?<Process>\d+) scan=(?<ScanNumber>\d+)", RegexOptions.Compiled),

                // Bruker/Agilent YEP; Bruker BAF; Bruker U2; scan number only nativeID format
                // scan=1
                new(@"scan=(?<ScanNumber>\d+)", RegexOptions.Compiled),

                // Agilent MassHunter
                // scanId=2110
                new(@"scanId=(?<ScanNumber>\d+)", RegexOptions.Compiled),

                // Shimadzu Biotech
                // start=34 end=35
                new(@"start=(?<ScanNumber>\d+) end=(?<ScanNumberEnd>\d+)", RegexOptions.Compiled),

                // Not supported: SCIEX TOF/TOF
                // @"jobRun=\d+ spotLabel=[^ ]+ spectrum=(?<Spectrum>\d+)"

            };

            Regex preferredRegEx = null;

            var spectrumIds = GetSpectrumIdList();

            // Construct a scanNumber to scanIndex mapping
            var scanNumberToIndexMap = new Dictionary<int, int>();

            for (var spectrumIndex = 0; spectrumIndex < spectrumIds.Count; spectrumIndex++)
            {
                Match match = null;

                if (preferredRegEx != null)
                {
                    match = preferredRegEx.Match(spectrumIds[spectrumIndex]);
                }

                if (match == null || !match.Success)
                {
                    foreach (var matcher in reNativeIdMatchers)
                    {
                        match = matcher.Match(spectrumIds[spectrumIndex]);
                        if (match.Success)
                        {
                            preferredRegEx = matcher;
                            break;
                        }
                    }
                }

                if (match == null || !match.Success)
                {
                    if (reIndexBasedMatcher.Match(spectrumIds[spectrumIndex]).Success)
                    {
                        throw new Exception(string.Format("Data file is a peak list file with indexed spectra but no tracked scan numbers; " +
                                                          "this function thus cannot be used; NativeId is: {0}", spectrumIds[spectrumIndex]));
                    }

                    if (reWiffFileMatcher.Match(spectrumIds[spectrumIndex]).Success)
                    {
                        throw new Exception(string.Format("Data file WIFF file and thus this function cannot be used; " +
                                                          "NativeId is: {0}", spectrumIds[spectrumIndex]));
                    }

                    throw new Exception(string.Format("NativeId did not match the expected format: {0}", spectrumIds[spectrumIndex]));
                }

                var scanNumber = int.Parse(match.Groups["ScanNumber"].Value);

                scanNumberToIndexMap.Add(scanNumber, spectrumIndex);
            }

            return scanNumberToIndexMap;
        }

        public bool HasSrmSpectra => HasSrmSpectraInList(SpectrumList);

        public bool HasDriftTimeSpectra => HasDriftTimeSpectraInList(SpectrumList);

        public bool HasChromatogramData
        {
            get
            {
                var len = ChromatogramCount;

                // Many files have just one TIC chromatogram
                if (len < 2)
                    return false;

                for (var i = 0; i < len; i++)
                {
                    var id = GetChromatogramId(i, out _);

                    if (IsSingleIonCurrentId(id))
                        return true;
                }
                return false;
            }
        }

        private static bool HasSrmSpectraInList(pwiz.CLI.msdata.SpectrumList spectrumList)
        {
            if (spectrumList == null || spectrumList.size() == 0)
                return false;

            // If the first spectrum is not SRM, the others will not be either
            using (var spectrum = spectrumList.spectrum(0, false))
            {
                return IsSrmSpectrum(spectrum);
            }
        }

        private static bool HasDriftTimeSpectraInList(pwiz.CLI.msdata.SpectrumList spectrumList)
        {
            if (spectrumList == null || spectrumList.size() == 0)
                return false;

            // Assume that if any spectra have drift times, all do
            using (var spectrum = spectrumList.spectrum(0, false))
            {
                return GetDriftTimeMsec(spectrum).HasValue;
            }
        }

        public MsDataSpectrum GetSrmSpectrum(int scanIndex)
        {
            var spectrum = GetPwizSpectrum(scanIndex, getBinaryData: true);
            return GetSpectrum(IsSrmSpectrum(spectrum) ? spectrum : null, scanIndex);
        }

        public string GetSpectrumId(int scanIndex)
        {
            return GetPwizSpectrum(scanIndex, false).id;
        }

        public bool IsCentroided(int scanIndex)
        {
            return IsCentroided(GetPwizSpectrum(scanIndex, false));
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        private static bool IsCentroided(Spectrum spectrum)
        {
            return spectrum.hasCVParam(CVID.MS_centroid_spectrum);
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        private static bool NegativePolarity(Spectrum spectrum)
        {
            var param = spectrum.cvParamChild(CVID.MS_scan_polarity);
            if (param.empty())
                return false;  // Assume positive if undeclared
            return (param.cvid == CVID.MS_negative_scan);
        }

        public bool IsSrmSpectrum(int scanIndex)
        {
            return IsSrmSpectrum(GetPwizSpectrum(scanIndex, false));
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        private static bool IsSrmSpectrum(Spectrum spectrum)
        {
            return spectrum.hasCVParam(CVID.MS_SRM_spectrum);
        }

        public int GetMsLevel(int scanIndex)
        {
            using (var spectrum = SpectrumList.spectrum(scanIndex, _detailMsLevel))
            {
                var level = GetMsLevel(spectrum);
                if (level.HasValue || _detailMsLevel == DetailLevel.FullMetadata)
                    return level ?? 0;

                // If level is not found with faster metadata methods, try the slower ones.
                if (_detailMsLevel == DetailLevel.InstantMetadata)
                    _detailMsLevel = DetailLevel.FastMetadata;
                else if (_detailMsLevel == DetailLevel.FastMetadata)
                    _detailMsLevel = DetailLevel.FullMetadata;
                return GetMsLevel(scanIndex);
            }
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        private static int? GetMsLevel(Spectrum spectrum)
        {
            var param = spectrum.cvParam(CVID.MS_ms_level);
            if (param.empty())
                return null;
            return (int)param.value;
        }

        public double? GetDriftTimeMsec(int scanIndex)
        {
            using (var spectrum = SpectrumList.spectrum(scanIndex, _detailDriftTime))
            {
                var driftTime = GetDriftTimeMsec(spectrum);
                if (driftTime.HasValue || _detailDriftTime >= DetailLevel.FullMetadata)
                    return driftTime ?? 0;

                // If level is not found with faster metadata methods, try the slower ones.
                if (_detailDriftTime == DetailLevel.InstantMetadata)
                    _detailDriftTime = DetailLevel.FastMetadata;
                else if (_detailDriftTime == DetailLevel.FastMetadata)
                    _detailDriftTime = DetailLevel.FullMetadata;
                return GetDriftTimeMsec(scanIndex);
            }
        }

        private static double? GetDriftTimeMsec(Spectrum spectrum)
        {
            if (spectrum.scanList.scans.Count == 0)
                return null;
            var scan = spectrum.scanList.scans[0];
            var driftTime = scan.cvParam(CVID.MS_ion_mobility_drift_time);
            if (driftTime.empty())
            {
                var param = scan.userParam(USER_PARAM_DRIFT_TIME); // support files with the original drift time UserParam
                if (param.empty())
                    return null;
                return param.timeInSeconds() * 1000.0;
            }

            return driftTime.timeInSeconds() * 1000.0;
        }

        public double? GetStartTime(int scanIndex)
        {
            using (var spectrum = SpectrumList.spectrum(scanIndex, _detailStartTime))
            {
                var startTime = GetStartTime(spectrum);
                if (startTime.HasValue || _detailStartTime >= DetailLevel.FullMetadata)
                    return startTime ?? 0;

                // If level is not found with faster metadata methods, try the slower ones.
                if (_detailStartTime == DetailLevel.InstantMetadata)
                    _detailStartTime = DetailLevel.FastMetadata;
                else if (_detailStartTime == DetailLevel.FastMetadata)
                    _detailStartTime = DetailLevel.FullMetadata;
                return GetStartTime(scanIndex);
            }
        }

        private static double? GetStartTime(Spectrum spectrum)
        {
            if (spectrum.scanList.scans.Count == 0)
                return null;
            var scan = spectrum.scanList.scans[0];
            var param = scan.cvParam(CVID.MS_scan_start_time);
            if (param.empty())
                return null;
            return param.timeInSeconds() / 60;
        }

        public MsTimeAndPrecursors GetInstantTimeAndPrecursors(int scanIndex)
        {
            using (var spectrum = SpectrumList.spectrum(scanIndex, DetailLevel.InstantMetadata))
            {
                return new MsTimeAndPrecursors
                {
                    Precursors = GetPrecursors(spectrum),
                    RetentionTime = GetStartTime(spectrum)
                };
            }
        }

        public MsPrecursor[] GetPrecursors(int scanIndex)
        {
            return GetPrecursors(GetPwizSpectrum(scanIndex, false));
        }

        private static MsPrecursor[] GetPrecursors(Spectrum spectrum)
        {
            var negativePolarity = NegativePolarity(spectrum);
            return spectrum.precursors.Select(p =>
                new MsPrecursor
                {
                    PrecursorMz = GetPrecursorMz(p, negativePolarity),
                    PrecursorDriftTimeMsec = GetPrecursorDriftTimeMsec(p),
                    PrecursorCollisionEnergy = GetPrecursorCollisionEnergy(p),
                    IsolationWindowTargetMz = new SignedMz(GetIsolationWindowValue(p, CVID.MS_isolation_window_target_m_z), negativePolarity),
                    IsolationWindowLower = GetIsolationWindowValue(p, CVID.MS_isolation_window_lower_offset),
                    IsolationWindowUpper = GetIsolationWindowValue(p, CVID.MS_isolation_window_upper_offset),
                    ActivationTypes = GetPrecursorActivationList(p)
                }).ToArray();
        }

        private static MsPrecursor[] GetMs1Precursors(Spectrum spectrum)
        {
            var negativePolarity = NegativePolarity(spectrum);
            return spectrum.scanList.scans[0].scanWindows.Select(s =>
                {
                    double windowStart = s.cvParam(CVID.MS_scan_window_lower_limit).value;
                    double windowEnd = s.cvParam(CVID.MS_scan_window_upper_limit).value;
                    var isolationWidth = (windowEnd - windowStart) / 2;
                    return new MsPrecursor
                    {
                        IsolationWindowTargetMz = new SignedMz(windowStart + isolationWidth, negativePolarity),
                        IsolationWindowLower = isolationWidth,
                        IsolationWindowUpper = isolationWidth
                    };
                }).ToArray();
        }

        /// <summary>
        /// Construct a list of precursor activation types used (user-friendly abbreviations)
        /// </summary>
        /// <param name="precursor"></param>
        /// <returns></returns>
        private static List<string> GetPrecursorActivationList(Precursor precursor)
        {
            var activationTypes = new List<string>();

            foreach (var cvParam in precursor.activation.cvParams)
            {
                switch (cvParam.cvid)
                {
                    case CVID.MS_CID:       // Aka MS_collision_induced_dissociation
                    case CVID.MS_in_source_collision_induced_dissociation:
                    case CVID.MS_trap_type_collision_induced_dissociation:
                        activationTypes.Add("cid");
                        break;
                    case CVID.MS_ECD:       // Aka MS_electron_capture_dissociation
                        activationTypes.Add("ecd");
                        break;
                    case CVID.MS_ETD:       // Aka MS_electron_transfer_dissociation
                        activationTypes.Add("etd");
                        break;
                    case CVID.MS_HCD:       // Aka MS_beam_type_collision_induced_dissociation
                    case CVID.MS_higher_energy_beam_type_collision_induced_dissociation:
                        activationTypes.Add("hcd");
                        break;
                    case CVID.MS_PQD:       // Aka MS_pulsed_q_dissociation
                        activationTypes.Add("pqd");
                        break;
                    case CVID.MS_SID:       // Aka MS_surface_induced_dissociation
                        activationTypes.Add("sid");
                        break;
                    case CVID.MS_MPD:       // Aka MS_multiphoton_dissociation
                        activationTypes.Add("mpd");
                        break;
                    case CVID.MS_BIRD:       // Aka MS_blackbody_infrared_radiative_dissociation
                        activationTypes.Add("bird");
                        break;
                    case CVID.MS_IRMPD:       // Aka MS_infrared_multiphoton_dissociation
                        activationTypes.Add("irmpd");
                        break;
                }
            }

            return activationTypes;
        }

        private static SignedMz GetPrecursorMz(Precursor precursor, bool negativePolarity)
        {
            // CONSIDER: Only the first selected ion m/z is considered for the precursor m/z
            var selectedIon = precursor.selectedIons.FirstOrDefault();
            if (selectedIon == null)
                return SignedMz.EMPTY;
            return new SignedMz(selectedIon.cvParam(CVID.MS_selected_ion_m_z).value, negativePolarity);
        }

        private const string USER_PARAM_DRIFT_TIME = "drift time"; // Not L10N

        // ReSharper disable once SuggestBaseTypeForParameter
        private static double? GetPrecursorDriftTimeMsec(Precursor precursor)
        {
            var param = precursor.userParam(USER_PARAM_DRIFT_TIME);  //   CONSIDER: this will eventually be a proper CVParam
            if (param.empty())
                return null;
            return param.timeInSeconds() * 1000.0;
        }

        private static double? GetPrecursorCollisionEnergy(Precursor precursor)
        {
            var param = precursor.activation.cvParam(CVID.MS_collision_energy);
            if (param.empty())
                return null;
            return (double)param.value;
        }

        private static double? GetIsolationWindowValue(Precursor precursor, CVID cvid)
        {
            var term = precursor.isolationWindow.cvParam(cvid);
            if (!term.empty())
                return term.value;
            return null;
        }

        public void Write(string path)
        {
            MSDataFile.write(_msDataFile, path);
        }

        public void Dispose()
        {
            _scanCache?.Dispose();
            _spectrumList?.Dispose();
            _spectrumList = null;
            _chromatogramList?.Dispose();
            _chromatogramList = null;
            _msDataFile?.Dispose();
            _msDataFile = null;
        }

        public string FilePath { get; }
    }

    public sealed class MsDataConfigInfo
    {
        public int Spectra { get; set; }
        public string ContentType { get; set; }
        public string IonSource { get; set; }
        public string Analyzer { get; set; }
        public string Detector { get; set; }
    }

    /// <summary>
    /// For Waters lockmass correction
    /// </summary>
    public sealed class LockMassParameters : IComparable
    {
        public LockMassParameters(double? lockmassPositive, double? lockmassNegative, double? lockmassTolerance)
        {
            LockmassPositive = lockmassPositive;
            LockmassNegative = lockmassNegative;
            if (LockmassPositive.HasValue || LockmassNegative.HasValue)
            {
                LockmassTolerance = lockmassTolerance ?? LOCKMASS_TOLERANCE_DEFAULT;
            }
            else
            {
                LockmassTolerance = null;  // Means nothing when no mz is given
            }
        }

        public double? LockmassPositive { get; }
        public double? LockmassNegative { get; }
        public double? LockmassTolerance { get; }

        public static readonly double LOCKMASS_TOLERANCE_DEFAULT = 0.1; // Per Will T
        public static readonly double LOCKMASS_TOLERANCE_MAX = 10.0;
        public static readonly double LOCKMASS_TOLERANCE_MIN = 0;

        public static readonly LockMassParameters EMPTY = new(null, null, null);

        public bool IsEmpty =>
            Math.Abs(LockmassNegative ?? 0) < float.Epsilon &&
            Math.Abs(LockmassPositive ?? 0) < float.Epsilon;

        private bool Equals(LockMassParameters other)
        {
            return LockmassPositive.Equals(other.LockmassPositive) &&
                   LockmassNegative.Equals(other.LockmassNegative) &&
                   LockmassTolerance.Equals(other.LockmassTolerance);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is LockMassParameters lockmassParams && Equals(lockmassParams);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var result = LockmassPositive.GetHashCode();
                result = (result * 397) ^ LockmassNegative.GetHashCode();
                result = (result * 397) ^ LockmassTolerance.GetHashCode();
                return result;
            }
        }

        public int CompareTo(LockMassParameters other)
        {
            if (other is null)
                return -1;
            var result = Nullable.Compare(LockmassPositive, other.LockmassPositive);
            if (result != 0)
                return result;
            result = Nullable.Compare(LockmassNegative, other.LockmassNegative);
            if (result != 0)
                return result;
            return Nullable.Compare(LockmassTolerance, other.LockmassTolerance);
        }

        public int CompareTo(object obj)
        {
            if (obj is null) return -1;
            if (ReferenceEquals(this, obj)) return 0;
            if (obj.GetType() != GetType()) return -1;
            return CompareTo((LockMassParameters)obj);
        }
    }

    /// <summary>
    /// Structure for tracking a single CVParam
    /// </summary>
    public struct CVParamData
    {
        /// <summary>
        /// CV id, e.g. 1000504
        /// </summary>
        public int CVId;

        /// <summary>
        /// CV name, e.g. MS_base_peak_m/z
        /// </summary>
        public string CVName;

        /// <summary>
        /// Param name, e.g. base peak m/z
        /// </summary>
        public string Name;

        /// <summary>
        /// Param value, e.g. 575.15087890625
        /// </summary>
        public string Value;

        /// <summary>
        /// Unit id, e.g. 1000040
        /// </summary>
        public int UnitsID;

        /// <summary>
        /// Units, e.g. m/z
        /// </summary>
        public string UnitsName;

        /// <summary>
        /// Summary of CVParam - name and value
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Name + ": " + Value;
        }
    }

    /// <summary>
    /// Class to track the CVParams and ScanWindows associated with a Scan
    /// </summary>
    public class SpectrumScanData
    {
        /// <summary>
        /// CVParams for this scan
        /// </summary>
        /// <remarks>Examples include scan start time, filter string, and ion injection time</remarks>
        public List<CVParamData> CVParams;

        /// <summary>
        /// Scan windows for this scan
        /// </summary>
        /// <remarks>Scan windows define the scan range of the data in the scan, for example 200 to 2000 m/z</remarks>
        public List<CVParamData> ScanWindowList;

        /// <summary>
        /// User parameters for this scan, stored as KeyValuePairs where key is user param name, value is user param value
        /// </summary>
        /// <remarks>Example is [Thermo Trailer Extra]Monoisotopic M/Z</remarks>
        public List<KeyValuePair<string, string>> UserParams;

        /// <summary>
        /// Constructor
        /// </summary>
        public SpectrumScanData()
        {
            CVParams = new List<CVParamData>();
            ScanWindowList = new List<CVParamData>();
            UserParams = new List<KeyValuePair<string, string>>();
        }
    }

    /// <summary>
    /// Class to track the Scans and CVParams associated with a Spectrum
    /// </summary>
    public class SpectrumScanContainer
    {
        /// <summary>
        /// CVParams for this spectrum
        /// </summary>
        /// <remarks>Examples include scan start time, filter string, and ion injection time</remarks>
        public List<CVParamData> CVParams;

        /// <summary>
        /// Scans associated with this spectrum
        /// </summary>
        /// <remarks>Typically a spectrum has just one scan</remarks>
        public List<SpectrumScanData> Scans;

        /// <summary>
        /// Constructor
        /// </summary>
        public SpectrumScanContainer()
        {
            CVParams = new List<CVParamData>();
            Scans = new List<SpectrumScanData>();
        }
    }

    /// <summary>
    /// We need a way to distinguish chromatograms for negative ion modes from those for positive.
    /// The idea of m/z is inherently "positive", in the sense of sorting etc, so we carry around
    /// the m/z value, and a sign flag, and when we sort it's by sign then by (normally positive) m/z.
    /// The m/z value *could* be negative as a result of an arithmetic operator, that has a special
    /// meaning for comparisons but doesn't happen in normal use.
    /// There's a lot of operator magic here to minimize code changes where we used to implement mz
    /// values as simple doubles.
    /// </summary>
    public readonly struct SignedMz : IComparable, IEquatable<SignedMz>, IFormattable
    {
        private readonly double? _mz;

        public SignedMz(double? mz, bool isNegative)
        {
            _mz = mz;
            IsNegative = isNegative;
        }

        public SignedMz(double? mz)  // For deserialization - a negative value is taken to mean negative polarity
        {
            IsNegative = (mz ?? 0) < 0;
            _mz = mz.HasValue ? Math.Abs(mz.Value) : (double?)null;
        }

        public static readonly SignedMz EMPTY = new(null, false);

        public static readonly SignedMz ZERO = new(0);

        // ReSharper disable once PossibleInvalidOperationException
        public double Value => _mz.Value;

        public double GetValueOrDefault()
        {
            return HasValue ? Value : 0.0;
        }

        /// <summary>
        /// For serialization etc - returns a negative number if IsNegative is true
        /// </summary>
        public double? RawValue => _mz.HasValue ? (IsNegative ? -_mz.Value : _mz.Value) : _mz;

        public bool IsNegative { get; }

        public bool HasValue => _mz.HasValue;

        public static implicit operator double(SignedMz mz)
        {
            return mz.Value;
        }

        public static implicit operator double?(SignedMz mz)
        {
            return mz._mz;
        }

        public static SignedMz operator +(SignedMz mz, double step)
        {
            return new SignedMz(mz.Value + step, mz.IsNegative);
        }

        public static SignedMz operator -(SignedMz mz, double step)
        {
            return new SignedMz(mz.Value - step, mz.IsNegative);
        }

        public static SignedMz operator +(SignedMz mz, SignedMz step)
        {
            if (mz.IsNegative != step.IsNegative)
                throw new InvalidOperationException("polarity mismatch"); // Not L10N
            return new SignedMz(mz.Value + step.Value, mz.IsNegative);
        }

        public static SignedMz operator -(SignedMz mz, SignedMz step)
        {
            if (mz.IsNegative != step.IsNegative)
                throw new InvalidOperationException("polarity mismatch"); // Not L10N
            return new SignedMz(mz.Value - step.Value, mz.IsNegative);
        }

        public static bool operator <(SignedMz mzA, SignedMz mzB)
        {
            return mzA.CompareTo(mzB) < 0;
        }

        public static bool operator <=(SignedMz mzA, SignedMz mzB)
        {
            return mzA.CompareTo(mzB) <= 0;
        }

        public static bool operator >=(SignedMz mzA, SignedMz mzB)
        {
            return mzA.CompareTo(mzB) >= 0;
        }

        public static bool operator >(SignedMz mzA, SignedMz mzB)
        {
            return mzA.CompareTo(mzB) > 0;
        }

        public static bool operator ==(SignedMz mzA, SignedMz mzB)
        {
            return mzA.CompareTo(mzB) == 0;
        }

        public static bool operator !=(SignedMz mzA, SignedMz mzB)
        {
            return !(mzA == mzB);
        }

        public bool Equals(SignedMz other)
        {
            return CompareTo(other) == 0;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            return obj is SignedMz mz && Equals(mz);
        }

        public override int GetHashCode()
        {
            return RawValue.GetHashCode();
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return Value.ToString(format, formatProvider);
        }

        public int CompareTo(object obj)
        {
            if (obj is null) return -1;
            if (obj.GetType() != GetType()) return -1;
            return CompareTo((SignedMz)obj);
        }

        public int CompareTo(SignedMz other)
        {
            if (_mz.HasValue != other.HasValue)
            {
                return _mz.HasValue ? 1 : -1;
            }
            if (IsNegative != other.IsNegative)
            {
                return IsNegative ? -1 : 1;
            }
            // Same sign
            if (_mz.HasValue)
                return Value.CompareTo(other.Value);
            return 0; // Both empty
        }

        public int CompareTolerant(SignedMz other, float tolerance)
        {
            if (_mz.HasValue != other.HasValue)
            {
                return _mz.HasValue ? 1 : -1;
            }
            if (IsNegative != other.IsNegative)
            {
                return IsNegative ? -1 : 1; // Not interested in tolerance when signs disagree
            }
            // Same sign
            if (Math.Abs(Value - other.Value) <= tolerance)
                return 0;
            return Value.CompareTo(other.Value);
        }

        public SignedMz ChangeMz(double mz)
        {
            return new SignedMz(mz, IsNegative);  // New mz, same polarity
        }
    }

    public struct MsPrecursor
    {
        public SignedMz PrecursorMz { get; set; }
        public double? PrecursorDriftTimeMsec { get; set; }
        public double? PrecursorCollisionEnergy { get; set; }
        public SignedMz IsolationWindowTargetMz { get; set; }
        public double? IsolationWindowUpper { get; set; }
        public double? IsolationWindowLower { get; set; }

        /// <summary>
        /// Activation types, like cid, etc, hcd, etc.
        /// </summary>
        public List<string> ActivationTypes { get; set; }

        public SignedMz IsolationMz
        {
            get
            {
                var targetMz = IsolationWindowTargetMz.HasValue ? IsolationWindowTargetMz : PrecursorMz;
                // If the isolation window is not centered around the target m/z, then return a
                // m/z value that is centered in the isolation window.
                if (targetMz.HasValue && IsolationWindowUpper.HasValue && IsolationWindowLower.HasValue &&
                    Math.Abs(IsolationWindowUpper.Value - IsolationWindowLower.Value) > float.Epsilon)
                {
                    return new SignedMz((targetMz * 2 + IsolationWindowUpper.Value - IsolationWindowLower.Value) / 2.0, targetMz.IsNegative);
                }

                return targetMz;
            }
        }

        public double? IsolationWidth
        {
            get
            {
                if (IsolationWindowUpper.HasValue && IsolationWindowLower.HasValue)
                {
                    var width = IsolationWindowUpper.Value + IsolationWindowLower.Value;
                    if (width > 0)
                        return width;
                }
                return null;
            }
        }
    }

    public sealed class MsTimeAndPrecursors
    {
        public double? RetentionTime { get; set; }
        public MsPrecursor[] Precursors { get; set; }
    }

    public sealed class MsDataSpectrum
    {
        public string Id { get; set; }
        public string NativeId { get; set; }
        public int Level { get; set; }
        public int Index { get; set; } // index into parent file, if any
        public double? RetentionTime { get; set; }
        public double? DriftTimeMsec { get; set; }
        public MsPrecursor[] Precursors { get; set; }
        public bool Centroided { get; set; }
        public bool NegativeCharge { get; set; } // True if negative ion mode
        public double[] Mzs { get; set; }
        public double[] Intensities { get; set; }

        public bool BinaryDataLoaded { get; set; }

        public static int WatersFunctionNumberFromId(string id)
        {
            return int.Parse(id.Split('.')[0]); // Yes, this will throw if it's not in dotted format - and that's good
        }

        public int WatersFunctionNumber => WatersFunctionNumberFromId(Id);
    }

    public sealed class MsInstrumentConfigInfo
    {
        public string Model { get; }
        public string Ionization { get; }
        public string Analyzer { get; }
        public string Detector { get; }

        public static readonly MsInstrumentConfigInfo EMPTY = new(null, null, null, null);

        public MsInstrumentConfigInfo(string model, string ionization,
                                      string analyzer, string detector)
        {
            Model = model?.Trim();
            Ionization = ionization?.Replace('\n', ' ').Trim(); // Not L10N
            Analyzer = analyzer?.Replace('\n', ' ').Trim(); // Not L10N
            Detector = detector?.Replace('\n', ' ').Trim(); // Not L10N
        }

        public bool IsEmpty =>
            string.IsNullOrEmpty(Model) &&
            string.IsNullOrEmpty(Ionization) &&
            string.IsNullOrEmpty(Analyzer) &&
            string.IsNullOrEmpty(Detector);

        #region object overrides

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(MsInstrumentConfigInfo)) return false;
            return Equals((MsInstrumentConfigInfo)obj);
        }

        public bool Equals(MsInstrumentConfigInfo other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Model, Model) &&
                Equals(other.Ionization, Ionization) &&
                Equals(other.Analyzer, Analyzer) &&
                Equals(other.Detector, Detector);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var result = 0;
                result = (result * 397) ^ (Model != null ? Model.GetHashCode() : 0);
                result = (result * 397) ^ (Ionization != null ? Ionization.GetHashCode() : 0);
                result = (result * 397) ^ (Analyzer != null ? Analyzer.GetHashCode() : 0);
                result = (result * 397) ^ (Detector != null ? Detector.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }

    /// <summary>
    /// A class to cache scans recently read from the file
    /// </summary>
    public class MsDataScanCache : IDisposable
    {
        private readonly Dictionary<int, MsDataSpectrum> _cache;
        private readonly Dictionary<int, Spectrum> _cacheNative;

        /// <summary>
        /// queue to keep track of order in which scans were added
        /// </summary>
        private readonly Queue<int> _scanStack;
        private readonly Queue<int> _scanNativeStack;
        public int Capacity { get; }
        public int Size => _scanStack.Count;
        public int SizeNative => _scanNativeStack.Count;

        public MsDataScanCache()
            : this(100)
        {
        }

        public MsDataScanCache(int cacheSize)
        {
            Capacity = cacheSize;
            _cache = new Dictionary<int, MsDataSpectrum>(Capacity);
            _cacheNative = new Dictionary<int, Spectrum>(Capacity);
            _scanStack = new Queue<int>();
            _scanNativeStack = new Queue<int>();
        }

        public bool HasScan(int scanNum)
        {
            return _cache.ContainsKey(scanNum);
        }

        public bool HasScanNative(int scanNum)
        {
            return _cacheNative.ContainsKey(scanNum);
        }

        public void Add(int scanNum, MsDataSpectrum s)
        {
            if (_scanStack.Count() >= Capacity)
            {
                _cache.Remove(_scanStack.Dequeue());
            }

            if (_cache.ContainsKey(scanNum))
            {
                _cache[scanNum] = s;
            }
            else
            {
                _cache.Add(scanNum, s);
                _scanStack.Enqueue(scanNum);
            }
        }

        public bool TryGetSpectrum(int scanNum, out Spectrum spectrum)
        {
            return _cacheNative.TryGetValue(scanNum, out spectrum);
        }

        public void Add(int scanNum, Spectrum s)
        {
            if (_scanNativeStack.Count() >= Capacity)
            {
                var index = _scanNativeStack.Dequeue();
                // Cleanup - the spectrum holds unmanaged memory resources
                _cacheNative[index].Dispose();
                _cacheNative.Remove(index);
            }

            if (_cacheNative.ContainsKey(scanNum))
            {
                _cacheNative[scanNum] = s;
            }
            else
            {
                _cacheNative.Add(scanNum, s);
                _scanNativeStack.Enqueue(scanNum);
            }
        }

        public bool TryGetSpectrum(int scanNum, out MsDataSpectrum spectrum)
        {
            return _cache.TryGetValue(scanNum, out spectrum);
        }

        public void Clear()
        {
            _cache.Clear();
            _scanStack.Clear();
            DisposeNativeSpectra();
        }

        private void DisposeNativeSpectra()
        {
            foreach (var spectrum in _cacheNative)
            {
                spectrum.Value.Dispose();
            }
            _cacheNative.Clear();
            _scanNativeStack.Clear();
        }

        public void Dispose()
        {
            DisposeNativeSpectra();
        }
    }
#pragma warning restore 1591
}
