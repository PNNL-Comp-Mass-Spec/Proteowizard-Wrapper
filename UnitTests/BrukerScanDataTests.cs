﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using pwiz.ProteowizardWrapper;

// ReSharper disable StringLiteralTypo
namespace ProteowizardWrapperUnitTests
{
    [TestFixture]
    public class BrukerScanDataTests
    {
        // Ignore Spelling: Bruker, cid

        [Test]
        [TestCase("MZ20160603PPS_edta_000004.d", 1, 1, 0, 1)]
        [TestCase("Blank-2_05May16_Leopard_Infuse_1_01_7976.d", 1, 1, 1, 0)]
        [TestCase("Corrupt_2016_05_08_FloroBenzene_PhF_Neg_000002.d", 1, 1, 0, 0)]
        public void TestCorruptDataHandling(
            string dotDFolderName,
            int scanStart,
            int scanEnd,
            int expectedMS1,
            int expectedMS2)
        {
            var dataFolder = GetBrukerDataFolder(dotDFolderName);

            try
            {
                using var reader = new MSDataFileReader(dataFolder.FullName);

                var scanCount = reader.SpectrumCount;
                Console.WriteLine("Scan count for {0}: {1}", dataFolder.Name, scanCount);

                if (expectedMS1 + expectedMS2 == 0)
                {
                    Assert.IsTrue(scanCount == 0, "ScanCount is non-zero, while we expected it to be 0");
                }
                else
                {
                    Assert.IsTrue(scanCount > 0, "ScanCount is zero, while we expected it to be > 0");
                }

                var scanNumberToIndexMap = reader.GetScanToIndexMapping();

                var scanCountMS1 = 0;
                var scanCountMS2 = 0;

                foreach (var scan in scanNumberToIndexMap.Where(x => x.Key >= scanStart && x.Key <= scanEnd))
                {
                    var scanNumber = scan.Key;
                    var spectrumIndex = scan.Value;

                    try
                    {
                        var spectrum = reader.GetSpectrum(spectrumIndex);

                        var cvScanInfo = reader.GetSpectrumScanInfo(spectrumIndex);

                        Assert.IsTrue(cvScanInfo != null, "GetSpectrumScanInfo returned a null object for scan {0}", scanNumber);

                        if (spectrum.Level > 1)
                            scanCountMS2++;
                        else
                            scanCountMS1++;

                        var dataPointCount = spectrum.Mzs.Length;

                        Assert.IsTrue(dataPointCount > 0, "Data point count is 0 for scan {0}", scanNumber);
                        Assert.IsTrue(spectrum.Mzs.Length > 0, "m/z data is empty for scan {0}", scanNumber);
                        Assert.IsTrue(spectrum.Intensities.Length > 0, "Intensity data is empty for scan {0}", scanNumber);
                        Assert.IsTrue(spectrum.Mzs.Length == spectrum.Intensities.Length, "Array length mismatch for m/z and intensity data for scan {0}", scanNumber);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception reading scan {0}: {1}", scanNumber, ex.Message);
                        Assert.Fail("Exception reading scan {0}", scanNumber);
                    }
                }

                Console.WriteLine("scanCountMS1={0}", scanCountMS1);
                Console.WriteLine("scanCountMS2={0}", scanCountMS2);

                Assert.AreEqual(expectedMS1, scanCountMS1, "MS1 scan count mismatch");
                Assert.AreEqual(expectedMS2, scanCountMS2, "MS2 scan count mismatch");
            }
            catch (Exception ex)
            {
                if (expectedMS1 + expectedMS2 == 0)
                {
                    Console.WriteLine("Error opening .D folder (this was expected):\n{0}", ex.Message);
                }
                else
                {
                    var msg = string.Format("Exception opening .D folder {0}:\n{1}", dotDFolderName, ex.Message);
                    Console.WriteLine(msg);
                    Assert.Fail(msg);
                }
            }
        }

        [Test]
        [TestCase("2016_04_12_Background_000001.d", 1, 5, 5, 0, 5)]
        [TestCase("Blank-2_05May16_Leopard_Infuse_1_01_7976.d", 1, 1, 1, 0, 1)]
        [TestCase("blk_1_01_651.d", 1, 6, 6, 0, 6)]
        [TestCase("MZ20160603PPS_edta_000004.d", 1, 1, 0, 1, 1)]
        [TestCase("Humira_100fmol_20121026_hi_res_9_01_716.d", 1, 32, 32, 0, 32)]
        public void TestGetScanCountsByScanType(
            string dotDFolderName,
            int scanStart,
            int scanEnd,
            int expectedMS1,
            int expectedMS2,
            int expectedTotalScanCount)
        {
            var dataFolder = GetBrukerDataFolder(dotDFolderName);

            using var reader = new MSDataFileReader(dataFolder.FullName);

            Console.WriteLine("Parsing scan headers for {0}", dataFolder.Name);

            var scanCount = reader.SpectrumCount;
            Console.WriteLine("Total scans: {0}", scanCount);
            Assert.AreEqual(expectedTotalScanCount, scanCount, "Total scan count mismatch");
            Console.WriteLine();

            var scanNumberToIndexMap = reader.GetScanToIndexMapping();

            var scanCountMS1 = 0;
            var scanCountMS2 = 0;

            foreach (var scan in scanNumberToIndexMap.Where(x => x.Key >= scanStart && x.Key <= scanEnd))
            {
                // var scanNumber = scan.Key;
                var spectrumIndex = scan.Value;

                var spectrum = reader.GetSpectrum(spectrumIndex, false);

                if (spectrum.Level > 1)
                    scanCountMS2++;
                else
                    scanCountMS1++;
            }

            Console.WriteLine("scanCountMS1={0}", scanCountMS1);
            Console.WriteLine("scanCountMS2={0}", scanCountMS2);

            Assert.AreEqual(expectedMS1, scanCountMS1, "MS1 scan count mismatch");
            Assert.AreEqual(expectedMS2, scanCountMS2, "MS2 scan count mismatch");
        }

        [Test]
        [TestCase("2016_04_12_Background_000001.d", 1, 5, 5, 0)]
        [TestCase("Blank-2_05May16_Leopard_Infuse_1_01_7976.d", 1, 1, 1, 0)]
        [TestCase("Humira_100fmol_20121026_hi_res_9_01_716.d", 15, 20, 6, 0)]
        [TestCase("MZ20160603PPS_edta_000004.d", 1, 1, 0, 1)]
        public void TestGetScanInfo(string dotDFolderName, int scanStart, int scanEnd, int expectedMS1, int expectedMS2)
        {
            var expectedData = new Dictionary<string, Dictionary<int, string>>();

            // Keys in this dictionary are the scan number whose metadata is being retrieved
            var file1Data = new Dictionary<int, string>
            {
                // Scan MSLevel NumPeaks RetentionTime ScanStartTime DriftTimeMsec IonMobility LowMass HighMass TotalIonCurrent BasePeakMZ BasePeakIntensity ParentIonMZ IsolationWidth
                {1, "1 1 7794096   0.59   0.59 0 0  92  1300 7.1E+9    0.000 5.5E+8     0.00          positive False   696.00  0.0"},
                {2, "2 1 7794096   1.10   1.10 0 0  92  1300 7.2E+9    0.000 5.6E+8     0.00          positive False   696.00  0.0"},
                {3, "3 1 7794096   1.62   1.62 0 0  92  1300 7.2E+9    0.000 5.5E+8     0.00          positive False   696.00  0.0"},
                {4, "4 1 7794096   2.14   2.14 0 0  92  1300 7.1E+9    0.000 5.3E+8     0.00          positive False   696.00  0.0"},
                {5, "5 1 7794096   2.65   2.65 0 0  92  1300 7.1E+9    0.000 5.4E+8     0.00          positive False   696.00  0.0"}
            };
            expectedData.Add("2016_04_12_Background_000001", file1Data);

            var file2Data = new Dictionary<int, string>
            {
                {1, "1 1 7615807   3.58   3.58 0 0 111  1200 3.6E+10    0.000 3.1E+9     0.00          negative False   655.50  0.0"}
            };
            expectedData.Add("Blank-2_05May16_Leopard_Infuse_1_01_7976", file2Data);

            var file3Data = new Dictionary<int, string>
            {
                {15, "15 1 39775   2.38   2.38 0 0 983   984 0.0E+0    0.000 0.0E+0     0.00          positive False   983.50  0.0"},
                {16, "16 1 39775   2.53   2.53 0 0 983   984 0.0E+0    0.000 0.0E+0     0.00          positive False   983.50  0.0"},
                {17, "17 1 39775   2.69   2.69 0 0 983   984 0.0E+0    0.000 0.0E+0     0.00          positive False   983.50  0.0"},
                {18, "18 1 39775   2.84   2.84 0 0 983   984 0.0E+0    0.000 0.0E+0     0.00          positive False   983.50  0.0"},
                {19, "19 1 39775   2.99   2.99 0 0 983   984 0.0E+0    0.000 0.0E+0     0.00          positive False   983.50  0.0"},
                {20, "20 1 39775   3.15   3.15 0 0 983   984 0.0E+0    0.000 0.0E+0     0.00          positive False   983.50  0.0"}
            };
            expectedData.Add("Humira_100fmol_20121026_hi_res_9_01_716", file3Data);

            var file4Data = new Dictionary<int, string>
            {
                {1, "1 2 8214787   2.20   2.20 0 0 207 10000 6.0E+8    0.000 1.3E+7  1600.00 cid      negative False  1600.00  0.0"}
            };
            expectedData.Add("MZ20160603PPS_edta_000004", file4Data);

            var dataFolder = GetBrukerDataFolder(dotDFolderName);

            using var reader = new MSDataFileReader(dataFolder.FullName);

            Console.WriteLine("Scan info for {0}", dataFolder.Name);
            Console.WriteLine("{0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11} {12} {13} {14} {15} {16} {17}",
                "Scan", "MSLevel",
                "NumPeaks", "RetentionTime",
                "ScanStartTime",
                "DriftTimeMsec",
                "IonMobility",
                "LowMass", "HighMass", "TotalIonCurrent",
                "BasePeakMZ", "BasePeakIntensity",
                "ParentIonMZ", "ActivationType",
                "IonMode", "IsCentroided",
                "IsolationMZ", "IsolationWindowWidth");

            var scanNumberToIndexMap = reader.GetScanToIndexMapping();

            var scanCountMS1 = 0;
            var scanCountMS2 = 0;

            foreach (var scan in scanNumberToIndexMap.Where(x => x.Key >= scanStart && x.Key <= scanEnd))
            {
                var scanNumber = scan.Key;
                var spectrumIndex = scan.Value;

                var spectrum = reader.GetSpectrum(spectrumIndex);
                var spectrumParams = reader.GetSpectrumCVParamData(spectrumIndex);

                Assert.IsTrue(spectrum != null, "GetSpectrum returned a null object for scan " + scanNumber);

                var totalIonCurrent = CVParamUtilities.GetCvParamValueDbl(spectrumParams, CVParamUtilities.CVIDs.MS_TIC);
                var basePeakMZ = CVParamUtilities.GetCvParamValueDbl(spectrumParams, CVParamUtilities.CVIDs.MS_base_peak_m_z);
                var basePeakIntensity = CVParamUtilities.GetCvParamValueDbl(spectrumParams, CVParamUtilities.CVIDs.MS_base_peak_intensity);

                double isolationMZ = 0;
                double parentIonMZ = 0;
                var activationType = string.Empty;

                if (spectrum.Precursors.Count > 0)
                {
                    var precursor = spectrum.Precursors[0];

                    isolationMZ = precursor.IsolationMz.GetValueOrDefault();
                    parentIonMZ = precursor.PrecursorMz.GetValueOrDefault();

                    if (precursor.ActivationTypes != null)
                        activationType = string.Join(", ", precursor.ActivationTypes);
                }

                reader.GetScanMetadata(spectrumIndex, out var scanStartTime, out _, out _, out var lowMass, out var highMass, out var isolationWindowWidth);

                var retentionTime = CVParamUtilities.CheckNull(spectrum.RetentionTime);

                var numPeaks = spectrum.Mzs.Length;
                var ionMode = spectrum.NegativeCharge ? "negative" : "positive";

                var scanSummary =
                    string.Format(
                        "{0} {1} {2,5} {3,6:0.00} {4,6:0.00} {5:0} {6:0} {7,3:0} {8,5:0} {9,6:0.0E+0} {10,8:0.000} {11,6:0.0E+0} {12,8:0.00} {13,-8} {14} {15,-5} {16,8:0.00} {17,4:0.0}",
                        scanNumber, spectrum.Level,
                        numPeaks, retentionTime, scanStartTime,
#pragma warning disable 618
                        CVParamUtilities.CheckNull(spectrum.DriftTimeMsec),
#pragma warning restore 618
                        CVParamUtilities.CheckNull(spectrum.IonMobility.Mobility),
                        lowMass, highMass,
                        totalIonCurrent,
                        basePeakMZ, basePeakIntensity, parentIonMZ,
                        activationType,
                        ionMode, spectrum.Centroided,
                        isolationMZ, isolationWindowWidth);

                Console.WriteLine(scanSummary);

                if (spectrum.Level > 1)
                    scanCountMS2++;
                else
                    scanCountMS1++;

                if (!expectedData.TryGetValue(Path.GetFileNameWithoutExtension(dataFolder.Name), out var expectedDataThisFile))
                {
                    Assert.Fail("Dataset {0} not found in dictionary expectedData", dataFolder.Name);
                }

                if (expectedDataThisFile.TryGetValue(scanNumber, out var expectedScanSummary))
                {
                    Assert.AreEqual(expectedScanSummary, scanSummary,
                        "Scan summary mismatch, scan " + scanNumber);
                }

                var scanDescription = reader.GetScanDescription(spectrumIndex);
                Assert.IsTrue(string.IsNullOrWhiteSpace(scanDescription), "Scan description is typically null for Bruker .d directories");

                var expectedId = scanNumber.ToString();
                var expectedNativeId = string.Format("scan={0}", scanNumber);

                Assert.AreEqual(spectrum.Id, expectedId, "Id is not in the expected format for scan {0}", scanNumber);

#pragma warning disable 618
                Assert.AreEqual(spectrum.NativeId, expectedNativeId, "NativeId is not in the expected format for scan {0}", scanNumber);
#pragma warning restore 618
            }

            Console.WriteLine("scanCountMS1={0}", scanCountMS1);
            Console.WriteLine("scanCountMS2={0}", scanCountMS2);

            Assert.AreEqual(expectedMS1, scanCountMS1, "MS1 scan count mismatch");
            Assert.AreEqual(expectedMS2, scanCountMS2, "MS2 scan count mismatch");
        }

        [Test]
        [TestCase("Humira_100fmol_20121026_hi_res_9_01_716.d", 5, 9)]
        [TestCase("Blank-2_05May16_Leopard_Infuse_1_01_7976.d", 1, 1)]
        public void TestGetScanData(string dotDFolderName, int scanStart, int scanEnd)
        {
            var expectedData = new Dictionary<string, Dictionary<int, string>>();

            // Keys in this dictionary are the scan number of data being retrieved
            var file1Data = new Dictionary<int, string>
            {
                {5, "5 39775    39775    982.741  0.0E+0   1410.843 8.7E+4"},
                {6, "6 39775    39775    982.741  0.0E+0   1410.843 9.1E+4"},
                {7, "7 39775    39775    982.741  0.0E+0   1410.843 8.7E+4"},
                {8, "8 39775    39775    982.741  0.0E+0   1410.843 4.5E+4"},
                {9, "9 39775    39775    982.741  0.0E+0   1410.843 1.4E+5"}
            };

            expectedData.Add("Humira_100fmol_20121026_hi_res_9_01_716", file1Data);

            var file2Data = new Dictionary<int, string>
            {
                {1, "1 7615807  7615807  110.561  3.8E+4   202.468  4.2E+5"}
            };
            expectedData.Add("Blank-2_05May16_Leopard_Infuse_1_01_7976", file2Data);

            var dataFolder = GetBrukerDataFolder(dotDFolderName);

            using var reader = new MSDataFileReader(dataFolder.FullName);

            Console.WriteLine("Scan data for {0}", dataFolder.Name);
            Console.WriteLine("{0} {1,-8} {2,-8} {3,-8} {4,-8} {5,-8} {6}",
                "Scan", "MzCount", "IntCount",
                "FirstMz", "FirstInt", "MidMz", "MidInt");

            var scanNumberToIndexMap = reader.GetScanToIndexMapping();

            foreach (var scan in scanNumberToIndexMap.Where(x => x.Key >= scanStart && x.Key <= scanEnd))
            {
                var scanNumber = scan.Key;
                var spectrumIndex = scan.Value;

                var spectrum = reader.GetSpectrum(spectrumIndex);

                var dataPointsRead = spectrum.Mzs.Length;

                Assert.IsTrue(dataPointsRead > 0, "GetScanData returned 0 for scan {0}", scanNumber);

                var midPoint = (int)(spectrum.Intensities.Length / 2f);

                var scanSummary =
                    string.Format(
                        "{0} {1,-8} {2,-8} {3,-8:0.000} {4,-8:0.0E+0} {5,-8:0.000} {6:0.0E+0}",
                        scanNumber,
                        spectrum.Mzs.Length, spectrum.Intensities.Length,
                        spectrum.Mzs[0], spectrum.Intensities[0],
                        spectrum.Mzs[midPoint], spectrum.Intensities[midPoint]);

                Console.WriteLine(scanSummary);

                if (!expectedData.TryGetValue(Path.GetFileNameWithoutExtension(dataFolder.Name), out var expectedDataThisFile))
                {
                    Assert.Fail("Dataset {0} not found in dictionary expectedData", dataFolder.Name);
                }

                if (expectedDataThisFile.TryGetValue(scanNumber, out var expectedDataDetails))
                {
                    Assert.AreEqual(expectedDataDetails, scanSummary,
                        "Scan details mismatch, scan " + scanNumber);
                }
            }
        }

        /// <summary>
        /// Get a DirectoryInfo object for the given .D directory
        /// </summary>
        /// <param name="dotDDirectoryName">.D directory name</param>
        public static DirectoryInfo GetBrukerDataFolder(string dotDDirectoryName)
        {
            if (InstrumentDataUtilities.FindInstrumentData(dotDDirectoryName, true, out var instrumentDataDirectory))
            {
                if (instrumentDataDirectory is DirectoryInfo dotDDirectory)
                    return dotDDirectory;
            }

            Assert.Fail("Directory not found: " + dotDDirectoryName);
            return null;
        }
    }
}
