﻿/*
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
        /// <summary>
        /// This static constructor ensures that the Assembly Resolver is added prior to actually using this class.
        /// </summary>
        /// <remarks>This code is executed prior to the instance constructor</remarks>
        static MSDataFileReader()
        {
            pwiz.ProteowizardWrapper.DependencyLoader.AddAssemblyResolver();
        }

        /// <summary>
        /// Get the list of CVParams for the specified chromatogram (requires reference to pwiz_bindings_cli; set "copy local" to false.)
        /// </summary>
        /// <param name="chromIndex"></param>
        /// <returns></returns>
        /// <remarks>
        /// Use of this method requires the calling project to reference pwiz_bindings_cli.dll
        /// Set "Copy Local" to false to avoid breaking the DLL resolver
        /// You must also call <see cref="pwiz.ProteowizardWrapper.DependencyLoader.AddAssemblyResolver()"/> in any function that calls a function that uses this function.
        /// </remarks>
        public CVParamList GetChromatogramCVParams(int chromIndex)
        {
            return mDataReader.GetChromatogramCVParams(chromIndex);
        }

        /// <summary>
        /// Get the ProteoWizard native chromatogram object for the specified spectrum (requires reference to pwiz_bindings_cli; set "copy local" to false.)
        /// </summary>
        /// <param name="chromIndex"></param>
        /// <returns></returns>
        /// <remarks>
        /// Use of this method requires the calling project to reference pwiz_bindings_cli.dll
        /// Set "Copy Local" to false to avoid breaking the DLL resolver
        /// You must also call <see cref="pwiz.ProteowizardWrapper.DependencyLoader.AddAssemblyResolver()"/> in any function that calls a function that uses this function.
        /// </remarks>
        public Chromatogram GetChromatogramObject(int chromIndex)
        {
            return mDataReader.GetChromatogramObject(chromIndex);
        }

        /// <summary>
        /// Get the list of CVParams for the specified spectrum (requires reference to pwiz_bindings_cli; set "copy local" to false.)
        /// </summary>
        /// <param name="scanIndex"></param>
        /// <returns></returns>
        /// <remarks>
        /// Use of this method requires the calling project to reference pwiz_bindings_cli.dll
        /// Set "Copy Local" to false to avoid breaking the DLL resolver
        /// You must also call <see cref="pwiz.ProteowizardWrapper.DependencyLoader.AddAssemblyResolver()"/> in any function that calls a function that uses this function.
        /// Alternatively, use <see cref="GetSpectrumCVParamData"/>
        /// </remarks>
        public CVParamList GetSpectrumCVParams(int scanIndex)
        {
            return mDataReader.GetSpectrumCVParams(scanIndex);
        }

        /// <summary>
        /// Get the list of CVParams for the specified spectrum (requires reference to pwiz_bindings_cli; set "copy local" to false.)
        /// </summary>
        /// <param name="scanIndex"></param>
        /// <returns>List of CVParamData structs</returns>
        /// <remarks>
        /// Use of this method requires the calling project to reference pwiz_bindings_cli.dll
        /// Set "Copy Local" to false to avoid breaking the DLL resolver
        /// You must also call <see cref="pwiz.ProteowizardWrapper.DependencyLoader.AddAssemblyResolver()"/> in any function that calls a function that uses this function.
        /// </remarks>
        public List<CVParamData> GetSpectrumCVParamData(int scanIndex)
        {
            return mDataReader.GetSpectrumCVParamData(scanIndex);
        }

        /// <summary>
        /// Get a container describing the scan (or scans) associated with the given spectrum
        /// </summary>
        /// <param name="scanIndex"></param>
        /// <returns>Scan info container</returns>
        /// <remarks>Useful for obtaining the filter string, scan start time, ion injection time, etc.</remarks>
        public SpectrumScanContainer GetSpectrumScanInfo(int scanIndex)
        {
            return mDataReader.GetSpectrumScanInfo(scanIndex);
        }

        /// <summary>
        /// Get the ProteoWizard native spectrum object for the specified spectrum. (requires reference to pwiz_bindings_cli; set "copy local" to false.)
        /// </summary>
        /// <param name="scanIndex"></param>
        /// <returns></returns>
        /// <remarks>
        /// Use of this method requires the calling project to reference pwiz_bindings_cli.dll
        /// Set "Copy Local" to false to avoid breaking the DLL resolver
        /// You must also call <see cref="pwiz.ProteowizardWrapper.DependencyLoader.AddAssemblyResolver()"/> in any function that calls a function that uses this function.
        /// Alternatively, use <see cref="GetSpectrumScanInfo"/> or the GetSpectrum method that returns an <see cref="MsDataSpectrum"/> object
        /// </remarks>
        public Spectrum GetSpectrumObject(int scanIndex)
        {
            return mDataReader.GetSpectrumObject(scanIndex);
        }

        /// <summary>
        /// List of MSConvert-style filter strings to apply to the spectrum list.
        /// </summary>
        /// <remarks>If the filter count is greater than 0, the default handling of the spectrumList using the optional constructor parameters is disabled.</remarks>
        public List<string> Filters => mDataReader.Filters;

        /// <summary>
        /// Uses the centroiding/peak picking algorithm that the vendor libraries provide, if available; otherwise uses a low-quality centroiding algorithm.
        /// </summary>
        public string VendorCentroiding => MsDataFileImpl.VendorCentroiding;

        /// <summary>
        /// Continuous Wavelet Transform peak picker - high-quality peak picking, may be slow with some high-res data.
        /// </summary>
        public string CwtCentroiding => MsDataFileImpl.CwtCentroiding;

        /// <summary>
        /// Add/remove Vendor Centroiding to the filter list. Call <see cref="RedoFilters()"/> if calling this after reading any spectra.
        /// </summary>
        public bool UseVendorCentroiding
        {
            get => mDataReader.UseVendorCentroiding;
            set => mDataReader.UseVendorCentroiding = value;
        }

        /// <summary>
        /// Add/remove CWT Centroiding to the filter list. Call <see cref="RedoFilters()"/> if calling this after reading any spectra.
        /// </summary>
        public bool UseCwtCentroiding
        {
            get => mDataReader.UseCwtCentroiding;
            set => mDataReader.UseCwtCentroiding = value;
        }

        /// <summary>
        /// Force the reload of the spectrum list, reapplying any specified filters.
        /// </summary>
        public void RedoFilters()
        {
            mDataReader.RedoFilters();
        }

        private readonly MsDataFileImpl mDataReader;

        /// <summary>
        /// Returns the file id of the specified file (as an array, which typically only has one item)
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string[] ReadIds(string path)
        {
            return MsDataFileImpl.ReadIds(path);
        }

        /// <summary>
        /// Constant that corresponds to "SRM TIC "
        /// </summary>
        public static string PREFIX_TOTAL => MsDataFileImpl.PREFIX_TOTAL;

        /// <summary>
        /// Constant that corresponds to "SRM SIC "
        /// </summary>
        public static string PREFIX_SINGLE => MsDataFileImpl.PREFIX_SINGLE;

        /// <summary>
        /// Constant that corresponds to "SIM SIC "
        /// </summary>
        public static string PREFIX_PRECURSOR => MsDataFileImpl.PREFIX_PRECURSOR;

        /// <summary>
        /// If the specified id is negative charge
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static bool? IsNegativeChargeIdNullable(string id)
        {
            return MsDataFileImpl.IsNegativeChargeIdNullable(id);
        }

        /// <summary>
        /// If the specified id is Single Ion
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static bool IsSingleIonCurrentId(string id)
        {
            return MsDataFileImpl.IsSingleIonCurrentId(id);
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="path">Data file path</param>
        /// <param name="sampleIndex">Sample index to select within the data file, typically 0</param>
        /// <param name="lockmassParameters">Lock mass parameters (used for Waters datasets)</param>
        /// <param name="simAsSpectra">Whether to treat SIM data as spectra, default true</param>
        /// <param name="srmAsSpectra">Whether to treat SRM data as spectra, default true</param>
        /// <param name="acceptZeroLengthSpectra">Whether to accept zero-length spectra, default true</param>
        /// <param name="requireVendorCentroidedMS1">True to return centroided MS1 spectra</param>
        /// <param name="requireVendorCentroidedMS2">True to return centroided MS2 spectra</param>
        /// <param name="spectrumCacheSize">Positive number to cache recent spectra in memory to reduce disk I/O; defaults to 3</param>
        /// <remarks>This differs from the ProteoWizard version of this code by defaulting to treating SIM and SRM data as spectra.</remarks>
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
        /// Enable spectrum data caching. May result in faster reading
        /// </summary>
        /// <param name="cacheSize"></param>
        public void EnableCaching(int? cacheSize)
        {
            mDataReader.EnableCaching(cacheSize);
        }

        /// <summary>
        /// Disable the spectrum data caching (NOTE: May result in slower reading)
        /// </summary>
        public void DisableCaching()
        {
            mDataReader.DisableCaching();
        }

        /// <summary>
        /// The Run ID
        /// </summary>
        public string RunId => mDataReader.RunId;

        /// <summary>
        /// The run start time
        /// </summary>
        public DateTime? RunStartTime => mDataReader.RunStartTime;

        /// <summary>
        /// Data and Instrument Configuration information
        /// </summary>
        public MsDataConfigInfo ConfigInfo => mDataReader.ConfigInfo;

        /// <summary>
        /// Check if the file has be processed by the specified software
        /// </summary>
        /// <param name="softwareName"></param>
        /// <returns></returns>
        public bool IsProcessedBy(string softwareName)
        {
            return mDataReader.IsProcessedBy(softwareName);
        }

        /// <summary>
        /// If the spectrum is a Waters Lockmass spectrum
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public bool IsWatersLockmassSpectrum(MsDataSpectrum s)
        {
            return mDataReader.IsWatersLockmassSpectrum(s);
        }

        /// <summary>
        /// Record any instrument info found in the file, along with any Waters lockmass info we have
        /// </summary>
        public IEnumerable<MsInstrumentConfigInfo> GetInstrumentConfigInfoList()
        {
            return mDataReader.GetInstrumentConfigInfoList();
        }

        /// <summary>
        /// If the file is an AB Sciex file
        /// </summary>
        public bool IsABFile => mDataReader.IsABFile;

        /// <summary>
        /// If the file is a MzWiff file
        /// </summary>
        public bool IsMzWiffXml => mDataReader.IsMzWiffXml;

        /// <summary>
        /// If the file is an Agilent file
        /// </summary>
        public bool IsAgilentFile => mDataReader.IsAgilentFile;

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
        /// If the file is a Shimadzu file
        /// </summary>
        public bool IsShimadzuFile => mDataReader.IsShimadzuFile;

        /// <summary>
        /// Number of chromatograms
        /// </summary>
        public int ChromatogramCount => mDataReader.ChromatogramCount;

        /// <summary>
        /// Get the NativeID of the specified chromatogram
        /// </summary>
        /// <param name="index"></param>
        /// <param name="indexId"></param>
        /// <returns></returns>
        public string GetChromatogramId(int index, out int indexId)
        {
            return mDataReader.GetChromatogramId(index, out indexId);
        }

        /// <summary>
        /// Get the data for the specified chromatogram
        /// </summary>
        /// <param name="chromIndex"></param>
        /// <param name="id"></param>
        /// <param name="timeArray"></param>
        /// <param name="intensityArray"></param>
        public void GetChromatogram(int chromIndex, out string id,
            out float[] timeArray, out float[] intensityArray)
        {
            mDataReader.GetChromatogram(chromIndex, out id, out timeArray, out intensityArray);
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
        /// Get an array containing the total ion current for all scans
        /// </summary>
        /// <returns></returns>
        public double[] GetTotalIonCurrent()
        {
            return mDataReader.GetTotalIonCurrent();
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
        public void GetScanTimesAndMsLevels(out double[] times, out byte[] msLevels, Action<int, int> progressDelegate = null)
        {
            mDataReader.GetScanTimesAndMsLevels(out times, out msLevels, progressDelegate);
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
        public void GetScanTimesAndMsLevels(
            CancellationToken cancellationToken,
            out double[] times,
            out byte[] msLevels,
            Action<int, int> progressDelegate = null)
        {
            mDataReader.GetScanTimesAndMsLevels(cancellationToken, out times, out msLevels, progressDelegate);
        }

        /// <summary>
        /// Get the spectrum count
        /// </summary>
        public int SpectrumCount => mDataReader.SpectrumCount;

        /// <summary>
        /// Get the spectrum count
        /// </summary>
        /// <returns></returns>
        [Obsolete("Use the SpectrumCount property instead")]
        public int GetSpectrumCount()
        {
            return SpectrumCount;
        }

        /// <summary>
        /// Get the spectrum index of the specified NativeID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public int GetSpectrumIndex(string id)
        {
            return mDataReader.GetSpectrumIndex(id);
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
        /// Returns an MsDataSpectrum object representing the spectrum requested.
        /// </summary>
        /// <param name="spectrumIndex"></param>
        /// <param name="getBinaryData"></param>
        /// <returns></returns>
        /// <remarks>
        /// If you need direct access to CVParams, and are using MSDataFileReader, try using <see cref="GetSpectrumObject"/> instead.
        /// Alternatively, use <see cref="GetSpectrumScanInfo"/>
        /// </remarks>
        public MsDataSpectrum GetSpectrum(int spectrumIndex, bool getBinaryData = true)
        {
            return mDataReader.GetSpectrum(spectrumIndex, getBinaryData);
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
        /// Return the typical NativeId for a scan number in a thermo .raw file
        /// </summary>
        /// <param name="scanNumber"></param>
        /// <returns></returns>
        public string GetThermoNativeId(int scanNumber)
        {
            return mDataReader.GetThermoNativeId(scanNumber);
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
        /// Return a mapping from scan number to spectrumIndex
        /// </summary>
        /// <returns>Dictionary where keys are scan number and values are the spectrumIndex for each scan</returns>
        /// <remarks>
        /// Works for Thermo .raw files, Bruker .D folders, Bruker/Agilent .yep files, Agilent MassHunter data, Waters .raw folders, and Shimadzu data
        /// For UIMF files use <see cref="GetUimfFrameScanPairToIndexMapping"/></remarks>
        public Dictionary<int, int> GetScanToIndexMapping()
        {
            return mDataReader.GetScanToIndexMapping();
        }

        /// <summary>
        /// Check if the file has SRR Spectra
        /// </summary>
        public bool HasSrmSpectra => mDataReader.HasSrmSpectra;

        /// <summary>
        /// Check if the file has drift time spectra
        /// </summary>
        public bool HasDriftTimeSpectra => mDataReader.HasDriftTimeSpectra;

        /// <summary>
        /// Check if the file contains chromatogram data
        /// </summary>
        public bool HasChromatogramData => mDataReader.HasChromatogramData;

        /// <summary>
        /// Get the specified SRM spectrum. Returns null if the specified spectrum is not SRM
        /// </summary>
        /// <param name="scanIndex"></param>
        /// <returns></returns>
        public MsDataSpectrum GetSrmSpectrum(int scanIndex)
        {
            return mDataReader.GetSrmSpectrum(scanIndex);
        }

        /// <summary>
        /// Get the NativeID of the specified scan
        /// </summary>
        /// <param name="scanIndex"></param>
        /// <returns></returns>
        public string GetSpectrumId(int scanIndex)
        {
            return mDataReader.GetSpectrumId(scanIndex);
        }

        /// <summary>
        /// Check is the specified scan is centroided
        /// </summary>
        /// <param name="scanIndex"></param>
        /// <returns></returns>
        public bool IsCentroided(int scanIndex)
        {
            return mDataReader.IsCentroided(scanIndex);
        }

        /// <summary>
        /// Check if the specified scan is SRM
        /// </summary>
        /// <param name="scanIndex"></param>
        /// <returns></returns>
        public bool IsSrmSpectrum(int scanIndex)
        {
            return mDataReader.IsSrmSpectrum(scanIndex);
        }

        /// <summary>
        /// Get the MS Level of the specified scan
        /// </summary>
        /// <param name="scanIndex"></param>
        /// <returns></returns>
        public int GetMsLevel(int scanIndex)
        {
            return mDataReader.GetMsLevel(scanIndex);
        }

        /// <summary>
        /// Get the drift time (in msec) of the specified scan
        /// </summary>
        /// <param name="scanIndex"></param>
        /// <returns></returns>
        public double? GetDriftTimeMsec(int scanIndex)
        {
            return mDataReader.GetDriftTimeMsec(scanIndex);
        }

        /// <summary>
        /// Get the start time of the specified scan
        /// </summary>
        /// <param name="scanIndex"></param>
        /// <returns></returns>
        public double? GetStartTime(int scanIndex)
        {
            return mDataReader.GetStartTime(scanIndex);
        }

        /// <summary>
        /// Get the time and precursors for the specified scan
        /// </summary>
        /// <param name="scanIndex"></param>
        /// <returns></returns>
        public MsTimeAndPrecursors GetInstantTimeAndPrecursors(int scanIndex)
        {
            return mDataReader.GetInstantTimeAndPrecursors(scanIndex);
        }

        /// <summary>
        /// Get the precursors for the specified scan
        /// </summary>
        /// <param name="scanIndex"></param>
        /// <returns></returns>
        public MsPrecursor[] GetPrecursors(int scanIndex)
        {
            return mDataReader.GetPrecursors(scanIndex);
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
        /// Cleanup the objects; Chains to cleanup all held unmanaged objects
        /// </summary>
        public void Dispose()
        {
            mDataReader.Dispose();
        }

        /// <summary>
        /// The filepath of the currently loaded file
        /// </summary>
        public string FilePath => mDataReader.FilePath;
    }
}
