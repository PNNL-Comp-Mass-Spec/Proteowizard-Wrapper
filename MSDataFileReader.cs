/*
 * Original author: Matthew Monroe <matthew.monroe .at. pnnl.gov>
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
using System.Linq;
using System.Threading;
using pwiz.CLI.data;
using pwiz.CLI.msdata;

// ReSharper disable UnusedMember.Global

namespace pwiz.ProteowizardWrapper
{
    /// <summary>
    /// A wrapper around the internal class that allows us to add and use an assembly resolver to
    /// find the appropriate architecture of pwiz_bindings_cli.dll from an installed version of ProteoWizard.
    /// </summary>
    public class MSDataFileReader : IDisposable
    {
        // Ignore Spelling: centroided, centroiding, lockmass, structs
        // Ignore Spelling: Bruker, Sciex, Shimadzu

        /// <summary>
        /// This static constructor ensures that the Assembly Resolver is added prior to actually using this class.
        /// </summary>
        /// <remarks>This code is executed prior to the instance constructor</remarks>
        static MSDataFileReader()
        {
            pwiz.ProteowizardWrapper.DependencyLoader.AddAssemblyResolver();
        }

        private readonly MsDataFileImpl mDataReader;

        /// <summary>
        /// Constant that corresponds to "SIM SIC "
        /// </summary>
        public static string PREFIX_PRECURSOR => MsDataFileImpl.PREFIX_PRECURSOR;

        /// <summary>
        /// Constant that corresponds to "SRM SIC "
        /// </summary>
        public static string PREFIX_SINGLE => MsDataFileImpl.PREFIX_SINGLE;

        /// <summary>
        /// Constant that corresponds to "SRM TIC "
        /// </summary>
        public static string PREFIX_TOTAL => MsDataFileImpl.PREFIX_TOTAL;

        /// <summary>
        /// Constant indicating to use the Continuous Wavelet Transform peak picker
        /// </summary>
        /// <remarks>
        /// This is high-quality peak picking, but may be slow with some high-res data.
        /// </remarks>
        public string CwtCentroiding => MsDataFileImpl.CwtCentroiding;

        /// <summary>
        /// Constant indicating to use the centroiding/peak picking algorithm that the vendor libraries provide, if available.
        /// Otherwise uses a low-quality centroiding algorithm.
        /// </summary>
        public string VendorCentroiding => MsDataFileImpl.VendorCentroiding;

        /// <summary>
        /// Number of chromatograms
        /// </summary>
        /// <remarks>See also <see cref="SpectrumCount"/></remarks>
        public int ChromatogramCount => mDataReader.ChromatogramCount;

        /// <summary>
        /// Data and Instrument Configuration information
        /// </summary>
        public MsDataConfigInfo ConfigInfo => mDataReader.ConfigInfo;

        /// <summary>
        /// The file path of the currently loaded file
        /// </summary>
        /// <remarks>
        /// This is the path provided by the calling class when <see cref="MsDataFileImpl"/> was instantiated
        /// It is thus not necessarily a full path
        /// </remarks>
        public string FilePath => mDataReader.FilePath;

        /// <summary>
        /// List of MSConvert-style filter strings to apply to the spectrum list.
        /// </summary>
        /// <remarks>If the filter count is greater than 0, the default handling of the spectrumList using the optional constructor parameters is disabled.</remarks>
        public List<string> Filters => mDataReader.Filters;

        /// <summary>
        /// Check if the file contains chromatogram data
        /// </summary>
        public bool HasChromatogramData => mDataReader.HasChromatogramData;

        /// <summary>
        /// Check if the file has drift time spectra
        /// </summary>
        [Obsolete("Use HasIonMobilitySpectra")]
        public bool HasDriftTimeSpectra => mDataReader.HasDriftTimeSpectra;

        /// <summary>
        /// Check if the file has SRR Spectra
        /// </summary>
        public bool HasSrmSpectra => mDataReader.HasSrmSpectra;

        /// <summary>
        /// If the file is an AB Sciex file
        /// </summary>
        public bool IsABFile => mDataReader.IsABFile;

        /// <summary>
        /// If the file is an Agilent file
        /// </summary>
        public bool IsAgilentFile => mDataReader.IsAgilentFile;

        /// <summary>
        /// If the file is a MzWiff file
        /// </summary>
        public bool IsMzWiffXml => mDataReader.IsMzWiffXml;

        /// <summary>
        /// If the file is a Shimadzu file
        /// </summary>
        public bool IsShimadzuFile => mDataReader.IsShimadzuFile;

        /// <summary>
        /// If the file is a Thermo .raw file
        /// </summary>
        public bool IsThermoFile => mDataReader.IsThermoFile;

        /// <summary>
        /// If the file is a Waters file
        /// </summary>
        public bool IsWatersFile => mDataReader.IsWatersFile;

        /// <summary>
        /// If the file is a candidate for Waters Lockmass Correction
        /// </summary>
        public bool IsWatersLockmassCorrectionCandidate => mDataReader.IsWatersLockmassCorrectionCandidate;

        /// <summary>
        /// The Run ID
        /// </summary>
        public string RunId => mDataReader.RunId;

        /// <summary>
        /// The run start time
        /// </summary>
        public DateTime? RunStartTime => mDataReader.RunStartTime;

        /// <summary>
        /// Get the spectrum count
        /// </summary>
        /// <remarks>See also <see cref="ChromatogramCount"/></remarks>
        public int SpectrumCount => mDataReader.SpectrumCount;

        /// <summary>
        /// When true, add CWT Centroiding to the filter list
        /// </summary>
        /// <remarks>
        /// Call <see cref="RedoFilters()"/> if calling this after reading any spectra
        /// </remarks>
        public bool UseCwtCentroiding
        {
            get => mDataReader.UseCwtCentroiding;
            set => mDataReader.UseCwtCentroiding = value;
        }

        /// <summary>
        /// When true, add Vendor Centroiding to the filter list
        /// </summary>
        /// <remarks>
        /// Call <see cref="RedoFilters()"/> if calling this after reading any spectra
        /// </remarks>
        public bool UseVendorCentroiding
        {
            get => mDataReader.UseVendorCentroiding;
            set => mDataReader.UseVendorCentroiding = value;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <remarks>
        /// This class differs from <see cref="MsDataFileImpl"/> by defaulting to treating SIM and SRM data as spectra
        /// In addition, spectrum caching is auto-enabled
        /// </remarks>
        /// <param name="path">Data file path</param>
        /// <param name="sampleIndex">Sample index to select within the data file, typically 0</param>
        /// <param name="lockmassParameters">Lock mass parameters (used for Waters datasets)</param>
        /// <param name="simAsSpectra">Whether to treat SIM data as spectra, default true</param>
        /// <param name="srmAsSpectra">Whether to treat SRM data as spectra, default true</param>
        /// <param name="acceptZeroLengthSpectra">Whether to accept zero-length spectra, default true</param>
        /// <param name="requireVendorCentroidedMS1">True to return centroided MS1 spectra</param>
        /// <param name="requireVendorCentroidedMS2">True to return centroided MS2 spectra</param>
        /// <param name="spectrumCacheSize">
        /// <para>
        /// Number of recent spectra to cache in memory to reduce disk I/O; defaults to 3
        /// </para>
        /// <para>
        /// Spectrum caching is always enabled, even if this is 0 or negative; to disable caching, call method <see cref="DisableCaching"/>
        /// </para>
        /// </param>
        public MSDataFileReader(
            string path,
            int sampleIndex = 0,
            LockMassParameters lockmassParameters = null,
            bool simAsSpectra = true,
            bool srmAsSpectra = true,
            bool acceptZeroLengthSpectra = true,
            bool requireVendorCentroidedMS1 = false,
            bool requireVendorCentroidedMS2 = false,
            int spectrumCacheSize = 3)
        {
            mDataReader = new MsDataFileImpl(path, sampleIndex, lockmassParameters, simAsSpectra, srmAsSpectra, acceptZeroLengthSpectra, requireVendorCentroidedMS1, requireVendorCentroidedMS2);
            EnableCaching(spectrumCacheSize);
        }

        /// <summary>
        /// Disable the spectrum data caching (NOTE: May result in slower reading)
        /// </summary>
        public void DisableCaching()
        {
            mDataReader.DisableCaching();
        }

        /// <summary>
        /// Enable spectrum data caching. May result in faster reading
        /// </summary>
        /// <remarks>Spectrum caching is auto-enabled when this class is instantiated</remarks>
        /// <param name="cacheSize"></param>
        public void EnableCaching(int? cacheSize)
        {
            mDataReader.EnableCaching(cacheSize);
        }

        /// <summary>
        /// Get the data for the specified chromatogram
        /// </summary>
        /// <param name="chromIndex">Chromatogram index (0-based)</param>
        /// <param name="id">Output: chromatogram description</param>
        /// <param name="timeArray">Output: time values (in minutes)</param>
        /// <param name="intensityArray">Output: Intensity values</param>
        public void GetChromatogram(int chromIndex, out string id, out float[] timeArray, out float[] intensityArray)
        {
            mDataReader.GetChromatogram(chromIndex, out id, out timeArray, out intensityArray);
        }

        /// <summary>
        /// Get the list of CVParams for the specified chromatogram (requires reference to pwiz_bindings_cli; set "copy local" to false.)
        /// </summary>
        /// <remarks>
        /// Use of this method requires the calling project to reference pwiz_bindings_cli.dll
        /// Set "Copy Local" to false to avoid breaking the DLL resolver
        /// You must also call <see cref="pwiz.ProteowizardWrapper.DependencyLoader.AddAssemblyResolver()"/> in any function that calls a function that uses this function.
        /// </remarks>
        /// <param name="chromIndex"></param>
        public CVParamList GetChromatogramCVParams(int chromIndex)
        {
            return mDataReader.GetChromatogramCVParams(chromIndex);
        }

        /// <summary>
        /// Get the NativeID of the specified chromatogram
        /// </summary>
        /// <param name="index"></param>
        /// <param name="indexId"></param>
        public string GetChromatogramId(int index, out int indexId)
        {
            return mDataReader.GetChromatogramId(index, out indexId);
        }

        /// <summary>
        /// Get the ProteoWizard native chromatogram object for the specified spectrum (requires reference to pwiz_bindings_cli; set "copy local" to false.)
        /// </summary>
        /// <remarks>
        /// Use of this method requires the calling project to reference pwiz_bindings_cli.dll
        /// Set "Copy Local" to false to avoid breaking the DLL resolver
        /// You must also call <see cref="pwiz.ProteowizardWrapper.DependencyLoader.AddAssemblyResolver()"/> in any function that calls a function that uses this function.
        /// </remarks>
        /// <param name="chromIndex"></param>
        public Chromatogram GetChromatogramObject(int chromIndex)
        {
            return mDataReader.GetChromatogramObject(chromIndex);
        }

        /// <summary>
        /// Get the drift time (in msec) of the specified spectrum
        /// </summary>
        /// <param name="spectrumIndex"></param>
        [Obsolete("Use GetIonMobility")]
        public double? GetDriftTimeMsec(int spectrumIndex)
        {
            return mDataReader.GetDriftTimeMsec(spectrumIndex);
        }

        /// <summary>
        /// Get the time and precursors for the specified spectrum
        /// </summary>
        /// <param name="spectrumIndex"></param>
        [Obsolete("Deprecated")]
        public MsTimeAndPrecursors GetInstantTimeAndPrecursors(int spectrumIndex)
        {
            return mDataReader.GetInstantTimeAndPrecursors(spectrumIndex);
        }

        /// <summary>
        /// Get any instrument info found in the file, along with any Waters lockmass info we have
        /// </summary>
        public IEnumerable<MsInstrumentConfigInfo> GetInstrumentConfigInfoList()
        {
            return mDataReader.GetInstrumentConfigInfoList();
        }

        /// <summary>
        /// Get the instrument serial number
        /// </summary>
        public string GetInstrumentSerialNumber()
        {
            return mDataReader.GetInstrumentSerialNumber();
        }

        /// <summary>
        /// Get the MS Level of the specified spectrum
        /// </summary>
        /// <param name="spectrumIndex"></param>
        public int GetMsLevel(int spectrumIndex)
        {
            return mDataReader.GetMsLevel(spectrumIndex);
        }

        /// <summary>
        /// Get the precursors for the specified spectrum
        /// </summary>
        /// <param name="spectrumIndex"></param>
        public IList<MsPrecursor> GetPrecursors(int spectrumIndex)
        {
            return mDataReader.GetPrecursors(spectrumIndex, 1);
        }

        /// <summary>
        /// Obtain a description of the specified spectrum
        /// </summary>
        /// <remarks>Comes from optional parameter "scan description", which is undefined for Thermo .raw files</remarks>
        /// <param name="spectrumIndex"></param>
        public string GetScanDescription(int spectrumIndex)
        {
            return mDataReader.GetScanDescription(spectrumIndex);
        }

        /// <summary>
        /// Lookup the scan filter text, which is most commonly used by Thermo .raw files
        /// </summary>
        /// <remarks>
        /// Example values:
        /// FTMS + p NSI Full ms [300.00-1650.00]
        /// ITMS + c NSI d Full ms2 876.39@cid35.00 [230.00-1765.00]
        /// FTMS - p NSI SIM ms [330.00-380.00]
        /// + c NSI Q3MS [400.000-1400.000]
        /// </remarks>
        /// <param name="spectrumIndex"></param>
        public string GetScanFilterText(int spectrumIndex)
        {
            var success = GetScanMetadata(spectrumIndex, out _, out _, out var filterText, out _, out _, out _);
            return success ? filterText : string.Empty;
        }

        /// <summary>
        /// Lookup various values tracked by CVParams for the given spectrum
        /// </summary>
        /// <remarks>
        /// If a spectrum has more than one scan, only returns the metadata for the first one
        /// </remarks>
        /// <param name="spectrumIndex">Spectrum index</param>
        /// <param name="scanStartTime">Output: acquisition time at scan start (in minutes)</param>
        /// <param name="ionInjectionTime">Output: ion injection time</param>
        /// <param name="filterText">Output: filter text (most commonly used by Thermo .raw files, e.g. )</param>
        /// <param name="lowMass">Output: lowest m/z</param>
        /// <param name="highMass">Output: highest m/z</param>
        /// <returns>True if the spectrum was found and has at least one scan, otherwise false</returns>
        public bool GetScanMetadata(
            int spectrumIndex,
            out double scanStartTime,
            out double ionInjectionTime,
            out string filterText,
            out double lowMass,
            out double highMass)
        {
            return GetScanMetadata(spectrumIndex, out scanStartTime, out ionInjectionTime, out filterText, out lowMass, out highMass, out _);
        }

        /// <summary>
        /// Lookup various values tracked by CVParams for the given spectrum
        /// </summary>
        /// <remarks>
        /// If a spectrum has more than one scan, only returns the metadata for the first one
        /// </remarks>
        /// <param name="spectrumIndex">Spectrum index</param>
        /// <param name="scanStartTime">Output: acquisition time at scan start (in minutes)</param>
        /// <param name="ionInjectionTime">Output: ion injection time</param>
        /// <param name="filterText">Output: filter text (most commonly used by Thermo .raw files, e.g. FTMS + p NSI Full ms [300.00-1650.00])</param>
        /// <param name="lowMass">Output: lowest m/z</param>
        /// <param name="highMass">Output: highest m/z</param>
        /// <param name="isolationWindowWidth">Output: isolation window width (typically MS2 isolation window); 0 if not applicable</param>
        /// <returns>True if the spectrum was found and has at least one scan, otherwise false</returns>
        public bool GetScanMetadata(
            int spectrumIndex,
            out double scanStartTime,
            out double ionInjectionTime,
            out string filterText,
            out double lowMass,
            out double highMass,
            out double isolationWindowWidth)
        {
            var cvScanInfo = mDataReader.GetSpectrumScanInfo(spectrumIndex);
            var precursors = GetPrecursors(spectrumIndex);

            var isolationWidths = new SortedSet<double>();

            foreach (var precursor in precursors)
            {
                if (precursor.IsolationWidth.HasValue)
                {
                    isolationWidths.Add(precursor.IsolationWidth.Value);
                }
            }

            if (isolationWidths.Count > 1)
            {
                Console.WriteLine("Scan at spectrumIndex {0} has more than one precursor, and they have differing isolation widths; will use the isolation width of the first precursor", spectrumIndex);
            }

            // Lookup details on the first scan associated with this spectrum
            // (cvScanInfo.Scans is a list, but Thermo .raw files typically have a single scan for each spectrum)
            foreach (var scanEntry in cvScanInfo.Scans)
            {
                // Prior to September 2021, we used this, which gives minutes for Thermo .raw files and seconds for Bruker .d directories
                //scanStartTime = CVParamUtilities.GetCvParamValueDbl(scanEntry.CVParams, CVParamUtilities.CVIDs.MS_scan_start_time);

                // Instead, use method GetStartTime()
                scanStartTime = mDataReader.GetStartTime(spectrumIndex).GetValueOrDefault();

                ionInjectionTime = CVParamUtilities.GetCvParamValueDbl(scanEntry.CVParams, CVParamUtilities.CVIDs.MS_ion_injection_time);
                filterText = CVParamUtilities.GetCvParamValue(scanEntry.CVParams, CVParamUtilities.CVIDs.MS_filter_string);

                lowMass = CVParamUtilities.GetCvParamValueDbl(scanEntry.ScanWindowList, CVParamUtilities.CVIDs.MS_scan_window_lower_limit);
                highMass = CVParamUtilities.GetCvParamValueDbl(scanEntry.ScanWindowList, CVParamUtilities.CVIDs.MS_scan_window_upper_limit);

                isolationWindowWidth = isolationWidths.Count > 0
                    ? isolationWidths.First()
                    : 0;

                return true;
            }

            scanStartTime = 0;
            ionInjectionTime = 0;
            filterText = string.Empty;
            lowMass = 0;
            highMass = 0;
            isolationWindowWidth = 0;

            return false;
        }

        /// <summary>
        /// Gets the retention times from the first chromatogram in the data file.
        /// Returns null if there are no chromatograms in the file.
        /// </summary>
        public double[] GetScanTimes()
        {
            return mDataReader.GetScanTimes();
        }

        /// <summary>
        /// Walks the spectrum list, and fills in the retention time and MS level of each scan.
        /// Some data files do not have any chromatograms in them, so GetScanTimes
        /// cannot be used.
        /// </summary>
        /// <remarks>See also the overloaded version that accepts a CancellationToken</remarks>
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
        public void GetScanTimesAndMsLevels(out double[] times, out byte[] msLevels, Action<int, int> progressDelegate = null, bool useAlternateMethod = false)
        {
            mDataReader.GetScanTimesAndMsLevels(out times, out msLevels, progressDelegate, useAlternateMethod);
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
            mDataReader.GetScanTimesAndMsLevels(cancellationToken, out times, out msLevels, progressDelegate, useAlternateMethod);
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
            return mDataReader.GetScanToIndexMapping();
        }

        /// <summary>
        /// Returns an MsDataSpectrum object representing the spectrum requested.
        /// </summary>
        /// <remarks>
        /// If you need direct access to CVParams, and are using MSDataFileReader, try using <see cref="GetSpectrumObject"/> instead.
        /// Alternatively, use <see cref="GetSpectrumScanInfo"/>
        /// </remarks>
        /// <param name="spectrumIndex"></param>
        /// <param name="getBinaryData"></param>
        public MsDataSpectrum GetSpectrum(int spectrumIndex, bool getBinaryData = true)
        {
            return mDataReader.GetSpectrum(spectrumIndex, getBinaryData);
        }

        /// <summary>
        /// Populate parallel arrays with m/z and intensity values
        /// </summary>
        /// <param name="spectrumIndex"></param>
        /// <param name="mzArray"></param>
        /// <param name="intensityArray"></param>
        public void GetSpectrum(int spectrumIndex, out double[] mzArray, out double[] intensityArray)
        {
            mDataReader.GetSpectrum(spectrumIndex, out mzArray, out intensityArray);
        }

        /// <summary>
        /// Get the spectrum count
        /// </summary>
        [Obsolete("Use the SpectrumCount property instead")]
        public int GetSpectrumCount()
        {
            return SpectrumCount;
        }

        /// <summary>
        /// Get the list of CVParams for the specified spectrum (requires reference to pwiz_bindings_cli; set "copy local" to false.)
        /// </summary>
        /// <remarks>
        /// Use of this method requires the calling project to reference pwiz_bindings_cli.dll
        /// Set "Copy Local" to false to avoid breaking the DLL resolver
        /// You must also call <see cref="pwiz.ProteowizardWrapper.DependencyLoader.AddAssemblyResolver()"/> in any function that calls a function that uses this function.
        /// </remarks>
        /// <param name="spectrumIndex"></param>
        /// <returns>List of CVParamData structs</returns>
        public List<CVParamData> GetSpectrumCVParamData(int spectrumIndex)
        {
            return mDataReader.GetSpectrumCVParamData(spectrumIndex);
        }

        /// <summary>
        /// Get the list of CVParams for the specified spectrum (requires reference to pwiz_bindings_cli; set "copy local" to false.)
        /// </summary>
        /// <remarks>
        /// Use of this method requires the calling project to reference pwiz_bindings_cli.dll
        /// Set "Copy Local" to false to avoid breaking the DLL resolver
        /// You must also call <see cref="pwiz.ProteowizardWrapper.DependencyLoader.AddAssemblyResolver()"/> in any function that calls a function that uses this function.
        /// Alternatively, use <see cref="GetSpectrumCVParamData"/>
        /// </remarks>
        /// <param name="spectrumIndex"></param>
        public CVParamList GetSpectrumCVParams(int spectrumIndex)
        {
            return mDataReader.GetSpectrumCVParams(spectrumIndex);
        }

        /// <summary>
        /// Get the NativeID of the specified spectrum
        /// </summary>
        /// <param name="spectrumIndex"></param>
        public string GetSpectrumId(int spectrumIndex)
        {
            return mDataReader.GetSpectrumId(spectrumIndex);
        }

        /// <summary>
        /// Get SpectrumIDs for all spectra in the run
        /// </summary>
        /// <returns>List of NativeIds</returns>
        public List<string> GetSpectrumIdList()
        {
            return mDataReader.GetSpectrumIdList();
        }

        /// <summary>
        /// Get the spectrum index of the specified NativeID
        /// </summary>
        /// <param name="id"></param>
        public int GetSpectrumIndex(string id)
        {
            return mDataReader.GetSpectrumIndex(id);
        }

        /// <summary>
        /// Get the ProteoWizard native spectrum object for the specified spectrum. (requires reference to pwiz_bindings_cli; set "copy local" to false.)
        /// </summary>
        /// <remarks>
        /// Use of this method requires the calling project to reference pwiz_bindings_cli.dll
        /// Set "Copy Local" to false to avoid breaking the DLL resolver
        /// You must also call <see cref="pwiz.ProteowizardWrapper.DependencyLoader.AddAssemblyResolver()"/> in any function that calls a function that uses this function.
        /// Alternatively, use <see cref="GetSpectrumScanInfo"/> or the GetSpectrum method that returns an <see cref="MsDataSpectrum"/> object
        /// </remarks>
        /// <param name="spectrumIndex"></param>
        public Spectrum GetSpectrumObject(int spectrumIndex)
        {
            return mDataReader.GetSpectrumObject(spectrumIndex);
        }

        /// <summary>
        /// Get a container describing the scan (or scans) associated with the given spectrum
        /// </summary>
        /// <remarks>Useful for obtaining the filter string, scan start time, ion injection time, etc.</remarks>
        /// <param name="spectrumIndex"></param>
        /// <returns>Scan info container</returns>
        public SpectrumScanContainer GetSpectrumScanInfo(int spectrumIndex)
        {
            return mDataReader.GetSpectrumScanInfo(spectrumIndex);
        }

        /// <summary>
        /// Get the specified SRM spectrum. Returns null if the specified spectrum is not SRM
        /// </summary>
        /// <param name="spectrumIndex"></param>
        public MsDataSpectrum GetSrmSpectrum(int spectrumIndex)
        {
            return mDataReader.GetSrmSpectrum(spectrumIndex);
        }

        /// <summary>
        /// Get the start time of the specified spectrum
        /// </summary>
        /// <param name="spectrumIndex"></param>
        public double? GetStartTime(int spectrumIndex)
        {
            return mDataReader.GetStartTime(spectrumIndex);
        }

        /// <summary>
        /// Return the typical NativeId for a scan number in a thermo .raw file
        /// </summary>
        /// <param name="scanNumber"></param>
        public string GetThermoNativeId(int scanNumber)
        {
            return mDataReader.GetThermoNativeId(scanNumber);
        }

        /// <summary>
        /// Get an array containing the total ion current for all scans
        /// </summary>
        public double[] GetTotalIonCurrent()
        {
            return mDataReader.GetTotalIonCurrent();
        }

        /// <summary>
        /// Return a mapping from Frame and Scan number to spectrumIndex
        /// </summary>
        /// <returns>Dictionary where keys are KeyValuePairs of Frame,Scan and values are the spectrumIndex for each scan</returns>
        public Dictionary<KeyValuePair<int, int>, int> GetUimfFrameScanPairToIndexMapping()
        {
            return mDataReader.GetUimfFrameScanPairToIndexMapping();
        }

        /// <summary>
        /// Check is the specified spectrum is centroided
        /// </summary>
        /// <param name="spectrumIndex"></param>
        public bool IsCentroided(int spectrumIndex)
        {
            return mDataReader.IsCentroided(spectrumIndex);
        }

        /// <summary>
        /// If the specified id is negative charge
        /// </summary>
        /// <param name="id"></param>
        public static bool? IsNegativeChargeIdNullable(string id)
        {
            return MsDataFileImpl.IsNegativeChargeIdNullable(id);
        }

        /// <summary>
        /// Check if the file has be processed by the specified software
        /// </summary>
        /// <param name="softwareName"></param>
        public bool IsProcessedBy(string softwareName)
        {
            return mDataReader.IsProcessedBy(softwareName);
        }

        /// <summary>
        /// If the specified id is Single Ion
        /// </summary>
        /// <param name="id"></param>
        public static bool IsSingleIonCurrentId(string id)
        {
            return MsDataFileImpl.IsSingleIonCurrentId(id);
        }

        /// <summary>
        /// Check if the specified spectrum is SRM
        /// </summary>
        /// <param name="spectrumIndex"></param>
        public bool IsSrmSpectrum(int spectrumIndex)
        {
            return mDataReader.IsSrmSpectrum(spectrumIndex);
        }

        /// <summary>
        /// If the spectrum is a Waters Lockmass spectrum
        /// </summary>
        /// <param name="s"></param>
        public bool IsWatersLockmassSpectrum(MsDataSpectrum s)
        {
            return mDataReader.IsWatersLockmassSpectrum(s);
        }

        /// <summary>
        /// Returns the file id of the specified file (as an array, which typically only has one item)
        /// </summary>
        /// <param name="path"></param>
        public static string[] ReadIds(string path)
        {
            return MsDataFileImpl.ReadIds(path);
        }

        /// <summary>
        /// Force the reload of the spectrum list, reapplying any specified filters.
        /// </summary>
        public void RedoFilters()
        {
            mDataReader.RedoFilters();
        }

        /// <summary>
        /// Look for the specified CVParam in cvParams
        /// </summary>
        /// <param name="cvParams">List of CVParams</param>
        /// <param name="cvidToFind">CVID to find</param>
        /// <param name="paramMatch">Matching parameter, or null if no match</param>
        /// <returns>True on success, false if not found</returns>
        public static bool TryGetCVParam(CVParamList cvParams, pwiz.CLI.cv.CVID cvidToFind, out CVParam paramMatch)
        {
            foreach (var param in cvParams)
            {
                if (param.cvid != cvidToFind)
                    continue;

                if (param.empty())
                    continue;

                paramMatch = param;
                return true;
            }

            paramMatch = null;
            return false;
        }

        /// <summary>
        /// Look for the specified CVParam in cvParams
        /// </summary>
        /// <param name="cvParams">List of CVParams</param>
        /// <param name="cvidToFind">CVID to find</param>
        /// <param name="value">Value of the matching param, or valueIfMissing if no match</param>
        /// <param name="valueIfMissing">Value to assign to the value argument if the parameter is not found, or if the parameter's value is not numeric</param>
        /// <returns>True on success, false if not found or not numeric</returns>
        public static bool TryGetCVParamDouble(CVParamList cvParams, pwiz.CLI.cv.CVID cvidToFind, out double value, double valueIfMissing = 0)
        {
            if (!TryGetCVParam(cvParams, cvidToFind, out var paramMatch))
            {
                value = valueIfMissing;
                return false;
            }

            try
            {
                // Try to use implicit casting
                value = paramMatch.value;
                return true;
            }
            catch
            {
                // The value could not be converted implicitly; use an explicit conversion
            }

            if (double.TryParse(paramMatch.value.ToString(), out var parsedValue))
            {
                value = parsedValue;
                return true;
            }

            value = valueIfMissing;
            return false;
        }

        /// <summary>
        /// Write the data to the specified file
        /// </summary>
        /// <param name="path"></param>
        public void Write(string path)
        {
            mDataReader.Write(path);
        }

        /// <summary>
        /// Cleanup the objects
        /// </summary>
        /// <remarks>Chains to cleanup all held unmanaged objects</remarks>
        public void Dispose()
        {
            mDataReader.Dispose();
        }
    }
}
