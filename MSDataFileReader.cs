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
using System.Threading;
using pwiz.CLI.data;
using pwiz.CLI.msdata;

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
	    public List<string> Filters
	    {
	        get { return mDataReader.Filters; }
	    }

	    /// <summary>
	    /// Uses the centroiding/peak picking algorithm that the vendor libraries provide, if available; otherwise uses a low-quality centroiding algorithm.
	    /// </summary>
	    public string VendorCentroiding
	    {
	        get { return MsDataFileImpl.VendorCentroiding; }
	    }

	    /// <summary>
        /// Continuous Wavelet Transform peak picker - high-quality peak picking, may be slow with some high-res data.
        /// </summary>
        public string CwtCentroiding
	    {
	        get { return MsDataFileImpl.CwtCentroiding; }
	    }

        /// <summary>
        /// Add/remove Vendor Centroiding to the filter list. Call <see cref="RedoFilters()"/> if calling this after reading any spectra.
        /// </summary>
        public bool UseVendorCentroiding
        {
            get { return mDataReader.UseVendorCentroiding; }
            set { mDataReader.UseVendorCentroiding = value; }
        }

        /// <summary>
        /// Add/remove CWT Centroiding to the filter list. Call <see cref="RedoFilters()"/> if calling this after reading any spectra.
        /// </summary>
        public bool UseCwtCentroiding
        {
            get { return mDataReader.UseCwtCentroiding; }
            set { mDataReader.UseCwtCentroiding = value; }
        }

        /// <summary>
        /// Force the reload of the spectrum list, reapplying any specified filters.
        /// </summary>
        public void RedoFilters()
        {
            mDataReader.RedoFilters();
        }

        private readonly MsDataFileImpl mDataReader;

        public static string[] ReadIds(string path)
        {
            return MsDataFileImpl.ReadIds(path);
        }

        public static string PREFIX_TOTAL { get { return MsDataFileImpl.PREFIX_TOTAL; } }
        public static string PREFIX_SINGLE { get { return MsDataFileImpl.PREFIX_SINGLE; } }
        public static string PREFIX_PRECURSOR { get { return MsDataFileImpl.PREFIX_PRECURSOR; } }

        public static bool? IsNegativeChargeIdNullable(string id)
        {
            return MsDataFileImpl.IsNegativeChargeIdNullable(id);
        }

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

        public void EnableCaching(int? cacheSize)
        {
            mDataReader.EnableCaching(cacheSize);
        }

        public void DisableCaching()
        {
            mDataReader.DisableCaching();
        }

        public string RunId { get { return mDataReader.RunId; } }

        public DateTime? RunStartTime
        {
            get { return mDataReader.RunStartTime; }
        }

        public MsDataConfigInfo ConfigInfo
        {
            get { return mDataReader.ConfigInfo; }
        }

        public bool IsProcessedBy(string softwareName)
        {
            return mDataReader.IsProcessedBy(softwareName);
        }

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

        public bool IsABFile
        {
            get { return mDataReader.IsABFile; }
        }

        public bool IsMzWiffXml
        {
            get { return mDataReader.IsMzWiffXml; } // Not L10N
        }

        public bool IsAgilentFile
        {
            get { return mDataReader.IsAgilentFile; }
        }

        public bool IsThermoFile
        {
            get { return mDataReader.IsThermoFile; }
        }

        public bool IsWatersFile
        {
            get { return mDataReader.IsWatersFile; }
        }

        public bool IsWatersLockmassCorrectionCandidate
        {
            get { return mDataReader.IsWatersLockmassCorrectionCandidate; }
        }

        public bool IsShimadzuFile
        {
            get { return mDataReader.IsShimadzuFile; }
        }

        public int ChromatogramCount
        {
            get { return mDataReader.ChromatogramCount; }
        }

        public string GetChromatogramId(int index, out int indexId)
        {
            return mDataReader.GetChromatogramId(index, out indexId);
        }

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
        /// <remarks>See also the overloaded version that accepts a CancellationToken</remarks>
        public void GetScanTimesAndMsLevels(out double[] times, out byte[] msLevels)
        {
            mDataReader.GetScanTimesAndMsLevels(out times, out msLevels);
        }

        /// <summary>
        /// Walks the spectrum list, and fills in the retention time and MS level of each scan.
        /// Some data files do not have any chromatograms in them, so GetScanTimes
        /// cannot be used.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="times">Output: scan times (in minutes)</param>
        /// <param name="msLevels">Output: MS Levels (1 for MS1, 2 for MS/MS, etc.)</param>
        /// <remarks>See also the overloaded version that accepts a CancellationToken</remarks>
        public void GetScanTimesAndMsLevels(CancellationToken cancellationToken, out double[] times, out byte[] msLevels)
        {
            mDataReader.GetScanTimesAndMsLevels(cancellationToken, out times, out msLevels);
        }

        public int SpectrumCount
        {
            get { return mDataReader.SpectrumCount; }
        }

        [Obsolete("Use the SpectrumCount property instead")]
        public int GetSpectrumCount()
        {
            return SpectrumCount;
        }

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
	    /// Return a mapping from scan number to spectrumIndex for a thermo .raw file
	    /// </summary>
	    /// <returns>Dictionary where keys are scan number and values are the spectrumIndex for each scan</returns>
	    public Dictionary<int, int> GetThermoScanToIndexMapping()
	    {
            return mDataReader.GetThermoScanToIndexMapping();
	    }

	    public bool HasSrmSpectra
        {
            get { return mDataReader.HasSrmSpectra; }
        }

        public bool HasDriftTimeSpectra
        {
            get { return mDataReader.HasDriftTimeSpectra; }
        }

        public bool HasChromatogramData
        {
            get { return mDataReader.HasChromatogramData; }
        }

        public MsDataSpectrum GetSrmSpectrum(int scanIndex)
        {
            return mDataReader.GetSrmSpectrum(scanIndex);
        }

        public string GetSpectrumId(int scanIndex)
        {
            return mDataReader.GetSpectrumId(scanIndex);
        }

        public bool IsCentroided(int scanIndex)
        {
            return mDataReader.IsCentroided(scanIndex);
        }

        public bool IsSrmSpectrum(int scanIndex)
        {
            return mDataReader.IsSrmSpectrum(scanIndex);
        }

        public int GetMsLevel(int scanIndex)
        {
            return mDataReader.GetMsLevel(scanIndex);
        }

        public double? GetDriftTimeMsec(int scanIndex)
        {
            return mDataReader.GetDriftTimeMsec(scanIndex);
        }

        public double? GetStartTime(int scanIndex)
        {
            return mDataReader.GetStartTime(scanIndex);
        }

        public MsTimeAndPrecursors GetInstantTimeAndPrecursors(int scanIndex)
        {
            return mDataReader.GetInstantTimeAndPrecursors(scanIndex);
        }

        public MsPrecursor[] GetPrecursors(int scanIndex)
        {
            return mDataReader.GetPrecursors(scanIndex);
        }

        public void Write(string path)
        {
            mDataReader.Write(path);
        }

        public void Dispose()
        {
            mDataReader.Dispose();
        }

        public string FilePath { get { return mDataReader.FilePath; } }
	}
}
