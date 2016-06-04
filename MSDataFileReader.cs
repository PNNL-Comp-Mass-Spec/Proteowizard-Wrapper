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
	public class MSDataFileReader : IDisposable
	{
        /// <summary>
        /// This ensures that the Assembly Resolver is added prior to actually using this class.
        /// </summary>
        static MSDataFileReader()
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
            return this.MsDataFileImpl.GetChromatogramCVParams(chromIndex);
        }

        /// <summary>
        /// Get the ProteoWizard native chromatogram object for the specified spectrum
        /// </summary>
        /// <param name="chromIndex"></param>
        /// <returns></returns>
        /// <remarks>Use of this method requires the calling project to reference pwiz_bindings_cli.dll</remarks>
        public Chromatogram GetChromatogramObject(int chromIndex)
        {
            return this.MsDataFileImpl.GetChromatogramObject(chromIndex);
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
            return this.MsDataFileImpl.GetSpectrumCVParams(scanIndex);
        }

        /// <summary>
        /// Get the list of CVParams for the specified spectrum
        /// </summary>
        /// <param name="scanIndex"></param>
        /// <returns>List of CVParamData structs</returns>
        public List<CVParamData> GetSpectrumCVParamData(int scanIndex)
        {
            return this.MsDataFileImpl.GetSpectrumCVParamData(scanIndex);
        }

        /// <summary>
        /// Get a container describing the scan (or scans) associated with the given spectrum
        /// </summary>
        /// <param name="scanIndex"></param>
        /// <returns>Scan info container</returns>
        /// <remarks>Useful for obtaining the filter string, scan start time, ion injection time, etc.</remarks>
        public SpectrumScanContainer GetSpectrumScanInfo(int scanIndex)
        {
            return this.MsDataFileImpl.GetSpectrumScanInfo(scanIndex);
        }
        
        /// <summary>
        /// Get the ProteoWizard native spectrum object for the specified spectrum.
        /// </summary>
        /// <param name="scanIndex"></param>
        /// <returns></returns>
        /// <remarks>Use of this method requires the calling project to reference pwiz_bindings_cli.dll</remarks>
        public Spectrum GetSpectrumObject(int scanIndex)
        {
            return this.MsDataFileImpl.GetSpectrumObject(scanIndex);
        }

	    /// <summary>
	    /// List of MSConvert-style filter strings to apply to the spectrum list.
	    /// </summary>
	    /// <remarks>If the filter count is greater than 0, the default handling of the spectrumList using the optional constructor parameters is disabled.</remarks>
	    public List<string> Filters
	    {
	        get { return this.MsDataFileImpl.Filters; }
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
            get { return this.MsDataFileImpl.UseVendorCentroiding; }
            set { this.MsDataFileImpl.UseVendorCentroiding = value; }
        }

        /// <summary>
        /// Add/remove CWT Centroiding to the filter list. Call <see cref="RedoFilters()"/> if calling this after reading any spectra.
        /// </summary>
        public bool UseCwtCentroiding
        {
            get { return this.MsDataFileImpl.UseCwtCentroiding; }
            set { this.MsDataFileImpl.UseCwtCentroiding = value; }
        }

        /// <summary>
        /// Force the reload of the spectrum list, reapplying any specified filters.
        /// </summary>
        public void RedoFilters()
        {
            this.MsDataFileImpl.RedoFilters();
        }

        private MsDataFileImpl MsDataFileImpl;

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
        /// Constructor; Call <see cref="pwiz.ProteowizardWrapper.DependencyLoader.AddAssemblyResolver"/> in the function that calls the function that calls this.
        /// </summary>
        /// <remarks>Call <see cref="pwiz.ProteowizardWrapper.DependencyLoader.AddAssemblyResolver"/> in the function that calls the function that calls this.</remarks>
        protected MSDataFileReader()
        {
            DependencyLoader.AddAssemblyResolver();
        }

        /// <summary>
        /// Constructor; Call <see cref="pwiz.ProteowizardWrapper.DependencyLoader.AddAssemblyResolver"/> in the function that calls the function that calls this.
        /// </summary>
        /// <param name="path">Data file path</param>
        /// <param name="sampleIndex">Sample index to select within the data file, typically 0</param>
        /// <param name="lockmassParameters">Lock mass parameters (used for Waters datasets)</param>
        /// <param name="simAsSpectra">Whether to treat SIM data as spectra, default false</param>
        /// <param name="srmAsSpectra">Whether to treat SRM data as spectra, default false</param>
        /// <param name="acceptZeroLengthSpectra">Whether to accept zero-length spectra, default true</param>
        /// <param name="requireVendorCentroidedMS1">True to return centroided MS1 spectra</param>
        /// <param name="requireVendorCentroidedMS2">True to return centroided MS2 spectra</param>
        /// <remarks>Call <see cref="pwiz.ProteowizardWrapper.DependencyLoader.AddAssemblyResolver"/> in the function that calls the function that calls this.</remarks>
        public MSDataFileReader(
            string path, 
            int sampleIndex = 0, 
            LockMassParameters lockmassParameters = null, 
            bool simAsSpectra = false, 
            bool srmAsSpectra = false, 
            bool acceptZeroLengthSpectra = true, 
            bool requireVendorCentroidedMS1 = false, 
            bool requireVendorCentroidedMS2 = false)
        {
            // This one actually won't work.
			DependencyLoader.AddAssemblyResolver();

            this.MsDataFileImpl = new MsDataFileImpl(path, sampleIndex, lockmassParameters, simAsSpectra, srmAsSpectra, acceptZeroLengthSpectra, requireVendorCentroidedMS1, requireVendorCentroidedMS2);
        }

        public void EnableCaching(int? cacheSize)
        {
            this.MsDataFileImpl.EnableCaching(cacheSize);
        }

        public void DisableCaching()
        {
            this.MsDataFileImpl.DisableCaching();
        }

        public string RunId { get { return this.MsDataFileImpl.RunId; } }

        public DateTime? RunStartTime
        {
            get { return this.MsDataFileImpl.RunStartTime; }
        }

        public MsDataConfigInfo ConfigInfo
        {
            get { return this.MsDataFileImpl.ConfigInfo; }
        }

        public bool IsProcessedBy(string softwareName)
        {
            return this.MsDataFileImpl.IsProcessedBy(softwareName);
        }

        public bool IsWatersLockmassSpectrum(MsDataSpectrum s)
        {
            return this.MsDataFileImpl.IsWatersLockmassSpectrum(s);
        }

        /// <summary>
        /// Record any instrument info found in the file, along with any Waters lockmass info we have
        /// </summary>
        public IEnumerable<MsInstrumentConfigInfo> GetInstrumentConfigInfoList()
        {
            return this.MsDataFileImpl.GetInstrumentConfigInfoList();
        }

        public bool IsABFile
        {
            get { return this.MsDataFileImpl.IsABFile; }
        }

        public bool IsMzWiffXml
        {
            get { return this.MsDataFileImpl.IsMzWiffXml; } // Not L10N
        }

        public bool IsAgilentFile
        {
            get { return this.MsDataFileImpl.IsAgilentFile; }
        }

        public bool IsThermoFile
        {
            get { return this.MsDataFileImpl.IsThermoFile; }
        }

        public bool IsWatersFile
        {
            get { return this.MsDataFileImpl.IsWatersFile; }
        }

        public bool IsWatersLockmassCorrectionCandidate
        {
            get { return this.MsDataFileImpl.IsWatersLockmassCorrectionCandidate; }
        }

        public bool IsShimadzuFile
        {
            get { return this.MsDataFileImpl.IsShimadzuFile; }
        }

        public int ChromatogramCount
        {
            get { return this.MsDataFileImpl.ChromatogramCount; }
        }

        public string GetChromatogramId(int index, out int indexId)
        {
            return this.MsDataFileImpl.GetChromatogramId(index, out indexId);
        }

        public void GetChromatogram(int chromIndex, out string id,
            out float[] timeArray, out float[] intensityArray)
        {
            this.MsDataFileImpl.GetChromatogram(chromIndex, out id, out timeArray, out intensityArray);         
        }

        /// <summary>
        /// Gets the retention times from the first chromatogram in the data file.
        /// Returns null if there are no chromatograms in the file.
        /// </summary>
        public double[] GetScanTimes()
        {
            return this.MsDataFileImpl.GetScanTimes();
        }

        public double[] GetTotalIonCurrent()
        {
            return this.MsDataFileImpl.GetTotalIonCurrent();
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
            this.MsDataFileImpl.GetScanTimesAndMsLevels(out times, out msLevels);
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
            this.MsDataFileImpl.GetScanTimesAndMsLevels(cancellationToken, out times, out msLevels);
        }

        public int SpectrumCount
        {
            get { return this.MsDataFileImpl.SpectrumCount; }
        }

        [Obsolete("Use the SpectrumCount property instead")]
        public int GetSpectrumCount()
        {
            return SpectrumCount;
        }

        public int GetSpectrumIndex(string id)
        {
            return this.MsDataFileImpl.GetSpectrumIndex(id);
        }

        public void GetSpectrum(int spectrumIndex, out double[] mzArray, out double[] intensityArray)
        {
            this.MsDataFileImpl.GetSpectrum(spectrumIndex, out mzArray, out intensityArray);
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
            return this.MsDataFileImpl.GetSpectrum(spectrumIndex, getBinaryData);
        }

        /// <summary>
        /// Get SpectrumIDs for all spectra in the run
        /// </summary>
        /// <returns>List of NativeIds</returns>
        public List<string> GetSpectrumIdList()
        {
            return this.MsDataFileImpl.GetSpectrumIdList();
        }

         /// <summary>
        /// Return the typical NativeId for a scan number in a thermo .raw file
        /// </summary>
        /// <param name="scanNumber"></param>
        /// <returns></returns>
        public string GetThermoNativeId(int scanNumber)
        {
            return this.MsDataFileImpl.GetThermoNativeId(scanNumber);
        }

	    /// <summary>
	    /// Return a mapping from scan number to spectrumIndex for a thermo .raw file
	    /// </summary>
	    /// <returns>Dictionary where keys are scan number and values are the spectrumIndex for each scan</returns>
	    public Dictionary<int, int> GetThermoScanToIndexMapping()
	    {
            return this.MsDataFileImpl.GetThermoScanToIndexMapping();
	    }

	    public bool HasSrmSpectra
        {
            get { return this.MsDataFileImpl.HasSrmSpectra; }
        }

        public bool HasDriftTimeSpectra
        {
            get { return this.MsDataFileImpl.HasDriftTimeSpectra; }
        }

        public bool HasChromatogramData
        {
            get { return this.MsDataFileImpl.HasChromatogramData; }
        }

        public MsDataSpectrum GetSrmSpectrum(int scanIndex)
        {
            return this.MsDataFileImpl.GetSrmSpectrum(scanIndex);
        }

        public string GetSpectrumId(int scanIndex)
        {
            return this.MsDataFileImpl.GetSpectrumId(scanIndex);
        }

        public bool IsCentroided(int scanIndex)
        {
            return this.MsDataFileImpl.IsCentroided(scanIndex);
        }

        public bool IsSrmSpectrum(int scanIndex)
        {
            return this.MsDataFileImpl.IsSrmSpectrum(scanIndex);
        }

        public int GetMsLevel(int scanIndex)
        {
            return this.MsDataFileImpl.GetMsLevel(scanIndex);
        }

        public double? GetDriftTimeMsec(int scanIndex)
        {
            return this.MsDataFileImpl.GetDriftTimeMsec(scanIndex);
        }

        public double? GetStartTime(int scanIndex)
        {
            return this.MsDataFileImpl.GetStartTime(scanIndex);
        }

        public MsTimeAndPrecursors GetInstantTimeAndPrecursors(int scanIndex)
        {
            return this.MsDataFileImpl.GetInstantTimeAndPrecursors(scanIndex);
        }

        public MsPrecursor[] GetPrecursors(int scanIndex)
        {
            return this.MsDataFileImpl.GetPrecursors(scanIndex);
        }

        public void Write(string path)
        {
            this.MsDataFileImpl.Write(path);
        }

        public void Dispose()
        {
            this.MsDataFileImpl.Dispose();
        }

        public string FilePath { get { return this.MsDataFileImpl.FilePath; } }
	}
}
