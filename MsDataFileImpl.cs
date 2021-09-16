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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using pwiz.CLI.cv;
using pwiz.CLI.data;
using pwiz.CLI.msdata;
using pwiz.CLI.analysis;
using pwiz.CLI.util;
using pwiz.ProteowizardWrapper.Common.Chemistry;
using pwiz.ProteowizardWrapper.Common.Collections;

// ReSharper disable ConvertToUsingDeclaration
// ReSharper disable UnusedMember.Global

namespace pwiz.ProteowizardWrapper
{
    /// <summary>
    /// <para>
    /// This is our wrapper class for ProteoWizard's MSData file reader interface.
    /// </para>
    /// <para>
    /// Performance measurements can be made here, see notes below on enabling that.
    /// </para>
    /// <para>
    /// When performance measurement is enabled, the GetLog() method can be called
    /// after read operations have been completed. This returns a handy CSV-formatted
    /// report on file read performance.
    /// </para>
    /// </summary>
    /// <remarks>
    /// This class has been customized by PNNL
    /// The original copy is at https://github.com/ProteoWizard/pwiz/tree/master/pwiz_tools/Shared/ProteowizardWrapper
    /// </remarks>
    internal class MsDataFileImpl : IDisposable
    {
        // ReSharper disable CommentTypo

        // Ignore Spelling: accessor, bspratt, centroided, centroiding, deserialization, idx, lockmass, mslevel
        // Ignore Spelling: pre, pwiz, readonly, snr, structs, typeof, wiff
        // Ignore Spelling: Biotech, Bruker, Shimadzu
        // Ignore Spelling: cid, ecd, etd, ethcd, hcd, irmpd, mpd, pqd, sid

        // ReSharper restore CommentTypo

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
        /// <remarks>Use of this method requires the calling project to reference pwiz_bindings_cli.dll</remarks>
        /// <param name="chromIndex"></param>
        public CVParamList GetChromatogramCVParams(int chromIndex)
        {
            return ChromatogramList.chromatogram(chromIndex).cvParams;
        }

        /// <summary>
        /// Get the ProteoWizard native chromatogram object for the specified spectrum
        /// </summary>
        /// <remarks>Use of this method requires the calling project to reference pwiz_bindings_cli.dll</remarks>
        /// <param name="chromIndex"></param>
        public Chromatogram GetChromatogramObject(int chromIndex)
        {
            return ChromatogramList.chromatogram(chromIndex, true);
        }

        /// <summary>
        /// Get the list of CVParams for the specified spectrum
        /// </summary>
        /// <remarks>
        /// Use of this method requires the calling project to reference pwiz_bindings_cli.dll
        /// Alternatively, use <see cref="GetSpectrumCVParamData"/>
        /// </remarks>
        /// <param name="scanIndex"></param>
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
        /// <remarks>Useful for obtaining the filter string, scan start time, ion injection time, etc.</remarks>
        /// <param name="scanIndex"></param>
        /// <returns>Scan info container</returns>
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
        /// <remarks>Use of this method requires the calling project to reference pwiz_bindings_cli.dll</remarks>
        /// <param name="scanIndex"></param>
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

            // PNNL Update:
            _spectrumListBase ??= _msDataFile.run.spectrumList;
            _msDataFile.run.spectrumList = _spectrumListBase;
        }

        #endregion

        private static readonly ReaderList FULL_READER_LIST = ReaderList.FullReaderList;

        public static IEnumerable<KeyValuePair<string, IList<string>>> GetFileExtensionsByType()
        {
            foreach (var typeExtsPair in FULL_READER_LIST.getFileExtensionsByType())
            {
                yield return typeExtsPair;
            }
        }

        public static bool SupportsVendorPeakPicking(string path)
        {
            return SpectrumList_PeakPicker.supportsVendorPeakPicking(path);
        }

        // Cached disposable objects
        private MSData _msDataFile;
        private readonly ReaderConfig _config;
        private SpectrumList _spectrumList;

        // PNNL Specific
        // For storing the unwrapped spectrumList, in case modification/unwrapping is needed
        private SpectrumList _spectrumListBase;

        private ChromatogramList _chromatogramList;
        private bool _providesConversionCCStoIonMobility;
        private SpectrumList_IonMobility.IonMobilityUnits _ionMobilityUnits;
        private SpectrumList_IonMobility _ionMobilitySpectrumList; // For Agilent and Bruker (and others, eventually?) conversion from CCS to ion mobility
        private MsDataScanCache _scanCache;

        // PNNL Disabled: private readonly IPerfUtil _perf; // for performance measurement, dummied by default

        private readonly LockMassParameters _lockmassParameters; // For Waters lockmass correction
        private int? _lockmassFunction;  // For Waters lockmass correction

        // PNNL Specific
        // Deprecated: private readonly MethodInfo _binaryDataArrayGetData;

        private readonly bool _trimNativeID;

        private DetailLevel _detailMsLevel = DetailLevel.InstantMetadata;

        private DetailLevel _detailStartTime = DetailLevel.InstantMetadata;

        private DetailLevel _detailDriftTime = DetailLevel.InstantMetadata;

        private DetailLevel _detailIonMobility = DetailLevel.InstantMetadata;

        private DetailLevel _detailLevelPrecursors = DetailLevel.InstantMetadata;

        private DetailLevel _detailScanDescription = DetailLevel.FastMetadata;

        private CVID? _cvidIonMobility;

        private double[] ToArray(BinaryDataArray binaryDataArray)
        {
            // PNNL Update:

            // Original code
            // return binaryDataArray.data.ToArray();

            // BinaryDataArray.get_data() problem fix
            // Pre-Nov. 7th, 2018 pwiz_binding_cli.dll: binaryDataArray.data returns pwiz.CLI.msdata.BinaryData, is a semi-automatic wrapper for a C++ vector, which implements IList<double>
            // Pre-Nov. 7th, 2018 pwiz_binding_cli.dll: binaryDataArray.data returns pwiz.CLI.util.BinaryData implements IList<double>, but also provides other optimization functions
            // The best way to access this before was binaryDataArray.data.ToArray()

            // Between 2019 and 2021 we used reflection to avoid issues of the ProteoWizardWrapper compiled reference vs. the ProteoWizard compiled DLL

            // var dataObj = _binaryDataArrayGetData?.Invoke(binaryDataArray, null);
            // if (dataObj is IEnumerable<double> data)
            // {
            //     return data.ToArray();
            // }
            // return new double[0];

            // However, with the update to ProteoWizard v3.0.21257 the method shown above using .Invoke() followed by .ToArray()
            // results in corrupt arrays being returned (with lots of zeros).

            // Instead, use .Storage().ToArray()

            return binaryDataArray.data.Storage().ToArray();
        }

        private static float[] ToFloatArray(IList<double> list)
        {
            var result = new float[list.Count];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = (float)list[i];
            }

            return result;
        }

        [Obsolete("Unused")]
        private float[] ToFloatArray(BinaryDataArray binaryDataArray)
        {
            // PNNL Update:

            // BinaryDataArray.get_data() problem fix
            // Pre-Nov. 7th, 2018 pwiz_binding_cli.dll: binaryDataArray.data returns pwiz.CLI.msdata.BinaryData, is a semi-automatic wrapper for a C++ vector, which implements IList<double>
            // Pre-Nov. 7th, 2018 pwiz_binding_cli.dll: binaryDataArray.data returns pwiz.CLI.util.BinaryData implements IList<double>, but also provides other optimization functions
            // The best way to access this before was binaryDataArray.data.ToArray()
            // In the future, this could be changed to binaryDataArray.data.Storage.ToArray(), but that may lead to more data copying than just using the IEnumerable<double> interface
            // Both versions implement IList<double>, so I can get the object via reflection and cast it to an IList<double> (or IEnumerable<double>).

            // Old method:
            // Call via reflection to avoid issues of the ProteoWizardWrapper compiled reference vs. the ProteoWizard compiled DLL
            // var dataObj = _binaryDataArrayGetData?.Invoke(binaryDataArray, null);

            // if (dataObj is IList<double> data)
            // {
            //     return ToFloatArray(data);
            // }

            // return new float[0];

            // Use .Storage().ToArray()
            return ToFloatArray(binaryDataArray.data.Storage());
        }

        /// <summary>
        /// Returns the file id of the specified file (as an array, which typically only has one item)
        /// </summary>
        /// <param name="path"></param>
        public static string[] ReadIds(string path)
        {
            return FULL_READER_LIST.readIds(path);
        }

        public static bool SupportsMultipleSamples(string path)
        {
            path = path.ToLowerInvariant();

            return path.EndsWith(".wiff") || path.EndsWith(".wiff2");
        }

        public const string PREFIX_TOTAL = "SRM TIC ";
        public const string PREFIX_SINGLE = "SRM SIC ";
        public const string PREFIX_PRECURSOR = "SIM SIC ";
        public const string TIC = "TIC";
        public const string BPC = "BPC";

        /// <summary>
        /// Return false if id starts with "+ "
        /// Return true  if id starts with "- "
        /// Otherwise, return null
        /// </summary>
        /// <param name="id"></param>
        public static bool? IsNegativeChargeIdNullable(string id)
        {
            if (id.StartsWith("+ "))
                return false;

            if (id.StartsWith("- "))
                return true;

            return null;
        }

        /// <summary>
        /// Return true if the id starts with "SRM SIC" or "SIM SIC"
        /// </summary>
        /// <param name="id"></param>
        public static bool IsSingleIonCurrentId(string id)
        {
            if (IsNegativeChargeIdNullable(id).HasValue)
                id = id.Substring(2);

            return id.StartsWith(PREFIX_SINGLE) || id.StartsWith(PREFIX_PRECURSOR);
        }

        public static bool ForceUncombinedIonMobility => false;

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
        /// <param name="ignoreZeroIntensityPoints"></param>
        /// <param name="preferOnlyMsLevel"></param>
        /// <param name="combineIonMobilitySpectra">When true, ask for IMS data in 3-array format (not guaranteed)</param>
        /// <param name="trimNativeId"></param>
        public MsDataFileImpl(
            string path,
            int sampleIndex = 0,
            LockMassParameters lockmassParameters = null,
            bool simAsSpectra = false,
            bool srmAsSpectra = false,
            bool acceptZeroLengthSpectra = true,
            bool requireVendorCentroidedMS1 = false,
            bool requireVendorCentroidedMS2 = false,
            bool ignoreZeroIntensityPoints = false,
            int preferOnlyMsLevel = 0,
            bool combineIonMobilitySpectra = true,
            bool trimNativeId = true)
        {
            FilePath = path;
            SampleIndex = sampleIndex;
            _msDataFile = new MSData();

            _config = new ReaderConfig
            {
                simAsSpectra = simAsSpectra,
                srmAsSpectra = srmAsSpectra,
                acceptZeroLengthSpectra = acceptZeroLengthSpectra,
                ignoreZeroIntensityPoints = ignoreZeroIntensityPoints,
                preferOnlyMsLevel = !ForceUncombinedIonMobility && combineIonMobilitySpectra ? 0 : preferOnlyMsLevel,
                allowMsMsWithoutPrecursor = false,
                combineIonMobilitySpectra = !ForceUncombinedIonMobility && combineIonMobilitySpectra,
                reportSonarBins = true, // For Waters SONAR data, report bin number instead of false drift time
                globalChromatogramsAreMs1Only = true
            };

            _lockmassParameters = lockmassParameters;

            // PNNL Update:
            InitializeReader(path, _msDataFile, sampleIndex, _config);

            RequireVendorCentroidedMs1 = requireVendorCentroidedMS1;
            RequireVendorCentroidedMs2 = requireVendorCentroidedMS2;
            _trimNativeID = trimNativeId;

            // PNNL Update:
            // BinaryDataArray.get_data() problem fix
            // Pre-Nov. 7th, 2018 pwiz_binding_cli.dll: bda.data returns pwiz.CLI.msdata.BinaryData,  is a semi-automatic wrapper for a C++ vector, which implements IList<double>
            // Pre-Nov. 7th, 2018 pwiz_binding_cli.dll: bda.data returns pwiz.CLI.util.BinaryData implements IList<double>, but also provides other optimization functions
            // The best way to access this before was bda.data.ToArray()
            // In the future, this could be changed to bda.data.Storage.ToArray(), but that may lead to more data copying than just using the IEnumerable<double> interface
            // Both versions implement IList<double>, so I can get the object via reflection and cast it to an IList<double> (or IEnumerable<double>).

            // Get the MethodInfo for BinaryDataArray.data property accessor
            // Deprecated: _binaryDataArrayGetData = typeof(BinaryDataArray).GetProperty("data", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)?.GetMethod;
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
            // PNNL Update:
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
                // Enable caching using the default cache size (100 spectra)
                _scanCache = new MsDataScanCache();
            }
            else
            {
                // Enable caching using the user-specified cache size
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

        public bool RequireVendorCentroidedMs1 { get; }

        public bool RequireVendorCentroidedMs2 { get; }

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
                {
                    return null;
                }

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
                        ionSource += ", ";
                    ionSource += instrumentIonSource;

                    if (analyzer.Length > 0)
                        analyzer += ", ";
                    analyzer += instrumentAnalyzer;

                    if (detector.Length > 0)
                        detector += ", ";
                    detector += instrumentDetector;
                }

                var contentTypeSet = new HashSet<string>();
                foreach (var term in _msDataFile.fileDescription.fileContent.cvParams)
                {
                    contentTypeSet.Add(term.name);
                }

                var contentTypes = contentTypeSet.ToArray();
                Array.Sort(contentTypes);
                var contentType = String.Join(", ", contentTypes);

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
                        {
                            ionSources.Add(c.order, term.name);
                        }
                        else
                        {
                            // If we did not find the ion source in a CVParam it may be in a UserParam
                            var uParam = c.userParam("msIonisation");

                            if (HasInfo(uParam))
                            {
                                ionSources.Add(c.order, uParam.value);
                            }
                        }
                        break;

                    case ComponentType.ComponentType_Analyzer:
                        term = c.cvParamChild(CVID.MS_mass_analyzer_type);

                        if (!term.empty())
                        {
                            analyzers.Add(c.order, term.name);
                        }
                        else
                        {
                            // If we did not find the analyzer in a CVParam it may be in a UserParam
                            var uParam = c.userParam("msMassAnalyzer");

                            if (HasInfo(uParam))
                            {
                                analyzers.Add(c.order, uParam.value);
                            }
                        }
                        break;

                    case ComponentType.ComponentType_Detector:
                        term = c.cvParamChild(CVID.MS_detector_type);

                        if (!term.empty())
                        {
                            detectors.Add(c.order, term.name);
                        }
                        else
                        {
                            // If we did not find the detector in a CVParam it may be in a UserParam
                            var uParam = c.userParam("msDetector");

                            if (HasInfo(uParam))
                            {
                                detectors.Add(c.order, uParam.value);
                            }
                        }
                        break;
                }
            }

            ionSource = String.Join("/", new List<string>(ionSources.Values).ToArray());

            analyzer = String.Join("/", new List<string>(analyzers.Values).ToArray());

            detector = String.Join("/", new List<string>(detectors.Values).ToArray());
        }

        /// <summary>
        /// Check if the file has be processed by the specified software
        /// </summary>
        /// <param name="softwareName"></param>
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
        public bool IsWatersLockmassSpectrum(MsDataSpectrum s)
        {
            return _lockmassFunction.HasValue &&
                   MsDataSpectrum.WatersFunctionNumberFromId(s.Id, s.IonMobilities != null) >= _lockmassFunction.Value;
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

                    // if instrument model free string is present, it is probably more specific than CVID model (which may only indicate manufacturer)
                    var uParam = ic.userParam("instrument model");

                    if (HasInfo(uParam))
                    {
                        instrumentModel = uParam.value;
                    }
                }

                if (instrumentModel == null)
                {
                    // If we did not find the instrument model in a CVParam it may be in a UserParam
                    var uParam = ic.userParam("msModel");

                    if (HasInfo(uParam))
                    {
                        instrumentModel = uParam.value;
                    }
                    else
                    {
                        uParam = ic.userParam("instrument model");

                        if (HasInfo(uParam))
                        {
                            instrumentModel = uParam.value;
                        }
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

        public string GetInstrumentSerialNumber()
        {
            return _msDataFile.instrumentConfigurationList.FirstOrDefault(o => o.hasCVParam(CVID.MS_instrument_serial_number))
                                                          ?.cvParam(CVID.MS_instrument_serial_number).value.ToString();
        }

        private static bool HasInfo(UserParam uParam)
        {
            return !uParam.empty() && !String.IsNullOrEmpty(uParam.value) &&
                   !string.Equals("unknown", uParam.value.ToString(), StringComparison.InvariantCultureIgnoreCase);
        }

        public static string GetCvParamName(string cvParamAccession)
        {
            return CV.cvTermInfo(cvParamAccession).shortName();
        }

        public void GetNativeIdAndFileFormat(out string nativeIdFormatAccession, out string fileFormatAccession)
        {
            var firstSource = _msDataFile.fileDescription.sourceFiles.First(source =>
                source.hasCVParamChild(CVID.MS_nativeID_format) &&
                source.hasCVParamChild(CVID.MS_file_format));
            nativeIdFormatAccession = CV.cvTermInfo(firstSource.cvParamChild(CVID.MS_nativeID_format).cvid).id;
            fileFormatAccession = CV.cvTermInfo(firstSource.cvParamChild(CVID.MS_file_format).cvid).id;
        }

        public bool IsABFile => _msDataFile.fileDescription.sourceFiles.Any(source => source.hasCVParam(CVID.MS_ABI_WIFF_format));

        public bool IsMzWiffXml => IsProcessedBy("mzWiff");

        public bool IsAgilentFile => _msDataFile.fileDescription.sourceFiles.Any(source => source.hasCVParam(CVID.MS_Agilent_MassHunter_format));

        public bool IsThermoFile => _msDataFile.fileDescription.sourceFiles.Any(source => source.hasCVParam(CVID.MS_Thermo_RAW_format));

        public bool IsWatersFile => _msDataFile.fileDescription.sourceFiles.Any(source => source.hasCVParam(CVID.MS_Waters_raw_format));

        public bool IsWatersLockmassCorrectionCandidate
        {
            get
            {
                try
                {
                    // Has to be a .raw file, not just an mzML translation of one
                    return FilePath.EndsWith(".raw", StringComparison.InvariantCultureIgnoreCase) &&
                        IsWatersFile &&
                        _msDataFile.run.spectrumList != null &&
                        !_msDataFile.run.spectrumList.empty() &&
                        !HasChromatogramData &&
                        !HasSrmSpectra;
                }
                catch (Exception)
                {
                    // Whatever that was, it wasn't a Waters file
                    return false;
                }
            }
        }

        public bool IsShimadzuFile => _msDataFile.softwareList.Any(software => software.hasCVParamChild(CVID.MS_Shimadzu_Corporation_software));

        public bool ProvidesCollisionalCrossSectionConverter => SpectrumList != null && _providesConversionCCStoIonMobility; // Checking SpectrumList provokes initialization of ionMobility info

        private SpectrumList_IonMobility IonMobilitySpectrumList => SpectrumList == null ? null : _ionMobilitySpectrumList; // Checking SpectrumList provokes initialization of ionMobility info

        public IonMobilityValue IonMobilityFromCCS(double ccs, double mz, int charge)
        {
            return IonMobilityValue.GetIonMobilityValue(IonMobilitySpectrumList.ccsToIonMobility(ccs, mz, charge), IonMobilityUnits);
        }

        public double CCSFromIonMobilityValue(IonMobilityValue ionMobilityValue, double mz, int charge)
        {
            return ionMobilityValue.Mobility.HasValue ? IonMobilitySpectrumList.ionMobilityToCCS(ionMobilityValue.Mobility.Value, mz, charge) : 0;
        }

        public eIonMobilityUnits IonMobilityUnits => _ionMobilityUnits switch
        {
            SpectrumList_IonMobility.IonMobilityUnits.none => eIonMobilityUnits.none,
            SpectrumList_IonMobility.IonMobilityUnits.drift_time_msec => eIonMobilityUnits.drift_time_msec,
            SpectrumList_IonMobility.IonMobilityUnits.inverse_reduced_ion_mobility_Vsec_per_cm2 => eIonMobilityUnits.inverse_K0_Vsec_per_cm2,
            SpectrumList_IonMobility.IonMobilityUnits.compensation_V => eIonMobilityUnits.compensation_V,
            SpectrumList_IonMobility.IonMobilityUnits.waters_sonar => eIonMobilityUnits.waters_sonar,   // Not really ion mobility, but uses IMS hardware to filter precursor m/z
            _ => throw new InvalidDataException(string.Format("unknown ion mobility type {0}", _ionMobilityUnits))
        };

        private ChromatogramList ChromatogramList => _chromatogramList ??= _msDataFile.run.chromatogramList;

        private SpectrumList SpectrumList
        {
            get
            {
                if (_spectrumList == null)
                {
                    // PNNL Update:
                    _spectrumListBase ??= _msDataFile.run.spectrumList;

                    // PNNL Update:
                    if (Filters.Count == 0)
                    {
                        // CONSIDER(bspratt): there is no acceptable wrapping order when both centroiding and lockmass are needed at the same time
                        // (For now, this can't happen in practice, as Waters offers no centroiding, but someday this may force pwiz API rework)
                        var centroidLevel = new List<int>();
                        _spectrumList = _msDataFile.run.spectrumList;
                        var hasSrmSpectra = HasSrmSpectraInList(_spectrumList);

                        if (!hasSrmSpectra)
                        {
                            if (RequireVendorCentroidedMs1)
                                centroidLevel.Add(1);

                            if (RequireVendorCentroidedMs2)
                                centroidLevel.Add(2);
                        }

                        if (centroidLevel.Count > 0 && _spectrumList != null)
                        {
                            _spectrumList = new SpectrumList_PeakPicker(_spectrumList,
                                new VendorOnlyPeakDetector(),
                                // Throws an exception when no vendor centroiding available
                                true, centroidLevel.ToArray());
                        }

                        _lockmassFunction = null;
                        if (_lockmassParameters != null && !_lockmassParameters.IsEmpty && _spectrumList != null)
                        {
                            // N.B. it's OK for lockmass wrapper to wrap centroiding wrapper, but not vice versa.
                            _spectrumList = new SpectrumList_LockmassRefiner(_spectrumList,
                                _lockmassParameters.LockmassPositive ?? 0,
                                _lockmassParameters.LockmassNegative ?? 0,
                                _lockmassParameters.LockmassTolerance ?? LockMassParameters.LOCKMASS_TOLERANCE_DEFAULT);
                        }

                        // Ion mobility info
                        if (_spectrumList != null) // No ion mobility for chromatogram-only files
                        {
                            _ionMobilitySpectrumList = new SpectrumList_IonMobility(_spectrumList);
                            _ionMobilityUnits = _ionMobilitySpectrumList.getIonMobilityUnits();
                            _providesConversionCCStoIonMobility = _ionMobilitySpectrumList.canConvertIonMobilityAndCCS(_ionMobilityUnits);
                        }

                        if (IsWatersFile && _spectrumList != null)
                        {
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
                                        // id.abbreviate() converts "function=1 process=0 scan=1" to "1.0.1"
                                        // id.abbreviate() converts "merged=1 function=1 block=1" to "1.1.1"

                                        var function =
                                            MsDataSpectrum.WatersFunctionNumberFromId(id.abbreviate(spectrum.id),
                                            HasCombinedIonMobilitySpectra && spectrum.id.Contains(MERGED_TAG));

                                        if (function > 1)
                                        {
                                            // Ignore all scans in this function for chromatogram extraction purposes
                                            _lockmassFunction = function;
                                        }
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

        public double? GetMaxIonMobility()
        {
            return GetMaxIonMobilityInList();
        }

        public bool HasCombinedIonMobilitySpectra => SpectrumList != null && IonMobilityUnits != eIonMobilityUnits.none && _ionMobilitySpectrumList != null && _ionMobilitySpectrumList.hasCombinedIonMobility();

        /// <summary>
        /// Gets the value of the MS_sample_name CV param of first sample in the MSData object, or null if there is no sample information.
        /// </summary>
        public string GetSampleId()
        {
            var samples = _msDataFile.samples;

            if (samples.Count > 0)
            {
                var sampleId = (string)samples[0].cvParam(CVID.MS_sample_name).value;

                if (sampleId.Length > 0)
                    return sampleId;
            }

            return null;
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

        private static readonly string[] msLevelOrFunctionArrayNames = { "ms level", "function" };

        public void GetChromatogram(int chromIndex, out string id,
            out float[] timeArray, out float[] intensityArray, bool onlyMs1OrFunction1 = false)
        {
            using (var chrom = ChromatogramList.chromatogram(chromIndex, true))
            {
                id = chrom.id;

                // PNNL Update:

                // Original code
                //timeArray = ToFloatArray(chrom.binaryDataArrays[0].data);
                //intensityArray = ToFloatArray(chrom.binaryDataArrays[1].data);

                // PNNL Version from 2016-2021
                // BinaryDataArray.get_data() problem fix
                // var timeArrayData = ToFloatArray(chrom.binaryDataArrays[0]);

                var timeArrayData = chrom.getTimeArray().data;

                // convert time to minutes
                var timeArrayParam = chrom.getTimeArray().cvParamChild(CVID.MS_binary_data_array);

                var timeUnitMultiple = timeArrayParam.units switch
                {
                    CVID.UO_nanosecond => 60 * 1e9f,
                    CVID.UO_microsecond => 60 * 1e6f,
                    CVID.UO_millisecond => 60 * 1e3f,
                    CVID.UO_second => 60,
                    CVID.UO_minute => 1,
                    CVID.UO_hour => 1f / 60,
                    _ => throw new InvalidDataException($"unsupported time unit in chromatogram: {timeArrayParam.unitsName}")
                };

                timeUnitMultiple = 1 / timeUnitMultiple;

                if (!onlyMs1OrFunction1)
                {
                    timeArray = new float[timeArrayData.Count];
                    for (var i = 0; i < timeArray.Length; ++i)
                    {
                        timeArray[i] = (float)timeArrayData[i] * timeUnitMultiple;
                    }

                    // PNNL Update

                    // Original code:
                    // intensityArray = ToFloatArray(chrom.binaryDataArrays[1].data);

                    // PNNL Version from 2016-2021
                    // intensityArray = ToFloatArray(chrom.binaryDataArrays[1]);

                    // Updated version:
                    intensityArray = ToFloatArray(chrom.getIntensityArray().data);
                }
                else
                {
                    // get array of ms level or function for each chromatogram point
                    var msLevelOrFunctionArray = chrom.integerDataArrays.FirstOrDefault(o =>
                        msLevelOrFunctionArrayNames.Contains(o.cvParam(CVID.MS_non_standard_data_array).value.ToString()));

                    // if array is missing or empty, return no chromatogram data points (because they could be from any ms level or function)
                    if (msLevelOrFunctionArray == null || msLevelOrFunctionArray.data.Count != chrom.binaryDataArrays[0].data.Count)
                    {
                        timeArray = intensityArray = null;
                        return;
                    }

                    var timeList = new List<float>();
                    var intensityList = new List<float>();
                    var intensityArrayData = chrom.getIntensityArray().data;
                    var msLevelOrFunctionArrayData = msLevelOrFunctionArray.data;

                    for (var i = 0; i < msLevelOrFunctionArrayData.Count; ++i)
                    {
                        if (msLevelOrFunctionArrayData[i] != 1)
                            continue;

                        timeList.Add((float)timeArrayData[i] * timeUnitMultiple);
                        intensityList.Add((float)intensityArrayData[i]);
                    }

                    timeArray = timeList.ToArray();
                    intensityArray = intensityList.ToArray();
                }
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

        private const string MERGED_TAG = "merged="; // Our cue that the scan in question represents 3-array IMS data

        public double[] GetTotalIonCurrent()
        {
            if (ChromatogramList == null)
            {
                return null;
            }

            using (var chromatogram = ChromatogramList.chromatogram(0, true))
            {
                return chromatogram?.getIntensityArray()?.data.Storage();
            }
        }

        public abstract class QcTraceQuality
        {
            public const string Pressure = "pressure";
            public const string FlowRate = "volumetric flow rate";
        }

        public abstract class QcTraceUnits
        {
            public const string PoundsPerSquareInch = "psi";
            public const string MicrolitersPerMinute = "uL/min";
        }

        public class QcTrace
        {
            public QcTrace(Chromatogram c, CVID chromatogramType)
            {
                Name = c.id;
                Index = c.index;

                if (chromatogramType == CVID.MS_pressure_chromatogram)
                {
                    MeasuredQuality = QcTraceQuality.Pressure;
                    IntensityUnits = QcTraceUnits.PoundsPerSquareInch;
                }
                else if (chromatogramType == CVID.MS_flow_rate_chromatogram)
                {
                    MeasuredQuality = QcTraceQuality.FlowRate;
                    IntensityUnits = QcTraceUnits.MicrolitersPerMinute;
                }
                else
                {
                    throw new InvalidDataException($"unsupported chromatogram type (not pressure or flow rate): {c.id}");
                }

                Times = c.getTimeArray().data.Storage();
                Intensities = c.binaryDataArrays[1].data.Storage();
            }

            public string Name { get; }
            public int Index { get; }
            public double[] Times { get; }
            public double[] Intensities { get; }
            public string MeasuredQuality { get; }
            public string IntensityUnits { get; }
        }

        public List<QcTrace> GetQcTraces()
        {
            if (ChromatogramList == null || ChromatogramList.size() == 0)
                return null;

            // some readers may return empty chromatograms at detail levels below FullMetadata
            var minDetailLevel = DetailLevel.InstantMetadata;

            if (ChromatogramList.chromatogram(0, minDetailLevel).empty())
                minDetailLevel = DetailLevel.FullMetadata;

            var result = new List<QcTrace>();
            for (var i = 0; i < ChromatogramList.size(); ++i)
            {
                CVID chromatogramType;
                using (var chromMetaData = ChromatogramList.chromatogram(i, minDetailLevel))
                {
                    chromatogramType = chromMetaData.cvParamChild(CVID.MS_chromatogram_type).cvid;

                    if (chromatogramType != CVID.MS_pressure_chromatogram &&
                        chromatogramType != CVID.MS_flow_rate_chromatogram)
                    {
                        continue;
                    }
                }

                using (var chromatogram = ChromatogramList.chromatogram(i, true))
                {
                    if (chromatogram == null)
                        return null;

                    result.Add(new QcTrace(chromatogram, chromatogramType));
                }
            }

            return result;
        }

        /// <summary>
        /// Walks the spectrum list, and fills in the retention time and MS level of each scan.
        /// Some data files do not have any chromatograms in them, so GetScanTimes
        /// cannot be used.
        /// </summary>
        /// <remarks>See also the overloaded version that accepts a CancellationToken</remarks>
        /// <param name="times">Output: scan times (in seconds)</param>
        /// <param name="msLevels">Output: MS Levels (1 for MS1, 2 for MS/MS, etc.)</param>
        /// <param name="progressDelegate">
        /// Delegate method for reporting progress while iterating over the spectra;
        /// The first value is spectra processed; the second value is total spectra
        /// </param>
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
        /// <param name="times">Output: scan times (in seconds)</param>
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
            progressDelegate ??= delegate { };

            var spectrumCount = SpectrumCount;
            times = new double[spectrumCount];
            msLevels = new byte[times.Length];

            for (var i = 0; i < times.Length; i++)
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
                    using (var spectrum = SpectrumList.spectrum(i))
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
        /// <remarks>
        /// If you need direct access to CVParams, and are using MSDataFileReader, try using "GetSpectrumObject" instead.
        /// </remarks>
        /// <param name="spectrumIndex"></param>
        /// <param name="getBinaryData"></param>
        public MsDataSpectrum GetSpectrum(int spectrumIndex, bool getBinaryData = true)
        {
            // Several PNNL Updates here

            if (_scanCache != null)
            {
                // Check the scan for this cache
                var success = _scanCache.TryGetSpectrum(spectrumIndex, out MsDataSpectrum returnSpectrum);

                if (!success || !returnSpectrum.BinaryDataLoaded && getBinaryData)
                {
                    // Spectrum not in the cache (or is in the cache but does not have binary data)
                    // Pull it from the file
                    returnSpectrum = GetSpectrum(GetPwizSpectrum(spectrumIndex, getBinaryData), spectrumIndex);

                    // add it to the cache
                    _scanCache.Add(spectrumIndex, returnSpectrum);
                }

                return returnSpectrum;
            }

            // PNNL Update
            using (var spectrum = GetPwizSpectrum(spectrumIndex, getBinaryData))
            {
                return GetSpectrum(spectrum, spectrumIndex);
            }
        }

        /// <summary>
        /// The last read spectrum index
        /// </summary>
        /// <remarks>PNNL specific</remarks>
        private int _lastRetrievedSpectrumIndex = -1;

        /// <summary>
        /// How many times a single spectrum has been read twice in a row
        /// </summary>
        /// <remarks>PNNL specific</remarks>
        private int _sequentialDuplicateSpectrumReadCount;

        /// <summary>
        /// The maximum number of time a single spectrum can be read twice in a row before we enable a cache for sanity.
        /// </summary>
        /// <remarks>PNNL specific</remarks>
        private const int DuplicateSpectrumReadThreshold = 10;

        /// <summary>
        /// Read the native ProteoWizard spectrum, using caching if enabled, and enabling a 1-spectrum cache if the number of sequential duplicate reads passes a certain threshold.
        /// </summary>
        /// <remarks>PNNL specific</remarks>
        /// <param name="spectrumIndex"></param>
        /// <param name="getBinaryData"></param>
        private Spectrum GetPwizSpectrum(int spectrumIndex, bool getBinaryData = true)
        {
            if (_scanCache != null)
            {
                var success2 = _scanCache.TryGetSpectrum(spectrumIndex, out Spectrum spectrum);

                if (!success2 || spectrum.binaryDataArrays.Count <= 1 && getBinaryData)
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

        private double[] GetIonMobilityArray(Spectrum s)
        {
            BinaryDataDouble data = null;

            // Remember where the ion mobility value came from and continue getting it from the
            // same place throughout the file. Trying to get an ion mobility value from a CVID
            // where there is none can be slow.
            if (_cvidIonMobility.HasValue)
            {
                if (_cvidIonMobility.Value != CVID.CVID_Unknown)
                    data = s.getArrayByCVID(_cvidIonMobility.Value)?.data;
            }
            else
            {
                switch (IonMobilityUnits)
                {
                    case eIonMobilityUnits.waters_sonar:
                    case eIonMobilityUnits.drift_time_msec:
                        data = TryGetIonMobilityData(s, CVID.MS_raw_ion_mobility_array, ref _cvidIonMobility);

                        if (data == null)
                        {
                            data = TryGetIonMobilityData(s, CVID.MS_mean_drift_time_array, ref _cvidIonMobility);
                            if (data == null && HasCombinedIonMobilitySpectra && !s.id.Contains(MERGED_TAG))
                            {
                                _cvidIonMobility = null; // We can't learn anything from a lockmass spectrum that has no IMS
                                return null;
                            }
                        }
                        break;

                    case eIonMobilityUnits.inverse_K0_Vsec_per_cm2:
                        data = TryGetIonMobilityData(s, CVID.MS_mean_inverse_reduced_ion_mobility_array, ref _cvidIonMobility) ??
                               TryGetIonMobilityData(s, CVID.MS_raw_inverse_reduced_ion_mobility_array, ref _cvidIonMobility);
                        break;

                        // default:
                        // throw new InvalidDataException(string.Format(@"mobility type {0} does not support ion mobility arrays", IonMobilityUnits));
                }

                if (data == null)
                    _cvidIonMobility = CVID.CVID_Unknown;
            }

            return data?.Storage();
        }

        private BinaryDataDouble TryGetIonMobilityData(Spectrum s, CVID cvid, ref CVID? cvidIonMobility)
        {
            var data = s.getArrayByCVID(cvid)?.data;

            if (data != null)
                cvidIonMobility = cvid;

            return data;
        }

        private MsDataSpectrum GetSpectrum(Spectrum spectrum, int spectrumIndex)
        {
            if (spectrum == null)
            {
                return new MsDataSpectrum
                {
                    Centroided = true,
                    Mzs = new double[0],
                    Intensities = new double[0],
                    IonMobilities = null
                };
            }

            var idText = spectrum.id;

            if (idText.Trim().Length == 0)
            {
                throw new ArgumentException(string.Format("Empty spectrum ID (and index = {0}) for scan {1}",
                    spectrum.index, spectrumIndex));
            }

            var expectIonMobilityValue = IonMobilityUnits != eIonMobilityUnits.none;

            var msDataSpectrum = new MsDataSpectrum
            {
                Id = _trimNativeID ? id.abbreviate(idText) : idText,

#pragma warning disable 618
                NativeId = idText,
#pragma warning restore 618

                Level = GetMsLevel(spectrum) ?? 0,
                Index = spectrum.index,
                RetentionTime = GetStartTime(spectrum),

#pragma warning disable 618
                DriftTimeMsec = GetDriftTimeMsec(spectrum),
#pragma warning restore 618

                PrecursorsByMsLevel = GetPrecursorsByMsLevel(spectrum),
                Centroided = IsCentroided(spectrum),
                NegativeCharge = NegativePolarity(spectrum),
                ScanDescription = GetScanDescription(spectrum)
            };

            if (IonMobilityUnits == eIonMobilityUnits.inverse_K0_Vsec_per_cm2)
            {
                var param = spectrum.scanList.scans[0].userParam("windowGroup"); // For Bruker diaPASEF
                msDataSpectrum.WindowGroup = param.empty() ? 0 : int.Parse(param.value);
            }

            if (expectIonMobilityValue)
            {
                // Note the range actually measured (for zero vs missing value determination)
                var param = spectrum.userParam("ion mobility lower limit");
                if (!param.empty())
                {
                    msDataSpectrum.IonMobilityMeasurementRangeLow = param.value;
                    param = spectrum.userParam("ion mobility upper limit");
                    msDataSpectrum.IonMobilityMeasurementRangeHigh = param.value;
                }
            }

            if (spectrum.binaryDataArrays.Count <= 1)
            {
                msDataSpectrum.Mzs = new double[0];
                msDataSpectrum.Intensities = new double[0];
                msDataSpectrum.IonMobilities = null;

                if (expectIonMobilityValue)
                {
                    msDataSpectrum.IonMobility = GetIonMobility(spectrum);
                }
                msDataSpectrum.BinaryDataLoaded = false;
            }
            else
            {
                try
                {
                    msDataSpectrum.Mzs = ToArray(spectrum.getMZArray());
                    msDataSpectrum.Intensities = ToArray(spectrum.getIntensityArray());
                    msDataSpectrum.IonMobilities = GetIonMobilityArray(spectrum);

                    if (msDataSpectrum.IonMobilities != null)
                    {
                        // One more linear walk should be fine, given how much copying and walking gets done
                        double min = double.MaxValue, max = double.MinValue;

                        foreach (var ionMobility in msDataSpectrum.IonMobilities)
                        {
                            min = Math.Min(min, ionMobility);
                            max = Math.Max(max, ionMobility);
                        }

                        msDataSpectrum.MinIonMobility = min;
                        msDataSpectrum.MaxIonMobility = max;
                    }
                    else if (expectIonMobilityValue)
                    {
                        msDataSpectrum.IonMobility = GetIonMobility(spectrum);
                    }

                    if (msDataSpectrum.Level == 1 && _config.simAsSpectra &&
                        spectrum.scanList.scans[0].scanWindows.Count > 0)
                    {
                        msDataSpectrum.Precursors = ImmutableList.ValueOf(GetMs1Precursors(spectrum));
                    }

                    msDataSpectrum.BinaryDataLoaded = true;

                    return msDataSpectrum;
                }
                catch (NullReferenceException)
                {
                    // Ignore errors here
                }
            }

            return msDataSpectrum;
        }

        /// <summary>
        /// Get SpectrumIDs for all spectra in the run
        /// </summary>
        /// <remarks>
        /// Example NativeIDs:
        /// Thermo .Raw file:      controllerType=0 controllerNumber=1 scan=1
        /// Bruker .d directory:   scan=1
        /// Waters .raw directory: function=1 process=0 scan=1
        /// Waters .raw IMS:       merged=1 function=1 block=1
        /// UIMF file:             frame=1 scan=0 frameType=1
        /// </remarks>
        /// <returns>List of NativeIds</returns>
        public List<string> GetSpectrumIdList()
        {
            // PNNL Update:

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
        public string GetThermoNativeId(int scanNumber)
        {
            // PNNL Update:
            return string.Format("controllerType=0 controllerNumber=1 scan={0}", scanNumber);
        }

        /// <summary>
        /// Return a mapping from Frame and Scan number to spectrumIndex
        /// </summary>
        /// <returns>Dictionary where keys are KeyValuePairs of Frame,Scan and values are the spectrumIndex for each scan</returns>
        public Dictionary<KeyValuePair<int, int>, int> GetUimfFrameScanPairToIndexMapping()
        {
            // PNNL Update:

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
        /// <remarks>
        /// Works for Thermo .raw files, Bruker .D directories, Bruker/Agilent .yep files, Agilent MassHunter data, Waters .raw directories, and Shimadzu data
        /// For UIMF files use <see cref="GetUimfFrameScanPairToIndexMapping"/></remarks>
        /// <returns>Dictionary where keys are scan number and values are the spectrumIndex for each scan</returns>
        public Dictionary<int, int> GetScanToIndexMapping()
        {
            // PNNL Update:

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

                // Waters .raw directories (non-IMS)
                // function=1 process=0 scan=1
                new(@"function=(?<Function>\d+) process=(?<Process>\d+) scan=(?<ScanNumber>\d+)", RegexOptions.Compiled),

                // Waters .raw directories (IMS)
                // merged=1 function=1 block=1
                // Treat block number as scan number
                new(@"merged=(?<Merged>\d+) function=(?<Function>\d+) block=(?<ScanNumber>\d+)", RegexOptions.Compiled),

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

        [Obsolete("Use HasIonMobilitySpectra")]
        public bool HasDriftTimeSpectra => HasDriftTimeSpectraInList(SpectrumList);

        public bool HasIonMobilitySpectra => HasIonMobilitySpectraInList();

        public bool HasChromatogramData
        {
            get
            {
                var len = ChromatogramCount;

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

        [Obsolete("Use HasIonMobilitySpectraInList")]
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

        private bool HasIonMobilitySpectraInList()
        {
            if (IonMobilitySpectrumList == null || IonMobilitySpectrumList.size() == 0)
                return false;

            // Assume that if any spectra have ion mobility info, all do
            using (var spectrum = IonMobilitySpectrumList.spectrum(0, false))
            {
                return GetIonMobility(spectrum).HasValue;
            }
        }

        public bool IsWatersSonarData()
        {
            if (IonMobilitySpectrumList == null || IonMobilitySpectrumList.size() == 0)
                return false;

            return IonMobilitySpectrumList.isWatersSonarData();
        }

        // Waters SONAR mode uses ion mobility hardware to filter on m/z and reports the results as bins
        public Tuple<int, int> SonarMzToBinRange(double mz, double tolerance)
        {
            int low = -1, high = -1;

            IonMobilitySpectrumList?.sonarMzToBinRange(mz, tolerance, ref low, ref high);

            return new Tuple<int, int>(low, high);
        }

        public double SonarBinToPrecursorMz(int bin)
        {
            double result = 0;
            IonMobilitySpectrumList?.sonarBinToPrecursorMz(bin, ref result); // Returns average of m/z range associated with bin, really only useful for display

            return result;
        }

        private double? GetMaxIonMobilityInList()
        {
            if (IonMobilitySpectrumList == null || IonMobilitySpectrumList.size() == 0)
                return null;

            // Assume that if any spectra have ion mobility values, all do, and all are same range
            double? maxIonMobility = null;

            for (var i = 0; i < IonMobilitySpectrumList.size(); i++)
            {
                using (var spectrum = IonMobilitySpectrumList.spectrum(i, true))
                {
                    var ionMobilities = GetIonMobilityArray(spectrum);
                    var ionMobility = ionMobilities != null
                        ? IonMobilityValue.GetIonMobilityValue(ionMobilities.Max(), IonMobilityUnits)
                        : GetIonMobility(spectrum);

                    if (!ionMobility.HasValue)
                    {
                        // Assume that if first few regular scans are without IM info, they are all without IM info
                        if (i < 20 || IsWatersLockmassSpectrum(GetSpectrum(spectrum, i)))
                            continue;  // In SONAR data, lockmass scan without IM info doesn't mean there's no IM info

                        if (!maxIonMobility.HasValue)
                            return null;
                    }

                    if (!maxIonMobility.HasValue)
                    {
                        maxIonMobility = ionMobility.Mobility;

                        if (ionMobilities != null)
                        {
                            break; // 3-array representation, we've seen the range in one go
                        }
                    }
                    else if (Math.Abs(ionMobility.Mobility ?? 0) < Math.Abs(maxIonMobility.Value))
                    {
                        break;  // We've cycled
                    }
                    else
                    {
                        maxIonMobility = ionMobility.Mobility;
                    }
                }
            }

            return maxIonMobility;
        }

        /// <summary>
        /// Highly probable that we'll look at the same scan several times for different metadata
        /// </summary>
        private int _lastScanIndex = -1;
        private DetailLevel _lastDetailLevel;
        private Spectrum _lastSpectrum;

        private Spectrum GetCachedSpectrum(int scanIndex, DetailLevel detailLevel)
        {
            if (scanIndex != _lastScanIndex || detailLevel > _lastDetailLevel)
            {
                _lastScanIndex = scanIndex;
                _lastDetailLevel = detailLevel;
                _lastSpectrum?.Dispose();
                _lastSpectrum = SpectrumList.spectrum(_lastScanIndex, _lastDetailLevel);
            }

            return _lastSpectrum;
        }

        [Obsolete("Unused")]
        private Spectrum GetCachedSpectrum(int scanIndex, bool getBinaryData)
        {
            return GetCachedSpectrum(scanIndex, getBinaryData ? DetailLevel.FullData : DetailLevel.FullMetadata);
        }

        public MsDataSpectrum GetSrmSpectrum(int scanIndex)
        {
            // PNNL Update:
            var spectrum = GetPwizSpectrum(scanIndex, getBinaryData: true);
            return GetSpectrum(IsSrmSpectrum(spectrum) ? spectrum : null, scanIndex);
        }

        public string GetSpectrumId(int scanIndex)
        {
            // PNNL Update:
            return GetPwizSpectrum(scanIndex, false).id;
        }

        public bool IsCentroided(int scanIndex)
        {
            // PNNL Update:
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

            return param.cvid == CVID.MS_negative_scan;
        }

        public bool IsSrmSpectrum(int scanIndex)
        {
            // PNNL Update:
            return IsSrmSpectrum(GetPwizSpectrum(scanIndex, false));
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        private static bool IsSrmSpectrum(Spectrum spectrum)
        {
            return spectrum.hasCVParam(CVID.MS_SRM_spectrum);
        }

        public TVal GetMetaDataValue<TVal>(int scanIndex, Func<Spectrum, TVal> getValue, Func<TVal, bool> isUsableValue,
            Func<TVal, TVal> returnValue, ref DetailLevel detailLevel, DetailLevel maxDetailLevel = DetailLevel.FullMetadata)
        {
            var spectrum = GetCachedSpectrum(scanIndex, detailLevel);
            var val = getValue(spectrum);

            if (isUsableValue(val) || detailLevel >= maxDetailLevel)
                return returnValue(val);

            // If level is not found with faster metadata methods, try the slower ones.
            if (detailLevel < maxDetailLevel)
                detailLevel++;

            return GetMetaDataValue(scanIndex, getValue, isUsableValue, returnValue, ref detailLevel);
        }

        public int GetMsLevel(int scanIndex)
        {
            return (int)GetMetaDataValue(scanIndex, GetMsLevel, v => v.HasValue, v => v ?? 0, ref _detailMsLevel);
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        private static int? GetMsLevel(Spectrum spectrum)
        {
            var param = spectrum.cvParam(CVID.MS_ms_level);

            if (param.empty())
                return null;

            return (int)param.value;
        }

        [Obsolete("Use GetIonMobility")]
        public double? GetDriftTimeMsec(int scanIndex)
        {
            using (var spectrum = SpectrumList.spectrum(scanIndex, _detailDriftTime))
            {
                var driftTime = GetDriftTimeMsec(spectrum);

                if (driftTime.HasValue || _detailDriftTime >= DetailLevel.FullMetadata)
                    return driftTime ?? 0;

                // If level is not found with faster metadata methods, try the slower ones.
                if (_detailDriftTime == DetailLevel.InstantMetadata)
                {
                    _detailDriftTime = DetailLevel.FastMetadata;
                }
                else if (_detailDriftTime == DetailLevel.FastMetadata)
                {
                    _detailDriftTime = DetailLevel.FullMetadata;
                }

                return GetDriftTimeMsec(scanIndex);
            }
        }

        [Obsolete("Use GetIonMobility")]
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

        public string GetScanDescription(int scanIndex)
        {
            return GetMetaDataValue(scanIndex, GetScanDescription, string.IsNullOrEmpty, v => v, ref _detailScanDescription, DetailLevel.FastMetadata);
        }

        private static string GetScanDescription(Spectrum spectrum)
        {
            const string USER_PARAM_SCAN_DESCRIPTION = "scan description";
            var param = spectrum.userParam(USER_PARAM_SCAN_DESCRIPTION);

            if (param.empty())
                return null;

            return param.value.ToString().Trim();
        }

        public IonMobilityValue GetIonMobility(int scanIndex) // for non-combined-mode IMS
        {
            return GetMetaDataValue(scanIndex, GetIonMobility, v => v != null && v.HasValue, v => v, ref _detailIonMobility);
        }

        private IonMobilityValue GetIonMobility(Spectrum spectrum) // for non-combined-mode IMS
        {
            if (IonMobilityUnits == eIonMobilityUnits.none || spectrum.scanList.scans.Count == 0)
                return IonMobilityValue.EMPTY;

            var scan = spectrum.scanList.scans[0];
            double value;
            var expectedUnits = IonMobilityUnits;

            switch (expectedUnits)
            {
                case eIonMobilityUnits.drift_time_msec:
                    var driftTime = scan.cvParam(CVID.MS_ion_mobility_drift_time);

                    if (driftTime.empty())
                    {
                        var param = scan.userParam(USER_PARAM_DRIFT_TIME); // support files with the original drift time UserParam
                        if (param.empty())
                            return IonMobilityValue.EMPTY;

                        value = param.timeInSeconds() * 1000.0;
                    }
                    else
                    {
                        value = driftTime.timeInSeconds() * 1000.0;
                    }

                    return IonMobilityValue.GetIonMobilityValue(value, expectedUnits);

                case eIonMobilityUnits.inverse_K0_Vsec_per_cm2:
                    var irim = scan.cvParam(CVID.MS_inverse_reduced_ion_mobility);

                    if (irim.empty())
                    {
                        return IonMobilityValue.EMPTY;
                    }

                    value = irim.value;

                    return IonMobilityValue.GetIonMobilityValue(value, expectedUnits);

                case eIonMobilityUnits.compensation_V:
                    var faims = spectrum.cvParam(CVID.MS_FAIMS_compensation_voltage);

                    if (faims.empty())
                    {
                        return IonMobilityValue.EMPTY;
                    }

                    value = faims.value;

                    return IonMobilityValue.GetIonMobilityValue(value, expectedUnits);

                default:
                    return IonMobilityValue.EMPTY;
            }
        }

        public double? GetStartTime(int scanIndex)
        {
            return GetMetaDataValue(scanIndex, GetStartTime, v => v.HasValue, v => v ?? 0, ref _detailStartTime);
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

        [Obsolete("Deprecated")]
        public MsTimeAndPrecursors GetInstantTimeAndPrecursors(int scanIndex)
        {
            using (var spectrum = SpectrumList.spectrum(scanIndex, DetailLevel.InstantMetadata))
            {
                return new MsTimeAndPrecursors
                {
                    Precursors = GetPrecursorsOld(spectrum),
                    RetentionTime = GetStartTime(spectrum)
                };
            }
        }

        [Obsolete("Use GetPrecursors that accepts level")]
        public MsPrecursor[] GetPrecursor(int scanIndex)
        {
            return GetPrecursorsOld(GetPwizSpectrum(scanIndex, false));
        }

        [Obsolete("Use GetPrecursors that accepts level")]
        private static MsPrecursor[] GetPrecursorsOld(Spectrum spectrum)
        {
            var negativePolarity = NegativePolarity(spectrum);

            return spectrum.precursors.Select(p =>
                new MsPrecursor
                {
                    PrecursorMz = GetPrecursorMz(p, negativePolarity),
                    PrecursorDriftTimeMsec = GetPrecursorDriftTimeMsec(p),
                    PrecursorCollisionEnergy = GetPrecursorCollisionEnergy(p),
                    IsolationWindowTargetMz = new SignedMz(GetIsolationWindowValue(p, CVID.MS_isolation_window_target_m_z) ?? 0, negativePolarity),
                    IsolationWindowLower = GetIsolationWindowValue(p, CVID.MS_isolation_window_lower_offset),
                    IsolationWindowUpper = GetIsolationWindowValue(p, CVID.MS_isolation_window_upper_offset),
                    ActivationTypes = GetPrecursorActivationList(p)
                }).ToArray();
        }

        /// <summary>
        /// Construct a list of precursor activation types used (user-friendly abbreviations)
        /// </summary>
        /// <param name="precursor"></param>
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

                    case CVID.MS_PD:        // Aka MS_plasma_desorption
                        activationTypes.Add("pd");
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

                    case CVID.MS_EThcD:
                        activationTypes.Add("ethcd");
                        break;

                        // Future:
                        // case CVID.MS_ETciD:    // Corresponds to MS:1003182
                        //    activationTypes.Add("ETciD");
                        //    break;
                }
            }

            return activationTypes;
        }

        /// <summary>
        /// Get the precursor ion (or ions) for the spectrum at the given index
        /// </summary>
        /// <remarks>MS1 spectra will always return an empty list</remarks>
        /// <param name="scanIndex"></param>
        /// <param name="level"></param>
        public IList<MsPrecursor> GetPrecursors(int scanIndex, int level)
        {
            if (GetMsLevel(scanIndex) < 2)
                return ImmutableList.Empty<MsPrecursor>();

            return GetMetaDataValue(scanIndex, s => GetPrecursors(s, level), v => v.Count > 0, v => v, ref _detailLevelPrecursors);
        }

        private IList<MsPrecursor> GetPrecursors(Spectrum spectrum, int level)
        {
            // return precursors with highest ms level
            var precursorsByMsLevel = GetPrecursorsByMsLevel(spectrum);

            if (level > precursorsByMsLevel.Count)
                return ImmutableList.Empty<MsPrecursor>();

            if (level < 1)
                level = 1;

            return precursorsByMsLevel[level - 1];
        }

        private static ImmutableList<ImmutableList<MsPrecursor>> GetPrecursorsByMsLevel(Spectrum spectrum)
        {
            var negativePolarity = NegativePolarity(spectrum);
            var count = spectrum.precursors.Count;

            if (count == 0)
                return ImmutableList<ImmutableList<MsPrecursor>>.EMPTY;

            // Most MS/MS spectra will have a single MS1 precursor
            if (spectrum.precursors.Count == 1 && GetMsLevel(spectrum.precursors[0]) == 1)
            {
                var msPrecursor = CreatePrecursor(spectrum.precursors[0], negativePolarity);
                return ImmutableList.Singleton(ImmutableList.Singleton(msPrecursor));
            }

            return ImmutableList.ValueOf(GetPrecursorsByMsLevel(spectrum.precursors, negativePolarity));
        }

        private static IEnumerable<ImmutableList<MsPrecursor>> GetPrecursorsByMsLevel(PrecursorList precursors, bool negativePolarity)
        {
            var level = 0;
            foreach (var group in precursors.GroupBy(GetMsLevel).OrderBy(g => g.Key))
            {
                var msLevel = group.Key;

                while (++level < msLevel)
                {
                    yield return ImmutableList<MsPrecursor>.EMPTY;
                }

                yield return ImmutableList.ValueOf(group.Select(p =>
                    CreatePrecursor(p, negativePolarity)));
            }
        }

        private static MsPrecursor CreatePrecursor(Precursor p, bool negativePolarity)
        {
            return new MsPrecursor
            {
                PrecursorMz = GetPrecursorMz(p, negativePolarity),
                PrecursorCollisionEnergy = GetPrecursorCollisionEnergy(p),
                IsolationWindowTargetMz =
                    GetSignedMz(GetIsolationWindowValue(p, CVID.MS_isolation_window_target_m_z),
                        negativePolarity),
                IsolationWindowLower = GetIsolationWindowValue(p, CVID.MS_isolation_window_lower_offset),
                IsolationWindowUpper = GetIsolationWindowValue(p, CVID.MS_isolation_window_upper_offset),
                ActivationTypes = GetPrecursorActivationList(p)
            };
        }

        private static int GetMsLevel(Precursor precursor)
        {
            var msLevelParam = precursor.isolationWindow.userParam("ms level");

            if (msLevelParam.empty())
                msLevelParam = precursor.userParam("ms level");

            return msLevelParam.empty() ? 1 : (int)msLevelParam.value;
        }

        [Obsolete("Unused")]
        private static int? GetChargeStateValue(Precursor precursor)
        {
            if (precursor.selectedIons == null || precursor.selectedIons.Count == 0)
                return null;

            var param = precursor.selectedIons[0].cvParam(CVID.MS_charge_state);

            if (param.empty())
                return null;

            return (int)param.value;
        }

        private static IEnumerable<MsPrecursor> GetMs1Precursors(Spectrum spectrum)
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
            });
        }

        private static SignedMz? GetPrecursorMz(Precursor precursor, bool negativePolarity)
        {
            // CONSIDER: Only the first selected ion m/z is considered for the precursor m/z
            var selectedIon = precursor.selectedIons.FirstOrDefault();

            if (selectedIon == null)
                return null;

            return GetSignedMz(selectedIon.cvParam(CVID.MS_selected_ion_m_z).value, negativePolarity);
        }

        private static SignedMz? GetSignedMz(double? mz, bool negativePolarity)
        {
            if (mz.HasValue)
                return new SignedMz(mz.Value, negativePolarity);

            return null;
        }

        private const string USER_PARAM_DRIFT_TIME = "drift time";

        // ReSharper disable once SuggestBaseTypeForParameter
        [Obsolete("Deprecated")]
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
            _lastSpectrum?.Dispose();
            _lastScanIndex = -1;
            _spectrumList?.Dispose();
            _spectrumList = null;
            _chromatogramList?.Dispose();
            _chromatogramList = null;
            _ionMobilitySpectrumList?.Dispose();
            _ionMobilitySpectrumList = null;
            _msDataFile?.Dispose();
            _msDataFile = null;
        }

        /// <summary>
        /// Path to the file provided to the constructor for this class
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Sample index, typically 0
        /// </summary>
        public int SampleIndex { get; }

        /// <summary>
        /// Returns true if the file can be successfully opened
        /// </summary>
        public static bool IsValidFile(string filepath)
        {
            if (!File.Exists(filepath))
                return false;

            try
            {
                var msd = new MSData();
                FULL_READER_LIST.read(filepath, msd);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
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

        // Ignoring tolerance here, which means nothing when no mz is given
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
        // PNNL Update:

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
        // PNNL Update:

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
        // PNNL Update:

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
    /// Information about a precursor ion or isolation window
    /// </summary>
    public struct MsPrecursor
    {
        /// <summary>
        /// Precursor m/z
        /// </summary>
        public SignedMz? PrecursorMz { get; set; }

        /// <summary>
        /// Charge state
        /// </summary>
        /// <remarks>Null if unknown</remarks>
        public int? ChargeState { get; set; }

        /// <summary>
        /// Drift time, in msec
        /// </summary>
        /// <remarks>Null if unknown</remarks>
        public double? PrecursorDriftTimeMsec { get; set; }

        /// <summary>
        /// Collision energy
        /// </summary>
        /// <remarks>Null if unknown</remarks>
        public double? PrecursorCollisionEnergy { get; set; }

        /// <summary>
        /// Central m/z of the isolation window for MS/MS spectra
        /// </summary>
        /// <remarks>Null if unknown or not applicable</remarks>
        public SignedMz? IsolationWindowTargetMz { get; set; }

        /// <summary>
        /// Add this to IsolationWindowTargetMz to get window upper bound
        /// </summary>
        public double? IsolationWindowUpper { get; set; }

        /// <summary>
        /// Subtract this from IsolationWindowTargetMz to get window lower bound
        /// </summary>
        public double? IsolationWindowLower { get; set; }

        /// <summary>
        /// Activation types, like cid, etc, hcd, etc.
        /// </summary>
        public List<string> ActivationTypes { get; set; }

        /// <summary>
        /// Returns IsolationWindowTargetMz if not null, otherwise PrecursorMz
        /// </summary>
        public SignedMz? IsolationMz
        {
            get
            {
                var targetMz = IsolationWindowTargetMz ?? PrecursorMz;

                // If the isolation window is not centered around the target m/z, return a
                // m/z value that is centered in the isolation window.
                if (targetMz.HasValue && IsolationWindowUpper.HasValue && IsolationWindowLower.HasValue &&
                    Math.Abs(IsolationWindowUpper.Value - IsolationWindowLower.Value) > float.Epsilon)
                {
                    return new SignedMz((targetMz.Value * 2 + IsolationWindowUpper.Value - IsolationWindowLower.Value) / 2.0, targetMz.Value.IsNegative);
                }

                return targetMz;
            }
        }

        /// <summary>
        /// Returns the isolation width if <see cref="IsolationWindowUpper"/> and <see cref="IsolationWindowLower"/> have values and are not equal
        /// Otherwise, returns null
        /// </summary>
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

    [Obsolete("Deprecated")]
    public sealed class MsTimeAndPrecursors
    {
        public double? RetentionTime { get; set; }
        public MsPrecursor[] Precursors { get; set; }
    }

    /// <summary>
    /// Information about a mass spectrum
    /// </summary>
    public sealed class MsDataSpectrum
    {
        private IonMobilityValue _ionMobility;

        /// <summary>
        /// Spectrum Id
        /// </summary>
        /// <remarks>
        /// For Thermo .raw files, the first spectrum has Id = 0.1.1 and Index = 0
        /// </remarks>
        public string Id { get; set; }

        /// <summary>
        /// Spectrum Native Id
        /// </summary>
        [Obsolete("Superseded by Id")]
        public string NativeId { get; set; }

        /// <summary>
        /// 1 if MS, 2 if MS/MS, etc.
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// Index into parent file, if any
        /// </summary>
        /// <remarks>
        /// The first spectrum has Index = 0
        /// </remarks>
        public int Index { get; set; }

        /// <summary>
        /// Retention time, aka elution time
        /// </summary>
        public double? RetentionTime { get; set; }

        /// <summary>
        /// Ion mobility drift time, in msec
        /// </summary>
        [Obsolete("Deprecated")]
        public double? DriftTimeMsec { get; set; }

        /// <summary>
        /// For non-combined-mode IMS
        /// </summary>
        public IonMobilityValue IonMobility
        {
            get => _ionMobility ?? IonMobilityValue.EMPTY;
            set => _ionMobility = value;
        }

        /// <summary>
        /// Minimum of the range of ion mobilities that were scanned (for zero vs missing value determination)
        /// </summary>
        public double? IonMobilityMeasurementRangeLow { get; set; }

        /// <summary>
        /// Maximum of the range of ion mobilities that were scanned (for zero vs missing value determination)
        /// </summary>
        public double? IonMobilityMeasurementRangeHigh { get; set; }

        /// <summary>
        /// Get the precursors for spectra at the given level
        /// </summary>
        /// <param name="level"></param>
        /// <returns>List of precursor values</returns>
        public ImmutableList<MsPrecursor> GetPrecursorsByMsLevel(int level)
        {
            if (PrecursorsByMsLevel == null || level > PrecursorsByMsLevel.Count)
                return ImmutableList<MsPrecursor>.EMPTY;

            if (level < 1)
                level = 1;

            return PrecursorsByMsLevel[level - 1];
        }

        /// <summary>
        /// <para>
        /// Tracks precursors, by MS Level
        /// </para>
        /// <para>
        /// MS1 spectra are at PrecursorsByMsLevel[0]
        /// MS2 spectra are at PrecursorsByMsLevel[1]
        /// </para>
        /// </summary>
        /// <remarks>
        /// The precursor of an MS1 spectrum is the central m/z value of the entire mass spectrum
        /// </remarks>
        public ImmutableList<ImmutableList<MsPrecursor>> PrecursorsByMsLevel { get; set; }

        /// <summary>
        /// List of precursors for this spectrum
        /// </summary>
        /// <remarks>
        /// The precursor of an MS1 spectrum is the central m/z value of the entire mass spectrum
        /// </remarks>
        public ImmutableList<MsPrecursor> Precursors
        {
            get
            {
                if (PrecursorsByMsLevel == null || PrecursorsByMsLevel.Count == 0)
                    return ImmutableList<MsPrecursor>.EMPTY;

                return GetPrecursorsByMsLevel(PrecursorsByMsLevel.Count);
            }
            set => PrecursorsByMsLevel = ImmutableList.Singleton(ImmutableList.ValueOf(value));
        }

        /// <summary>
        /// True if the m/z values have been centroided (peak-picked)
        /// </summary>
        public bool Centroided { get; set; }

        /// <summary>
        /// True if negative ion mode
        /// </summary>
        public bool NegativeCharge { get; set; }

        /// <summary>
        /// List of m/z values
        /// </summary>
        public double[] Mzs { get; set; }

        /// <summary>
        /// List of intensity values
        /// </summary>
        public double[] Intensities { get; set; }

        /// <summary>
        /// For combined-mode IMS (may be null)
        /// </summary>
        public double[] IonMobilities { get; set; }

        /// <summary>
        /// Minimum ion mobility, or null if not applicable
        /// </summary>
        public double? MinIonMobility { get; set; }

        /// <summary>
        /// Maximum ion mobility, or null if not applicable
        /// </summary>
        public double? MaxIonMobility { get; set; }

        /// <summary>
        /// For Bruker diaPASEF
        /// </summary>
        public int WindowGroup { get; set; }

        /// <summary>
        /// True if the spectrum includes binary data
        /// </summary>
        /// <remarks>PNNL Specific</remarks>
        public bool BinaryDataLoaded { get; set; }

        /// <summary>
        /// Scan description
        /// </summary>
        /// <remarks>Null for Thermo .raw files</remarks>
        public string ScanDescription { get; set; }

        /// <summary>
        /// For a Waters dataset, determine the function number from the spectrum Id
        /// </summary>
        /// <remarks>
        /// Throws an exception if the id is not in the form 1.1.1
        /// </remarks>
        /// <param name="id">Spectrum Id, in dotted format</param>
        /// <param name="isCombinedIonMobility">
        /// If false, assume the first integer in Id is the function number
        /// Otherwise, assumes the second integer is the function number
        /// </param>
        /// <returns>Function number</returns>
        public static int WatersFunctionNumberFromId(string id, bool isCombinedIonMobility)
        {
            return int.Parse(id.Split('.')[isCombinedIonMobility ? 1 : 0]);
        }

        /// <summary>
        /// For a Waters dataset, determine the function number from the spectrum Id, assuming the file does not include combined ion mobility values
        /// </summary>
        [Obsolete("Deprecated")]
        public int WatersFunctionNumber => WatersFunctionNumberFromId(Id, false);

        /// <summary>
        /// Description of the spectrum, for debugging purposes
        /// </summary>
        public override string ToString()
        {
            return $"id={Id} idx={Index} mslevel={Level} rt={RetentionTime}";
        }
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
            Ionization = ionization?.Replace('\n', ' ').Trim();
            Analyzer = analyzer?.Replace('\n', ' ').Trim();
            Detector = detector?.Replace('\n', ' ').Trim();
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
                var result = Model?.GetHashCode() ?? 0; // N.B. generated code starts with result = 0, which causes an inspection warning
                result = (result * 397) ^ (Ionization?.GetHashCode() ?? 0);
                result = (result * 397) ^ (Analyzer?.GetHashCode() ?? 0);
                result = (result * 397) ^ (Detector?.GetHashCode() ?? 0);

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

        // PNNL Specific
        private readonly Dictionary<int, Spectrum> _cacheNative;

        /// <summary>
        /// queue to keep track of order in which scans were added
        /// </summary>
        private readonly Queue<int> _scanStack;

        // PNNL Specific
        private readonly Queue<int> _scanNativeStack;

        public int Capacity { get; }

        public int Size => _scanStack.Count;

        // PNNL Specific
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

        // PNNL Specific
        public bool HasScanNative(int scanNum)
        {
            return _cacheNative.ContainsKey(scanNum);
        }

        public void Add(int scanNum, MsDataSpectrum s)
        {
            if (_scanStack.Count >= Capacity)
            {
                _cache.Remove(_scanStack.Dequeue());
            }

            // PNNL Update
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

        // PNNL Specific
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

        // PNNL Specific
        public bool TryGetSpectrum(int scanNum, out Spectrum spectrum)
        {
            return _cacheNative.TryGetValue(scanNum, out spectrum);
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

        // PNNL Specific
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
}
