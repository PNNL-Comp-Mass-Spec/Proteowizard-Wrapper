using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using pwiz.ProteowizardWrapper;
using ThermoRawFileReader;

// ReSharper disable StringLiteralTypo
namespace ProteowizardWrapperUnitTests
{
    [TestFixture]
    public class ThermoScanDataTests
    {
        // ReSharper disable CommentTypo

        // Ignore Spelling: cid, etd, hcd, sa
        // Ignore Spelling: Bruker, Daltonics, Lumos, Orbitrap, solarix

        // ReSharper restore CommentTypo

        [Test]
        [TestCase("Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20.RAW")]
        [TestCase("HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53.raw")]
        [TestCase("HCC-38_ETciD_EThcD_07Jan16_Pippin_15-08-53.raw")]
        [TestCase("MZ0210MnxEF889ETD.raw")]
        public void TestGetCollisionEnergy(string rawFileName)
        {
            // Keys in this dictionary are filename, values are Collision Energies by scan
            var expectedData = new Dictionary<string, Dictionary<int, List<double>>>();

            var ce30 = new List<double> { 30.00 };
            var ce45 = new List<double> { 45.00 };
            //var ce120 = new List<double> { 120.550003 }; // This is apparently the value ProteoWizard read when using MSFileReader
            var ce120 = new List<double> { 120.551193 }; // ProteoWizard gets this value from RawFileReader
            var ms1Scan = new List<double>();
            var etdScanBuggyResults = new List<double>();

            // Keys in this dictionary are scan number and values are collision energies
            var file1Data = new Dictionary<int, List<double>>
            {
                {2250, ce45},
                {2251, ce45},
                {2252, ce45},
                {2253, ms1Scan},
                {2254, ce45},
                {2255, ce45},
                {2256, ce45},
                {2257, ms1Scan},
                {2258, ce45},
                {2259, ce45},
                {2260, ce45}
            };
            expectedData.Add("Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20", file1Data);

            var file2Data = new Dictionary<int, List<double>>
            {
                {39000, ce30},
                {39001, ce30},
                {39002, ms1Scan},
                {39003, ce30},
                {39004, ce30},
                {39005, ce30},
                {39006, etdScanBuggyResults},   // This is an ETD scan with collision energy 120.55
                {39007, ce120},                 // Actually has two collision energies (120.55 and 20.00) but ProteoWizard only reports 120.55
                {39008, ce120},
                {39009, ce30},
                {39010, ce30}
            };
            expectedData.Add("HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53", file2Data);

            var file3Data = new Dictionary<int, List<double>>
            {
                {19000, etdScanBuggyResults},   // This is an ETD scan with collision energy 120.55
                {19001, ce120},
                {19002, ce120},
                {19003, ms1Scan},
                {19004, ce30},
                {19005, ce30},
                {19006, ce30},
                {19007, etdScanBuggyResults},   // This is an ETD scan with collision energy 120.55
                {19008, ce120},
                {19009, ce120},
                {19010, ce30}
            };
            expectedData.Add("HCC-38_ETciD_EThcD_07Jan16_Pippin_15-08-53", file3Data);

            var file4Data = new Dictionary<int, List<double>>
            {
                {1, etdScanBuggyResults}, // This is an ETD scan with collision energy 30.00
                {2, etdScanBuggyResults}, // This is an ETD scan with collision energy 30.00

            };
            expectedData.Add("MZ0210MnxEF889ETD", file4Data);

            var dataFile = GetRawDataFile(rawFileName);

            var datasetName = Path.GetFileNameWithoutExtension(dataFile.Name);

            if (!expectedData.TryGetValue(datasetName, out var collisionEnergiesThisFile))
            {
                Assert.Fail("Dataset {0} not found in dictionary expectedData", datasetName);
            }

            // Keys are scan number, values are the list of collision energies
            var collisionEnergiesActual = new Dictionary<int, List<double>>();

            // Keys are scan number, values are msLevel
            var msLevelsActual = new Dictionary<int, int>();

            // Keys are scan number, values are the ActivationType (or list of activation types), for example cid, etd, hcd
            var activationTypesActual = new Dictionary<int, List<string>>();

            using var reader = new MSDataFileReader(dataFile.FullName);

            Console.WriteLine("Examining data in " + dataFile.Name);

            var scanNumberToIndexMap = reader.GetScanToIndexMapping();

            foreach (var scanNumber in collisionEnergiesThisFile.Keys)
            {
                if (!scanNumberToIndexMap.TryGetValue(scanNumber, out var spectrumIndex))
                {
                    Assert.Fail("ScanToIndexMap does not contain scan number " + scanNumber);
                }

                var spectrum = reader.GetSpectrum(spectrumIndex, false);

                Assert.IsTrue(spectrum != null, "GetSpectrum returned a null object for scan " + scanNumber);

                var precursors = reader.GetPrecursors(spectrumIndex);

                var collisionEnergiesThisScan = (from precursor in precursors
                                                 select precursor.PrecursorCollisionEnergy
                                                 into collisionEnergy
                                                 where collisionEnergy != null
                                                 select (double)collisionEnergy).ToList();

                collisionEnergiesActual.Add(scanNumber, collisionEnergiesThisScan);

                msLevelsActual.Add(scanNumber, spectrum.Level);

                var activationTypes = new List<string>();

                foreach (var precursor in precursors)
                {
                    if (precursor.ActivationTypes != null)
                    {
                        activationTypes.AddRange(precursor.ActivationTypes);
                    }
                }

                activationTypesActual.Add(scanNumber, activationTypes.Distinct().ToList());
            }

            Console.WriteLine("{0,-5} {1,-5} {2}", "Valid", "Scan", "Collision Energy");

            foreach (var actualEnergiesOneScan in (from item in collisionEnergiesActual orderby item.Key select item))
            {
                var scanNumber = actualEnergiesOneScan.Key;

                var expectedEnergies = collisionEnergiesThisFile[scanNumber];

                var activationTypes = string.Join(", ", activationTypesActual[scanNumber]);

                if (actualEnergiesOneScan.Value.Count == 0)
                {
                    var msLevel = msLevelsActual[scanNumber];

                    if (msLevel != 1)
                    {
                        var msg = string.Format(
                            "Scan {0} has no collision energies, which should only be true for spectra with msLevel=1. This scan has msLevel={1} and activationType={2}",
                            scanNumber, msLevel, activationTypes);
                        Console.WriteLine(msg);

                        if (activationTypes.Contains("etd"))
                        {
                            // ProteoWizard has a bug where the collision energy is not reported correctly for etd spectra
                            // Thus, skip the assertion
                        }
                        else
                        {
                            Assert.Fail(msg);
                        }
                    }
                    else
                    {
                        Console.WriteLine("{0,-5} {1,-5} {2}", true, scanNumber, "MS1 scan");
                    }
                }
                else
                {
                    foreach (var actualEnergy in actualEnergiesOneScan.Value)
                    {
                        var isValid = expectedEnergies.Any(expectedEnergy => Math.Abs(actualEnergy - expectedEnergy) < 0.00001);

                        Console.WriteLine("{0,-5} {1,-5} {2:0.00}", isValid, scanNumber, actualEnergy);

                        Assert.IsTrue(isValid, "Unexpected collision energy {0:0.00} for scan {1}", actualEnergy, scanNumber);
                    }
                }

                if (expectedEnergies.Count != actualEnergiesOneScan.Value.Count)
                {
                    var msg = string.Format("Collision energy count mismatch for scan {0}", scanNumber);
                    Console.WriteLine(msg);
                    Assert.AreEqual(expectedEnergies.Count, actualEnergiesOneScan.Value.Count, msg);
                }
            }
        }

        [Test]
        [TestCase("blank_MeOH-3_18May16_Rainier_Thermo_10344958.raw", 1500, 1900, 190, 211)]
        [TestCase("Corrupt_Qc_Shew_13_04_pt1_a_5Sep13_Cougar_13-06-14.raw", 500, 600, 0, 0)]
        [TestCase("Corrupt_QC_Shew_07_03_pt25_e_6Apr08_Falcon_Fst-75-1.raw", 500, 600, 0, 0)]
        // This file causes .NET to become unstable and aborts the unit tests
        // [TestCase("Corrupt_Scans6920-7021_AID_STM_013_101104_06_LTQ_16Nov04_Earth_0904-8.raw", 6900, 7050, 10, 40, 6920, 7021)]
        public void TestCorruptDataHandling(
            string rawFileName,
            int scanStart,
            int scanEnd,
            int expectedMS1,
            int expectedMS2)
        {
            var dataFile = GetRawDataFile(rawFileName);

            try
            {
                using var reader = new MSDataFileReader(dataFile.FullName);

                var scanCount = reader.SpectrumCount;
                Console.WriteLine("Scan count for {0}: {1}", dataFile.Name, scanCount);

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
                        var spectrum = reader.GetSpectrum(spectrumIndex, getBinaryData: true);

                        var cvScanInfo = reader.GetSpectrumScanInfo(spectrumIndex);

                        Assert.IsTrue(cvScanInfo != null, "GetSpectrumScanInfo returned a null object for scan {0}", scanNumber);

                        var filterText = reader.GetScanFilterText(spectrumIndex);

                        Assert.IsFalse(string.IsNullOrEmpty(filterText), "FilterText is empty but should not be");

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
                    Console.WriteLine("Error opening .raw file (this was expected):\n{0}", ex.Message);
                }
                else
                {
                    var msg = string.Format("Exception opening .raw file {0}:\n{1}", rawFileName, ex.Message);
                    Console.WriteLine(msg);
                    Assert.Fail(msg);
                }
            }
        }

        [Test]
        [TestCase("2016_04_12_Background_000001.d", true, "Bruker Daltonics solarix series", "000000.00000", "4/12/2016 2:10:30 AM")]
        [TestCase("Angiotensin_AllScans.raw", false, "Orbitrap Fusion Lumos", "FSN20129", "9/26/2018 9:06:07 AM")]
        [TestCase("blank_MeOH-3_18May16_Rainier_Thermo_10344958.raw", false, "LTQ Orbitrap Elite", "SN03066B", "5/19/2016 6:57:13 AM")]
        [TestCase("Blank-2_05May16_Leopard_Infuse_1_01_7976.d", true, "Bruker Daltonics solarix series", "000000.00000", "5/5/2016 10:59:32 AM")]
        [TestCase("blk_1_01_651.d", true, "Bruker Daltonics solarix series", "000000.00000", "11/27/2013 9:47:06 AM")]
        [TestCase("HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53.raw", false, "Orbitrap Fusion Lumos", "FSN20129", "1/11/2016 1:38:24 AM")]
        [TestCase("Humira_100fmol_20121026_hi_res_9_01_716.d", true, "Bruker Daltonics solarix series", "-1.0", "10/26/2012 8:06:23 AM")]
        [TestCase("lowdose_IMAC_iTRAQ1_PQDMSA.raw", false, "LTQ Orbitrap", "SN1006B", "7/28/2009 5:25:40 AM")]
        [TestCase("MZ20150721blank2.raw", false, "LTQ Orbitrap Elite", "SN03066B", "7/22/2015 4:02:34 AM")]
        [TestCase("MZ20160603PPS_edta_000004.d", true, "Bruker Daltonics solarix series", "000000.00000", "6/3/2016 4:07:01 AM")]
        [TestCase("Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20.RAW", false, "Thermo Electron instrument model", "LC000718", "10/15/2004 3:22:43 PM")]
        public void TestGetInstrumentInfo(
            string dataFileOrDirectoryName,
            bool isBruker,
            string expectedModel,
            string expectedSerialNumber,
            string expectedStartTime)
        {
            FileSystemInfo dataFileOrDirectory;

            if (isBruker)
            {
                dataFileOrDirectory = BrukerScanDataTests.GetBrukerDataFolder(dataFileOrDirectoryName);
            }
            else
            {
                dataFileOrDirectory = GetRawDataFile(dataFileOrDirectoryName);
            }

            using var reader = new MSDataFileReader(dataFileOrDirectory.FullName);

            var instrumentConfigInfo = reader.GetInstrumentConfigInfoList();
            var serialNumber = reader.GetInstrumentSerialNumber();
            var runStartTime = reader.RunStartTime;

            var instrumentModel = string.Empty;

            foreach (var item in instrumentConfigInfo)
            {
                Console.WriteLine("{0,-14}: {1}", "Model", item.Model);
                Console.WriteLine("{0,-14}: {1}", "Analyzer", item.Analyzer);
                Console.WriteLine("{0,-14}: {1}", "Detector", item.Detector);
                Console.WriteLine("{0,-14}: {1}", "Ionization", item.Ionization);
                Console.WriteLine();

                if (string.IsNullOrWhiteSpace(instrumentModel))
                    instrumentModel = item.Model;
            }

            Console.WriteLine("{0,-14}: {1}", "Serial Number", serialNumber);
            Console.WriteLine("{0,-14}: {1}", "Start Time", runStartTime);

            if (runStartTime.HasValue && instrumentModel.StartsWith("Bruker"))
            {
                // The start time for Bruker datasets is not the local time
                // Convert using .ToUniversalTime
                var localTime = runStartTime.Value.ToUniversalTime();
                Console.WriteLine("{0,-14}: {1}", "Local Time", localTime);
            }

            Console.WriteLine();

            Assert.AreEqual(expectedModel, instrumentModel, "Instrument model mismatch");

            Assert.AreEqual(expectedSerialNumber, serialNumber, "Serial number mismatch");

            if (string.IsNullOrWhiteSpace(expectedStartTime))
            {
                return;
            }

            if (!runStartTime.HasValue)
            {
                Assert.Fail("RunStartTime does not have a value; it should be {0}", expectedStartTime);
            }

            var expectedTime = DateTime.Parse(expectedStartTime);

            var timeDiff = Math.Abs(expectedTime.Subtract(runStartTime.Value).TotalSeconds);

            // The following includes timeDiff - 3600 to allow for daylight savings issues

            Assert.IsTrue(timeDiff < 5 || Math.Abs(timeDiff - 3600) < 5, "Actual start time differs from the expected value: {0} vs. {1}", runStartTime.Value, expectedTime);
        }

        [Test]
        [TestCase("Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20.RAW", 3316)]
        [TestCase("HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53.raw", 71147)]
        [TestCase("QC_mam_16_01_125ng_CPTACpt7-3s-a_02Nov17_Pippin_REP-17-10-01.raw", 126)]
        [TestCase("Blank04_29Mar17_Smeagol.raw", 4330)]                                         // SRM data
        [TestCase("calmix_Q3_10192022_03.raw", 20)]                                             // MRM data (Q3MS)
        [TestCase("20181115_arginine_Gua13C_CIDcol25_158_HCDcol35.raw", 34)]                    // MS3 scans
        public void TestGetNumScans(string rawFileName, int expectedResult)
        {
            var dataFile = GetRawDataFile(rawFileName);

            using var reader = new MSDataFileReader(dataFile.FullName);

            var scanCount = reader.SpectrumCount;

            Console.WriteLine("Scan count for {0}: {1}", dataFile.Name, scanCount);
            Assert.AreEqual(expectedResult, scanCount, "Scan count mismatch");
        }

        [Test]
        [TestCase("B5_50uM_MS_r1.RAW", 1, 20, 20, 0, 20)]
        [TestCase("MNSLTFKK_ms.raw", 1, 88, 88, 0, 88)]
        [TestCase("QCShew200uL.raw", 4000, 4100, 101, 0, 8151)]
        [TestCase("Wrighton_MT2_SPE_200avg_240k_neg_330-380.raw", 1, 200, 200, 0, 200)]
        [TestCase("1229_02blk1.raw", 6000, 6100, 77, 24, 16142)]
        [TestCase("MCF7_histone_32_49B_400min_HCD_ETD_01172014_b.raw", 2300, 2400, 18, 83, 8237)]
        [TestCase("lowdose_IMAC_iTRAQ1_PQDMSA.raw", 15000, 15100, 16, 85, 27282)]
        [TestCase("MZ20150721blank2.raw", 1, 434, 62, 372, 434)]
        [TestCase("OG_CEPC_PU_22Oct13_Legolas_13-05-12.raw", 5000, 5100, 9, 92, 11715)]
        [TestCase("blank_MeOH-3_18May16_Rainier_Thermo_10344958.raw", 1500, 1900, 190, 211, 3139)]
        [TestCase("HCC-38_ETciD_EThcD_07Jan16_Pippin_15-08-53.raw", 25200, 25600, 20, 381, 39157)]
        [TestCase("MeOHBlank03POS_11May16_Legolas_HSS-T3_A925.raw", 5900, 6000, 8, 93, 7906)]
        [TestCase("IPA-blank-07_25Oct13_Gimli.raw", 1750, 1850, 101, 0, 3085)]
        [TestCase("MM_Strap_IMAC_FT_10xDilution_FAIMS_ID_01_FAIMS_Merry_03Feb23_REP-22-11-13.raw", 42000, 42224, 9, 216, 92550, true)] // DIA data
        public void TestGetScanCountsByScanType(
            string rawFileName,
            int scanStart,
            int scanEnd,
            int expectedMS1,
            int expectedMS2,
            int expectedTotalScanCount,
            bool skipIfMissing = false)
        {
            // Keys in this dictionary are filename, values are ScanCounts by collision mode, where the key is a Tuple of ScanType and FilterString
            var expectedData = new Dictionary<string, Dictionary<Tuple<string, string>, int>>();

            // Keys in this dictionary are scan type, values are a Dictionary of FilterString and the number of scans with that filter string
            AddExpectedTupleAndCount(expectedData, "B5_50uM_MS_r1", "Q1MS", "+ c NSI Q1MS", 20);

            AddExpectedTupleAndCount(expectedData, "MNSLTFKK_ms", "Q1MS", "+ p NSI Q1MS", 88);

            AddExpectedTupleAndCount(expectedData, "QCShew200uL", "Q3MS", "+ c NSI Q3MS", 101);

            AddExpectedTupleAndCount(expectedData, "Wrighton_MT2_SPE_200avg_240k_neg_330-380", "SIM ms", "FTMS - p NSI SIM ms", 200);

            const string file5 = "1229_02blk1";
            AddExpectedTupleAndCount(expectedData, file5, "MS", "ITMS + c NSI Full ms", 8);
            AddExpectedTupleAndCount(expectedData, file5, "CID-MSn", "ITMS + c NSI d Full ms2 0@cid35.00", 24);
            AddExpectedTupleAndCount(expectedData, file5, "SIM ms", "ITMS + p NSI SIM ms", 69);

            const string file6 = "MCF7_histone_32_49B_400min_HCD_ETD_01172014_b";
            AddExpectedTupleAndCount(expectedData, file6, "HMS", "FTMS + p NSI Full ms", 9);
            AddExpectedTupleAndCount(expectedData, file6, "ETD-HMSn", "FTMS + p NSI d Full ms2 0@etd25.00", 46);
            AddExpectedTupleAndCount(expectedData, file6, "HCD-HMSn", "FTMS + p NSI d Full ms2 0@hcd28.00", 37);
            AddExpectedTupleAndCount(expectedData, file6, "SIM ms", "FTMS + p NSI d SIM ms", 9);

            const string file7 = "lowdose_IMAC_iTRAQ1_PQDMSA";
            AddExpectedTupleAndCount(expectedData, file7, "HMS", "FTMS + p NSI Full ms", 16);
            AddExpectedTupleAndCount(expectedData, file7, "CID-MSn", "ITMS + c NSI d Full ms2 0@cid35.00", 43);
            AddExpectedTupleAndCount(expectedData, file7, "PQD-MSn", "ITMS + c NSI d Full ms2 0@pqd22.00", 42);

            const string file8 = "MZ20150721blank2";
            AddExpectedTupleAndCount(expectedData, file8, "HMS", "FTMS + p NSI Full ms", 62);
            AddExpectedTupleAndCount(expectedData, file8, "ETD-HMSn", "FTMS + p NSI d Full ms2 0@etd20.00", 186);
            AddExpectedTupleAndCount(expectedData, file8, "ETD-HMSn", "FTMS + p NSI d Full ms2 0@etd25.00", 186);

            const string file9 = "OG_CEPC_PU_22Oct13_Legolas_13-05-12";
            AddExpectedTupleAndCount(expectedData, file9, "HMS", "FTMS + p NSI Full ms", 9);
            AddExpectedTupleAndCount(expectedData, file9, "CID-MSn", "ITMS + c NSI d Full ms2 0@cid35.00", 46);
            AddExpectedTupleAndCount(expectedData, file9, "ETD-MSn", "ITMS + c NSI d Full ms2 0@etd1000.00", 1);
            AddExpectedTupleAndCount(expectedData, file9, "ETD-MSn", "ITMS + c NSI d Full ms2 0@etd333.33", 1);
            AddExpectedTupleAndCount(expectedData, file9, "ETD-MSn", "ITMS + c NSI d Full ms2 0@etd400.00", 8);
            AddExpectedTupleAndCount(expectedData, file9, "ETD-MSn", "ITMS + c NSI d Full ms2 0@etd500.00", 8);
            AddExpectedTupleAndCount(expectedData, file9, "ETD-MSn", "ITMS + c NSI d Full ms2 0@etd666.67", 56);
            AddExpectedTupleAndCount(expectedData, file9, "SA_ETD-MSn", "ITMS + c NSI d sa Full ms2 0@etd1000.00", 5);
            AddExpectedTupleAndCount(expectedData, file9, "SA_ETD-MSn", "ITMS + c NSI d sa Full ms2 0@etd285.71", 1);
            AddExpectedTupleAndCount(expectedData, file9, "SA_ETD-MSn", "ITMS + c NSI d sa Full ms2 0@etd333.33", 1);
            AddExpectedTupleAndCount(expectedData, file9, "SA_ETD-MSn", "ITMS + c NSI d sa Full ms2 0@etd400.00", 14);
            AddExpectedTupleAndCount(expectedData, file9, "SA_ETD-MSn", "ITMS + c NSI d sa Full ms2 0@etd500.00", 32);
            AddExpectedTupleAndCount(expectedData, file9, "SA_ETD-MSn", "ITMS + c NSI d sa Full ms2 0@etd666.67", 260);

            const string file10 = "blank_MeOH-3_18May16_Rainier_Thermo_10344958";
            AddExpectedTupleAndCount(expectedData, file10, "HMS", "FTMS - p ESI Full ms", 190);
            AddExpectedTupleAndCount(expectedData, file10, "CID-HMSn", "FTMS - c ESI d Full ms2 0@cid35.00", 207);
            AddExpectedTupleAndCount(expectedData, file10, "CID-HMSn", "FTMS - c ESI d Full ms3 0@cid35.00 0@cid35.00", 4);

            const string file11 = "HCC-38_ETciD_EThcD_07Jan16_Pippin_15-08-53";
            AddExpectedTupleAndCount(expectedData, file11, "HMS", "FTMS + p NSI Full ms", 20);
            AddExpectedTupleAndCount(expectedData, file11, "CID-MSn", "ITMS + c NSI r d Full ms2 0@cid30.00", 231);
            AddExpectedTupleAndCount(expectedData, file11, "ETciD-MSn", "ITMS + c NSI r d sa Full ms2 0@etd120.55@cid20.00", 46);
            AddExpectedTupleAndCount(expectedData, file11, "ETciD-MSn", "ITMS + c NSI r d sa Full ms2 0@etd53.58@cid20.00", 4);
            AddExpectedTupleAndCount(expectedData, file11, "ETD-MSn", "ITMS + c NSI r d Full ms2 0@etd120.55", 46);
            AddExpectedTupleAndCount(expectedData, file11, "ETD-MSn", "ITMS + c NSI r d Full ms2 0@etd53.58", 4);
            AddExpectedTupleAndCount(expectedData, file11, "EThcD-MSn", "ITMS + c NSI r d sa Full ms2 0@etd120.55@hcd20.00", 46);
            AddExpectedTupleAndCount(expectedData, file11, "EThcD-MSn", "ITMS + c NSI r d sa Full ms2 0@etd53.58@hcd20.00", 4);

            const string file12 = "MeOHBlank03POS_11May16_Legolas_HSS-T3_A925";
            AddExpectedTupleAndCount(expectedData, file12, "HMS", "FTMS + p ESI Full ms", 8);
            AddExpectedTupleAndCount(expectedData, file12, "CID-MSn", "ITMS + c ESI d Full ms2 0@cid35.00", 47);
            AddExpectedTupleAndCount(expectedData, file12, "HCD-HMSn", "FTMS + c ESI d Full ms2 0@hcd30.00", 38);
            AddExpectedTupleAndCount(expectedData, file12, "HCD-HMSn", "FTMS + c ESI d Full ms2 0@hcd35.00", 8);

            AddExpectedTupleAndCount(expectedData, "IPA-blank-07_25Oct13_Gimli", "Zoom-MS", "ITMS + p NSI Z ms", 101);

            const string file13 = "MM_Strap_IMAC_FT_10xDilution_FAIMS_ID_01_FAIMS_Merry_03Feb23_REP-22-11-13";
            AddExpectedTupleAndCount(expectedData, file13, "HMS", "FTMS + p NSI cv=-40.00 Full ms", 3);
            AddExpectedTupleAndCount(expectedData, file13, "HMS", "FTMS + p NSI cv=-60.00 Full ms", 3);
            AddExpectedTupleAndCount(expectedData, file13, "HMS", "FTMS + p NSI cv=-80.00 Full ms", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-40.00 Full ms2 377.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-40.00 Full ms2 419.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-40.00 Full ms2 448.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-40.00 Full ms2 473.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-40.00 Full ms2 497.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-40.00 Full ms2 520.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-40.00 Full ms2 542.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-40.00 Full ms2 564.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-40.00 Full ms2 587.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-40.00 Full ms2 610.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-40.00 Full ms2 635.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-40.00 Full ms2 660.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-40.00 Full ms2 685.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-40.00 Full ms2 712.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-40.00 Full ms2 741.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-40.00 Full ms2 771.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-40.00 Full ms2 803.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-40.00 Full ms2 838.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-40.00 Full ms2 877.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-40.00 Full ms2 921.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-40.00 Full ms2 972.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-40.00 Full ms2 1034.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-40.00 Full ms2 1133.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-40.00 Full ms2 1423.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-60.00 Full ms2 377.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-60.00 Full ms2 419.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-60.00 Full ms2 448.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-60.00 Full ms2 473.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-60.00 Full ms2 497.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-60.00 Full ms2 520.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-60.00 Full ms2 542.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-60.00 Full ms2 564.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-60.00 Full ms2 587.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-60.00 Full ms2 610.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-60.00 Full ms2 635.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-60.00 Full ms2 660.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-60.00 Full ms2 685.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-60.00 Full ms2 712.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-60.00 Full ms2 741.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-60.00 Full ms2 771.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-60.00 Full ms2 803.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-60.00 Full ms2 838.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-60.00 Full ms2 877.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-60.00 Full ms2 921.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-60.00 Full ms2 972.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-60.00 Full ms2 1034.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-60.00 Full ms2 1133.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-60.00 Full ms2 1423.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-80.00 Full ms2 377.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-80.00 Full ms2 419.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-80.00 Full ms2 448.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-80.00 Full ms2 473.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-80.00 Full ms2 497.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-80.00 Full ms2 520.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-80.00 Full ms2 542.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-80.00 Full ms2 564.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-80.00 Full ms2 587.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-80.00 Full ms2 610.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-80.00 Full ms2 635.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-80.00 Full ms2 660.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-80.00 Full ms2 685.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-80.00 Full ms2 712.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-80.00 Full ms2 741.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-80.00 Full ms2 771.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-80.00 Full ms2 803.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-80.00 Full ms2 838.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-80.00 Full ms2 877.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-80.00 Full ms2 921.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-80.00 Full ms2 972.0@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-80.00 Full ms2 1034.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-80.00 Full ms2 1133.5@hcd32.00", 3);
            AddExpectedTupleAndCount(expectedData, file13, "DIA-HCD-HMSn", "FTMS + p NSI cv=-80.00 Full ms2 1423.5@hcd32.00", 3);

            var dataFile = GetRawDataFile(rawFileName, skipIfMissing);
            var errorCount = 0;

            using var reader = new MSDataFileReader(dataFile.FullName);

            Console.WriteLine("Parsing scan headers for {0}", dataFile.Name);

            var scanCount = reader.SpectrumCount;
            Console.WriteLine("Total scans: {0}", scanCount);
            Assert.AreEqual(expectedTotalScanCount, scanCount, "Total scan count mismatch");
            Console.WriteLine();

            var scanNumberToIndexMap = reader.GetScanToIndexMapping();

            var scanCountMS1 = 0;
            var scanCountMS2 = 0;
            var scanTypeCountsActual = new Dictionary<Tuple<string, string>, int>();
            var lastProgress = DateTime.Now;

            foreach (var scan in scanNumberToIndexMap.Where(x => x.Key >= scanStart && x.Key <= scanEnd))
            {
                var scanNumber = scan.Key;
                var spectrumIndex = scan.Value;

                var spectrum = reader.GetSpectrum(spectrumIndex, false);

                var cvScanInfo = reader.GetSpectrumScanInfo(spectrumIndex);

                Assert.IsTrue(cvScanInfo != null, "GetSpectrumScanInfo returned a null object for scan {0}", scanNumber);

                reader.GetScanMetadata(spectrumIndex, out _, out _, out _, out _, out _, out var isolationWindowWidth);

                var filterText = reader.GetScanFilterText(spectrumIndex);

                if (string.IsNullOrWhiteSpace(filterText))
                {
                    Console.WriteLine("No filter string for scan {0}", scanNumber);

                    errorCount++;
                    if (errorCount > 25)
                        return;

                    continue;
                }

                var isDIA = spectrum.Level > 1 && isolationWindowWidth >= 6.5;
                var includeParentMZ = isDIA;

                var scanType = XRawFileIO.GetScanTypeNameFromThermoScanFilterText(filterText, isDIA, null);
                var genericScanFilter = XRawFileIO.MakeGenericThermoScanFilter(filterText, includeParentMZ);

                var scanTypeKey = new Tuple<string, string>(scanType, genericScanFilter);

                if (scanTypeCountsActual.TryGetValue(scanTypeKey, out var observedScanCount))
                {
                    scanTypeCountsActual[scanTypeKey] = observedScanCount + 1;
                }
                else
                {
                    scanTypeCountsActual.Add(scanTypeKey, 1);
                }

                if (spectrum.Level > 1)
                    scanCountMS2++;
                else
                    scanCountMS1++;

                if (DateTime.Now.Subtract(lastProgress).TotalSeconds > 10)
                {
                    lastProgress = DateTime.Now;
                    Console.WriteLine(" ... scan {0}", scanNumber);
                }
            }

            Console.WriteLine("scanCountMS1={0}", scanCountMS1);
            Console.WriteLine("scanCountMS2={0}", scanCountMS2);

            Assert.AreEqual(expectedMS1, scanCountMS1, "MS1 scan count mismatch");
            Assert.AreEqual(expectedMS2, scanCountMS2, "MS2 scan count mismatch");

            var datasetName = Path.GetFileNameWithoutExtension(dataFile.Name);

            if (!expectedData.TryGetValue(datasetName, out var expectedScanInfo))
            {
                Assert.Fail("Dataset {0} not found in dictionary expectedData", datasetName);
            }

            Console.WriteLine("{0,-5} {1,5} {2}", "Valid", "Count", "ScanType");

            foreach (var scanType in (from item in scanTypeCountsActual orderby item.Key select item))
            {
                if (expectedScanInfo.TryGetValue(scanType.Key, out var expectedScanCount))
                {
                    var isValid = scanType.Value == expectedScanCount;

                    Console.WriteLine("{0,-5} {1,5} {2}", isValid, scanType.Value, scanType.Key);

                    Assert.AreEqual(expectedScanCount, scanType.Value, "Scan type count mismatch");
                }
                else
                {
                    Console.WriteLine("{0,-5} {1,5} {2}", "??", scanType.Value, scanType.Key);

                    Console.WriteLine("Unexpected scan type found: {0}", scanType.Key);
                    Assert.Fail("Unexpected scan type found: {0}", scanType.Key);
                }
            }
        }

        [Test]
        [TestCase("Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20.RAW", 1513, 1521, 3, 6)]
        [TestCase("HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53.raw", 16121, 16165, 3, 42)]
        [TestCase("QC_mam_16_01_125ng_CPTACpt7-3s-a_02Nov17_Pippin_REP-17-10-01.raw", 65, 80, 4, 12)]
        [TestCase("MM_Strap_IMAC_FT_10xDilution_FAIMS_ID_01_FAIMS_Merry_03Feb23_REP-22-11-13.raw", 42000, 42050, 2, 49, true)] // DIA data
        [TestCase("Blank04_29Mar17_Smeagol.raw", 1390, 1402, 0, 13)]                                                           // SRM data
        [TestCase("calmix_Q3_10192022_03.raw", 5, 15, 11, 0)]                                                                  // MRM data (Q3MS)
        [TestCase("20181115_arginine_Gua13C_CIDcol25_158_HCDcol35.raw", 10, 20, 0, 0)]                                         // MS3 scans
        public void TestGetScanInfo(string rawFileName, int scanStart, int scanEnd, int expectedMS1, int expectedMS2, bool skipIfMissing = false)
        {
            var expectedData = new Dictionary<string, Dictionary<int, string>>();

            // Keys in this dictionary are the scan number whose metadata is being retrieved
            var file1Data = new Dictionary<int, string>
            {
                // Scan MSLevel NumPeaks RetentionTime DriftTimeMsec LowMass HighMass TotalIonCurrent BasePeakMZ BasePeakIntensity ParentIonMZ ActivationType IonMode IsCentroided ScanStartTime IonInjectionTime FilterText IsolationWidth
                { 1513, "1   851 44.57 0 400 2000 6.3E+8 1089.978 1.2E+7     0.00          positive True    1.50 + c ESI Full ms [400.00-2000.00]                                     0.0"},
                { 1514, "2   109 44.60 0 230 1780 5.0E+6  528.128 7.2E+5   884.41 cid      positive True   28.96 + c d Full ms2 884.41@cid45.00 [230.00-1780.00]                      0.0"},
                { 1515, "2   290 44.63 0 305 2000 2.6E+7 1327.414 6.0E+6  1147.67 cid      positive True   14.13 + c d Full ms2 1147.67@cid45.00 [305.00-2000.00]                     0.0"},
                { 1516, "2   154 44.66 0 400 2000 7.6E+5 1251.554 3.7E+4  1492.90 cid      positive True  123.30 + c d Full ms2 1492.90@cid45.00 [400.00-2000.00]                     0.0"},
                { 1517, "1   887 44.69 0 400 2000 8.0E+8 1147.613 1.0E+7     0.00          positive True    1.41 + c ESI Full ms [400.00-2000.00]                                     0.0"},
                { 1518, "2   190 44.71 0 380 2000 4.6E+6 1844.618 2.7E+5  1421.21 cid      positive True   40.91 + c d Full ms2 1421.21@cid45.00 [380.00-2000.00]                     0.0"},
                { 1519, "2   165 44.74 0 380 2000 6.0E+6 1842.547 6.9E+5  1419.24 cid      positive True   37.84 + c d Full ms2 1419.24@cid45.00 [380.00-2000.00]                     0.0"},
                { 1520, "2   210 44.77 0 265 2000 1.5E+6 1361.745 4.2E+4  1014.93 cid      positive True   96.14 + c d Full ms2 1014.93@cid45.00 [265.00-2000.00]                     0.0"},
                { 1521, "1   860 44.80 0 400 2000 6.9E+8 1126.627 2.9E+7     0.00          positive True    1.45 + c ESI Full ms [400.00-2000.00]                                     0.0"},
            };
            expectedData.Add("Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20", file1Data);

            var file2Data = new Dictionary<int, string>
            {
                 { 16121, "1 11888 47.68 0 350 1550 1.9E+9  503.565 3.4E+8     0.00          positive False   0.44 FTMS + p NSI Full ms [350.0000-1550.0000]                            0.0"},
                 { 16122, "2   490 47.68 0 106  817 1.6E+6  550.309 2.1E+5   403.22 cid      positive True   11.82 ITMS + c NSI r d Full ms2 403.2206@cid30.00 [106.0000-817.0000]      2.0"},
                 { 16123, "2   785 47.68 0 143 1627 5.5E+5  506.272 4.9E+4   538.84 cid      positive True   26.07 ITMS + c NSI r d Full ms2 538.8400@cid30.00 [143.0000-1627.0000]     2.0"},
                 { 16124, "2   996 47.68 0 208 2000 7.8E+5  737.530 7.0E+4   775.94 cid      positive True   24.65 ITMS + c NSI r d Full ms2 776.2740@cid30.00 [208.0000-2000.0000]     2.0"},
                 { 16125, "2   703 47.68 0 120 1627 2.1E+5  808.486 2.2E+4   538.84 etd      positive True   42.48 ITMS + c NSI r d Full ms2 538.8400@etd53.58 [120.0000-1627.0000]     2.0"},
                 { 16126, "2   753 47.68 0 120 1627 1.4E+5  536.209 9.0E+3   538.84 cid, etd positive True   58.96 ITMS + c NSI r d sa Full ms2 538.8400@etd53.58@cid20.00 [120.0...    2.0"},
                 { 16127, "2   872 47.68 0 120 1627 1.3E+5  808.487 1.4E+4   538.84 etd, hcd positive True   58.96 ITMS + c NSI r d sa Full ms2 538.8400@etd53.58@hcd20.00 [120.0...    2.0"},
                 { 16128, "2   972 47.69 0 225 1682 4.4E+5  805.579 2.3E+4   835.88 cid      positive True   42.71 ITMS + c NSI r d Full ms2 835.8777@cid30.00 [225.0000-1682.0000]     2.0"},
                 { 16129, "2   937 47.69 0 266 1986 3.4E+5  938.679 2.9E+4   987.40 cid      positive True   35.75 ITMS + c NSI r d Full ms2 987.8934@cid30.00 [266.0000-1986.0000]     2.0"},
                 { 16130, "2   622 47.69 0 110  853 2.7E+5  411.977 1.2E+4   421.26 cid      positive True   50.98 ITMS + c NSI r d Full ms2 421.2619@cid30.00 [110.0000-853.0000]      2.0"},
                 { 16131, "2    29 47.69 0 120 1986 2.1E+4  984.504 9.5E+3   987.40 etd      positive True   26.55 ITMS + c NSI r d Full ms2 987.8934@etd120.55 [120.0000-1986.0000]    2.0"},
                 { 16132, "2   239 47.69 0 120  853 1.2E+4  421.052 6.8E+2   421.26 etd      positive True  127.21 ITMS + c NSI r d Full ms2 421.2619@etd120.55 [120.0000-853.0000]     2.0"},
                 { 16133, "2   280 47.70 0 120  853 1.5E+4  421.232 1.2E+3   421.26 cid, etd positive True  110.21 ITMS + c NSI r d sa Full ms2 421.2619@etd120.55@cid20.00 [120....    2.0"},
                 { 16134, "2   343 47.70 0 120  853 1.4E+4  838.487 7.5E+2   421.26 etd, hcd positive True  110.21 ITMS + c NSI r d sa Full ms2 421.2619@etd120.55@hcd20.00 [120....    2.0"},
                 { 16135, "2    38 47.70 0 120 1986 2.1E+4  984.498 9.2E+3   987.40 cid, etd positive True   31.82 ITMS + c NSI r d sa Full ms2 987.8934@etd120.55@cid20.00 [120....    2.0"},
                 { 16136, "2    93 47.71 0 120 1986 2.3E+4  984.491 9.4E+3   987.40 etd, hcd positive True   31.82 ITMS + c NSI r d sa Full ms2 987.8934@etd120.55@hcd20.00 [120....    2.0"},
                 { 16137, "2  1172 47.71 0 336 2000 3.5E+5 1536.038 4.7E+3  1240.76 cid      positive True   30.70 ITMS + c NSI r d Full ms2 1241.0092@cid30.00 [336.0000-2000.0000]    2.0"},
                 { 16138, "2   925 47.72 0 235 1760 2.9E+5  826.095 2.5E+4   874.84 cid      positive True   40.56 ITMS + c NSI r d Full ms2 874.8397@cid30.00 [235.0000-1760.0000]     2.0"},
                 { 16139, "2    96 47.72 0 120 1760 1.6E+4  875.506 2.1E+3   874.84 etd      positive True   45.88 ITMS + c NSI r d Full ms2 874.8397@etd120.55 [120.0000-1760.0000]    2.0"},
                 { 16140, "2   174 47.72 0 120 1760 1.8E+4 1749.846 2.0E+3   874.84 cid, etd positive True   54.15 ITMS + c NSI r d sa Full ms2 874.8397@etd120.55@cid20.00 [120....    2.0"},
                 { 16141, "2   240 47.72 0 120 1760 1.6E+4  874.664 1.6E+3   874.84 etd, hcd positive True   54.15 ITMS + c NSI r d sa Full ms2 874.8397@etd120.55@hcd20.00 [120....    2.0"},
                 { 16142, "1 13501 47.73 0 350 1550 1.3E+9  503.565 1.9E+8     0.00          positive False   0.79 FTMS + p NSI Full ms [350.0000-1550.0000]                            0.0"},
                 { 16143, "2   651 47.73 0 128  981 6.5E+5  444.288 6.4E+4   485.28 cid      positive True   22.26 ITMS + c NSI r d Full ms2 485.2789@cid30.00 [128.0000-981.0000]      2.0"},
                 { 16144, "2   512 47.73 0 101 1561 5.0E+5  591.309 4.0E+4   387.41 cid      positive True   28.19 ITMS + c NSI r d Full ms2 387.6621@cid30.00 [101.0000-1561.0000]     2.0"},
                 { 16145, "2   817 47.73 0 162 1830 4.0E+5  567.912 2.8E+4   606.29 cid      positive True   37.30 ITMS + c NSI r d Full ms2 606.6241@cid30.00 [162.0000-1830.0000]     2.0"},
                 { 16146, "2   573 47.73 0  99  770 1.9E+5  532.308 3.4E+4   379.72 cid      positive True  100.00 ITMS + c NSI r d Full ms2 379.7246@cid30.00 [99.0000-770.0000]       2.0"},
                 { 16147, "2   813 47.74 0 120 1830 3.8E+5  603.095 3.1E+4   606.29 etd      positive True   25.47 ITMS + c NSI r d Full ms2 606.6241@etd53.58 [120.0000-1830.0000]     2.0"},
                 { 16148, "2   882 47.74 0 120 1830 1.5E+5  603.076 1.3E+4   606.29 cid, etd positive True   61.48 ITMS + c NSI r d sa Full ms2 606.6241@etd53.58@cid20.00 [120.0...    2.0"},
                 { 16149, "2  1121 47.74 0 120 1830 1.6E+5  603.027 1.1E+4   606.29 etd, hcd positive True   61.48 ITMS + c NSI r d sa Full ms2 606.6241@etd53.58@hcd20.00 [120.0...    2.0"},
                 { 16150, "2   625 47.74 0  95 1108 3.8E+5  418.536 1.2E+5   365.88 cid      positive True  134.71 ITMS + c NSI r d Full ms2 365.8827@cid30.00 [95.0000-1108.0000]      2.0"},
                 { 16151, "2   679 47.75 0 146 1656 2.8E+5  501.523 4.3E+4   548.54 cid      positive True   30.59 ITMS + c NSI r d Full ms2 548.5366@cid30.00 [146.0000-1656.0000]     2.0"},
                 { 16152, "2  1171 47.75 0 328 2000 1.8E+5  848.497 2.2E+3  1210.06 cid      positive True   38.05 ITMS + c NSI r d Full ms2 1210.2963@cid30.00 [328.0000-2000.0000]    2.0"},
                 { 16153, "2   600 47.75 0 120 1656 1.3E+5  548.396 1.3E+4   548.54 etd      positive True   50.35 ITMS + c NSI r d Full ms2 548.5366@etd53.58 [120.0000-1656.0000]     2.0"},
                 { 16154, "2   566 47.75 0 120 1656 4.2E+4  548.450 4.2E+3   548.54 cid, etd positive True  122.26 ITMS + c NSI r d sa Full ms2 548.5366@etd53.58@cid20.00 [120.0...    2.0"},
                 { 16155, "2   753 47.76 0 120 1656 4.2E+4  550.402 3.6E+3   548.54 etd, hcd positive True  122.26 ITMS + c NSI r d sa Full ms2 548.5366@etd53.58@hcd20.00 [120.0...    2.0"},
                 { 16156, "2  1120 47.76 0 324 2000 1.5E+5 1491.872 1.0E+4  1197.16 cid      positive True   63.61 ITMS + c NSI r d Full ms2 1197.5653@cid30.00 [324.0000-2000.0000]    2.0"},
                 { 16157, "2   714 47.76 0 124  950 2.2E+5  420.689 2.2E+4   469.71 cid      positive True  100.00 ITMS + c NSI r d Full ms2 469.7129@cid30.00 [124.0000-950.0000]      2.0"},
                 { 16158, "2   692 47.76 0 306 2000 1.3E+5 1100.042 3.5E+3  1132.02 cid      positive True   27.79 ITMS + c NSI r d Full ms2 1132.0164@cid30.00 [306.0000-2000.0000]    2.0"},
                 { 16159, "2   667 47.76 0 122  935 1.9E+5  445.117 2.7E+4   462.15 cid      positive True   69.09 ITMS + c NSI r d Full ms2 462.1478@cid30.00 [122.0000-935.0000]      2.0"},
                 { 16160, "2   694 47.77 0 145 1646 3.4E+5  539.065 6.0E+4   544.84 cid      positive True   28.97 ITMS + c NSI r d Full ms2 545.1750@cid30.00 [145.0000-1646.0000]     2.0"},
                 { 16161, "2   737 47.77 0 157 1191 2.8E+5  541.462 6.0E+4   590.28 cid      positive True   37.92 ITMS + c NSI r d Full ms2 590.2814@cid30.00 [157.0000-1191.0000]     2.0"},
                 { 16162, "2   288 47.77 0 120 1191 8.4E+4 1180.615 5.1E+3   590.28 etd      positive True   38.31 ITMS + c NSI r d Full ms2 590.2814@etd120.55 [120.0000-1191.0000]    2.0"},
                 { 16163, "2   305 47.77 0 120 1191 1.8E+4 1184.614 9.0E+2   590.28 cid, etd positive True  109.20 ITMS + c NSI r d sa Full ms2 590.2814@etd120.55@cid20.00 [120....    2.0"},
                 { 16164, "2   372 47.77 0 120 1191 1.7E+4 1184.644 8.7E+2   590.28 etd, hcd positive True  109.20 ITMS + c NSI r d sa Full ms2 590.2814@etd120.55@hcd20.00 [120....    2.0"},
                 { 16165, "1 13816 47.78 0 350 1550 1.2E+9  503.565 1.6E+8     0.00          positive False   0.76 FTMS + p NSI Full ms [350.0000-1550.0000]                            0.0"},
            };
            expectedData.Add("HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53", file2Data);

            var file3Data = new Dictionary<int, string>
            {
                 { 65, "1 10413 0.16 0 350 1800 5.2E+7  371.102 1.1E+7     0.00          positive False   9.70 FTMS + p NSI Full ms [350.0000-1800.0000]                            0.0"},
                 { 66, "2    28 0.16 0 110 2000 3.3E+4  178.281 1.4E+4  1704.25 hcd      positive True  105.00 FTMS + c NSI d Full ms2 1704.2509@hcd30.00 [110.0000-2000.0000]      0.7"},
                 { 67, "2    34 0.16 0 110 2000 3.3E+4  178.282 9.4E+3  1714.28 hcd      positive True  105.00 FTMS + c NSI d Full ms2 1714.6105@hcd30.00 [110.0000-2000.0000]      0.7"},
                 { 68, "2    75 0.16 0 110 2000 1.6E+5  148.192 2.4E+4  1326.51 hcd      positive True  105.00 FTMS + c NSI d Full ms2 1327.0189@hcd30.00 [110.0000-2000.0000]      0.7"},
                 { 69, "1 11589 0.17 0 350 1800 5.4E+7  371.101 1.1E+7     0.00          positive False   9.81 FTMS + p NSI Full ms [350.0000-1800.0000]                            0.0"},
                 { 70, "2    27 0.17 0 110 2000 3.2E+4  178.278 1.2E+4  1628.65 hcd      positive True  105.00 FTMS + c NSI d Full ms2 1628.6455@hcd30.00 [110.0000-2000.0000]      0.7"},
                 { 71, "2    31 0.17 0 110 2000 3.8E+4  178.280 1.6E+4  1579.63 hcd      positive True  105.00 FTMS + c NSI d Full ms2 1579.6287@hcd30.00 [110.0000-2000.0000]      0.7"},
                 { 72, "2    38 0.17 0 110 2000 3.6E+4  178.282 8.2E+3  1700.86 hcd      positive True  105.00 FTMS + c NSI d Full ms2 1701.8641@hcd30.00 [110.0000-2000.0000]      0.7"},
                 { 73, "2    30 0.17 0 110 2000 3.7E+4  128.953 7.5E+3  1311.66 hcd      positive True  105.00 FTMS + c NSI d Full ms2 1311.6626@hcd30.00 [110.0000-2000.0000]      0.7"},
                 { 74, "2    76 0.18 0 110 2000 1.6E+5  148.196 2.2E+4  1319.71 hcd      positive True  105.00 FTMS + c NSI d Full ms2 1319.7051@hcd30.00 [110.0000-2000.0000]      0.7"},
                 { 75, "1 12606 0.18 0 350 1800 5.5E+7  371.101 1.2E+7     0.00          positive False  12.96 FTMS + p NSI Full ms [350.0000-1800.0000]                            0.0"},
                 { 76, "2    35 0.18 0 110 2000 3.8E+4  178.279 1.4E+4  1694.55 hcd      positive True  105.00 FTMS + c NSI d Full ms2 1694.5530@hcd30.00 [110.0000-2000.0000]      0.7"},
                 { 77, "2    28 0.18 0 110 2000 3.3E+4  178.281 1.3E+4  1556.25 hcd      positive True  105.00 FTMS + c NSI d Full ms2 1556.5758@hcd30.00 [110.0000-2000.0000]      0.7"},
                 { 78, "2    80 0.19 0 110 2000 1.7E+5  148.192 2.2E+4  1478.03 hcd      positive True  105.00 FTMS + c NSI d Full ms2 1478.0348@hcd30.00 [110.0000-2000.0000]      0.7"},
                 { 79, "1 14314 0.19 0 350 1800 5.5E+7  371.102 1.1E+7     0.00          positive False  17.20 FTMS + p NSI Full ms [350.0000-1800.0000]                            0.0"},
                 { 80, "2    36 0.19 0 110 2000 3.8E+4  178.279 1.5E+4  1731.89 hcd      positive True  105.00 FTMS + c NSI d Full ms2 1732.2192@hcd30.00 [110.0000-2000.0000]      0.7"},
            };
            expectedData.Add("QC_mam_16_01_125ng_CPTACpt7-3s-a_02Nov17_Pippin_REP-17-10-01", file3Data);

            // This is a DIA dataset
            var file4Data = new Dictionary<int, string>
            {
                 { 42000, "2   178 54.51 -80 200 1600 3.7E+4 1271.590 3.9E+3  1423.50 hcd      positive False  54.00 FTMS + p NSI cv=-80.00 Full ms2 1423.5000@hcd32.00 [200.0000-1...  453.0"},
                 { 42001, "1  3032 54.51 -40 350 1650 8.7E+9  379.715 4.0E+9     0.00          positive False   0.03 FTMS + p NSI cv=-40.00 Full ms [350.0000-1650.0000]                  0.0"},
                 { 42002, "2  6798 54.51 -40 200 1600 1.7E+9  244.165 3.9E+8   377.00 hcd      positive False   1.24 FTMS + p NSI cv=-40.00 Full ms2 377.0000@hcd32.00 [200.0000-16...   54.0"},
                 { 42003, "2 11160 54.51 -40 200 1600 1.7E+8  621.382 2.6E+7   419.00 hcd      positive False  12.63 FTMS + p NSI cv=-40.00 Full ms2 419.0000@hcd32.00 [200.0000-16...   32.0"},
                 { 42004, "2 13193 54.51 -40 200 1600 4.2E+7  468.292 1.4E+6   448.00 hcd      positive False  27.04 FTMS + p NSI cv=-40.00 Full ms2 448.0000@hcd32.00 [200.0000-16...   28.0"},
                 { 42005, "2 16075 54.51 -40 200 1600 2.5E+7  244.165 2.8E+6   473.50 hcd      positive False  53.97 FTMS + p NSI cv=-40.00 Full ms2 473.5000@hcd32.00 [200.0000-16...   25.0"},
                 { 42006, "2 15512 54.52 -40 200 1600 2.4E+7  649.297 1.0E+6   497.50 hcd      positive False  54.00 FTMS + p NSI cv=-40.00 Full ms2 497.5000@hcd32.00 [200.0000-16...   25.0"},
                 { 42007, "2 17828 54.52 -40 200 1600 3.8E+7  734.368 2.3E+6   520.50 hcd      positive False  54.00 FTMS + p NSI cv=-40.00 Full ms2 520.5000@hcd32.00 [200.0000-16...   23.0"},
                 { 42008, "2 17401 54.52 -40 200 1600 2.5E+7  787.362 6.5E+5   542.50 hcd      positive False  54.00 FTMS + p NSI cv=-40.00 Full ms2 542.5000@hcd32.00 [200.0000-16...   23.0"},
                 { 42009, "2 17087 54.52 -40 200 1600 5.1E+7  903.427 2.7E+6   564.50 hcd      positive False  33.22 FTMS + p NSI cv=-40.00 Full ms2 564.5000@hcd32.00 [200.0000-16...   23.0"},
                 { 42010, "2 15516 54.52 -40 200 1600 5.0E+7  950.392 2.6E+6   587.00 hcd      positive False  31.78 FTMS + p NSI cv=-40.00 Full ms2 587.0000@hcd32.00 [200.0000-16...   24.0"},
                 { 42011, "2 17001 54.52 -40 200 1600 8.7E+7 1030.550 2.8E+6   610.50 hcd      positive False  19.37 FTMS + p NSI cv=-40.00 Full ms2 610.5000@hcd32.00 [200.0000-16...   25.0"},
                 { 42012, "2 12565 54.52 -40 200 1600 8.0E+7  630.363 1.3E+7   635.00 hcd      positive False  16.09 FTMS + p NSI cv=-40.00 Full ms2 635.0000@hcd32.00 [200.0000-16...   26.0"},
                 { 42013, "2 14453 54.52 -40 200 1600 5.7E+7  786.398 3.0E+6   660.00 hcd      positive False  20.19 FTMS + p NSI cv=-40.00 Full ms2 660.0000@hcd32.00 [200.0000-16...   26.0"},
                 { 42014, "2 14412 54.52 -40 200 1600 4.6E+7  673.329 1.3E+6   685.50 hcd      positive False  21.17 FTMS + p NSI cv=-40.00 Full ms2 685.5000@hcd32.00 [200.0000-16...   27.0"},
                 { 42015, "2 16190 54.53 -40 200 1600 3.2E+7  249.159 3.9E+5   712.50 hcd      positive False  29.42 FTMS + p NSI cv=-40.00 Full ms2 712.5000@hcd32.00 [200.0000-16...   29.0"},
                 { 42016, "2 15386 54.53 -40 200 1600 4.4E+7  735.375 8.0E+5   741.00 hcd      positive False  20.43 FTMS + p NSI cv=-40.00 Full ms2 741.0000@hcd32.00 [200.0000-16...   30.0"},
                 { 42017, "2 16712 54.53 -40 200 1600 6.3E+7  758.423 2.8E+6   771.00 hcd      positive False  27.87 FTMS + p NSI cv=-40.00 Full ms2 771.0000@hcd32.00 [200.0000-16...   32.0"},
                 { 42018, "2 18091 54.53 -40 200 1600 3.1E+7  516.277 9.5E+5   803.50 hcd      positive False  41.25 FTMS + p NSI cv=-40.00 Full ms2 803.5000@hcd32.00 [200.0000-16...   35.0"},
                 { 42019, "2 14584 54.53 -40 200 1600 2.4E+7  848.866 2.6E+6   838.50 hcd      positive False  50.64 FTMS + p NSI cv=-40.00 Full ms2 838.5000@hcd32.00 [200.0000-16...   37.0"},
                 { 42020, "2 17626 54.53 -40 200 1600 2.5E+7 1049.444 6.4E+5   877.00 hcd      positive False  53.08 FTMS + p NSI cv=-40.00 Full ms2 877.0000@hcd32.00 [200.0000-16...   42.0"},
                 { 42021, "2 11799 54.53 -40 200 1600 2.7E+8 1270.576 1.6E+7   921.00 hcd      positive False   6.04 FTMS + p NSI cv=-40.00 Full ms2 921.0000@hcd32.00 [200.0000-16...   48.0"},
                 { 42022, "2  5945 54.53 -40 200 1600 4.5E+6  758.421 2.0E+5   972.00 hcd      positive False  46.88 FTMS + p NSI cv=-40.00 Full ms2 972.0000@hcd32.00 [200.0000-16...   52.0"},
                 { 42023, "2  5074 54.53 -40 200 1600 3.7E+6  758.421 2.7E+5  1034.50 hcd      positive False  54.00 FTMS + p NSI cv=-40.00 Full ms2 1034.5000@hcd32.00 [200.0000-1...   71.0"},
                 { 42024, "2  5717 54.54 -40 200 1600 5.9E+6  758.422 5.2E+5  1133.50 hcd      positive False  54.00 FTMS + p NSI cv=-40.00 Full ms2 1133.5000@hcd32.00 [200.0000-1...  129.0"},
                 { 42025, "2 10629 54.54 -40 200 1600 2.5E+7  758.422 3.2E+6  1423.50 hcd      positive False  52.04 FTMS + p NSI cv=-40.00 Full ms2 1423.5000@hcd32.00 [200.0000-1...  453.0"},
                 { 42026, "1 11849 54.54 -60 350 1650 4.7E+9  396.735 1.1E+9     0.00          positive False   0.30 FTMS + p NSI cv=-60.00 Full ms [350.0000-1650.0000]                  0.0"},
                 { 42027, "2  7339 54.54 -60 200 1600 1.3E+9  546.325 2.5E+8   377.00 hcd      positive False   1.62 FTMS + p NSI cv=-60.00 Full ms2 377.0000@hcd32.00 [200.0000-16...   54.0"},
                 { 42028, "2 11774 54.54 -60 200 1600 1.9E+8  564.276 7.0E+6   419.00 hcd      positive False   5.58 FTMS + p NSI cv=-60.00 Full ms2 419.0000@hcd32.00 [200.0000-16...   32.0"},
                 { 42029, "2 14650 54.54 -60 200 1600 9.8E+7  524.744 1.9E+6   448.00 hcd      positive False  10.15 FTMS + p NSI cv=-60.00 Full ms2 448.0000@hcd32.00 [200.0000-16...   28.0"},
                 { 42030, "2 16217 54.55 -60 200 1600 1.1E+8  716.392 1.0E+7   473.50 hcd      positive False  16.20 FTMS + p NSI cv=-60.00 Full ms2 473.5000@hcd32.00 [200.0000-16...   25.0"},
                 { 42031, "2 14986 54.55 -60 200 1600 3.3E+8  745.372 3.9E+7   497.50 hcd      positive False   9.60 FTMS + p NSI cv=-60.00 Full ms2 497.5000@hcd32.00 [200.0000-16...   25.0"},
                 { 42032, "2 16549 54.55 -60 200 1600 1.1E+8  774.435 6.4E+6   520.50 hcd      positive False  19.16 FTMS + p NSI cv=-60.00 Full ms2 520.5000@hcd32.00 [200.0000-16...   23.0"},
                 { 42033, "2 11926 54.55 -60 200 1600 8.7E+7  546.323 1.4E+7   542.50 hcd      positive False  13.00 FTMS + p NSI cv=-60.00 Full ms2 542.5000@hcd32.00 [200.0000-16...   23.0"},
                 { 42034, "2 15353 54.55 -60 200 1600 9.6E+7  902.438 4.5E+6   564.50 hcd      positive False  14.36 FTMS + p NSI cv=-60.00 Full ms2 564.5000@hcd32.00 [200.0000-16...   23.0"},
                 { 42035, "2 14499 54.55 -60 200 1600 2.5E+7  730.334 5.8E+5   587.00 hcd      positive False  30.26 FTMS + p NSI cv=-60.00 Full ms2 587.0000@hcd32.00 [200.0000-16...   24.0"},
                 { 42036, "2 14517 54.55 -60 200 1600 1.4E+7  846.396 3.6E+5   610.50 hcd      positive False  50.93 FTMS + p NSI cv=-60.00 Full ms2 610.5000@hcd32.00 [200.0000-16...   25.0"},
                 { 42037, "2 13702 54.55 -60 200 1600 2.9E+7  645.392 4.3E+6   635.00 hcd      positive False  28.19 FTMS + p NSI cv=-60.00 Full ms2 635.0000@hcd32.00 [200.0000-16...   26.0"},
                 { 42038, "2 11752 54.55 -60 200 1600 1.3E+7  660.382 5.7E+5   660.00 hcd      positive False  47.46 FTMS + p NSI cv=-60.00 Full ms2 660.0000@hcd32.00 [200.0000-16...   26.0"},
                 { 42039, "2 11107 54.56 -60 200 1600 9.4E+6  694.316 4.1E+5   685.50 hcd      positive False  54.00 FTMS + p NSI cv=-60.00 Full ms2 685.5000@hcd32.00 [200.0000-16...   27.0"},
                 { 42040, "2 13523 54.56 -60 200 1600 1.2E+7  716.393 5.8E+5   712.50 hcd      positive False  54.00 FTMS + p NSI cv=-60.00 Full ms2 712.5000@hcd32.00 [200.0000-16...   29.0"},
                 { 42041, "2 10564 54.56 -60 200 1600 1.1E+7  745.371 1.7E+6   741.00 hcd      positive False  54.00 FTMS + p NSI cv=-60.00 Full ms2 741.0000@hcd32.00 [200.0000-16...   30.0"},
                 { 42042, "2 12864 54.56 -60 200 1600 1.1E+7  772.382 7.2E+5   771.00 hcd      positive False  54.00 FTMS + p NSI cv=-60.00 Full ms2 771.0000@hcd32.00 [200.0000-16...   32.0"},
                 { 42043, "2  8694 54.56 -60 200 1600 6.3E+6  792.460 3.9E+5   803.50 hcd      positive False  54.00 FTMS + p NSI cv=-60.00 Full ms2 803.5000@hcd32.00 [200.0000-16...   35.0"},
                 { 42044, "2  5473 54.56 -60 200 1600 2.8E+6  836.381 5.4E+4   838.50 hcd      positive False  54.00 FTMS + p NSI cv=-60.00 Full ms2 838.5000@hcd32.00 [200.0000-16...   37.0"},
                 { 42045, "2  3656 54.56 -60 200 1600 3.7E+6  858.454 9.7E+5   877.00 hcd      positive False  54.00 FTMS + p NSI cv=-60.00 Full ms2 877.0000@hcd32.00 [200.0000-16...   42.0"},
                 { 42046, "2  5090 54.56 -60 200 1600 4.8E+6 1026.061 3.4E+5   921.00 hcd      positive False  54.00 FTMS + p NSI cv=-60.00 Full ms2 921.0000@hcd32.00 [200.0000-16...   48.0"},
                 { 42047, "2   680 54.57 -60 200 1600 3.8E+5  971.537 6.4E+4   972.00 hcd      positive False  54.00 FTMS + p NSI cv=-60.00 Full ms2 972.0000@hcd32.00 [200.0000-16...   52.0"},
                 { 42048, "2   443 54.57 -60 200 1600 1.9E+5 1015.521 3.5E+4  1034.50 hcd      positive False  54.00 FTMS + p NSI cv=-60.00 Full ms2 1034.5000@hcd32.00 [200.0000-1...   71.0"},
                 { 42049, "2   212 54.57 -60 200 1600 5.5E+4 1099.535 5.8E+3  1133.50 hcd      positive False  54.00 FTMS + p NSI cv=-60.00 Full ms2 1133.5000@hcd32.00 [200.0000-1...  129.0"},
                 { 42050, "2   139 54.57 -60 200 1600 2.9E+4 1213.694 3.6E+3  1423.50 hcd      positive False  54.00 FTMS + p NSI cv=-60.00 Full ms2 1423.5000@hcd32.00 [200.0000-1...  453.0"},
            };
            expectedData.Add("MM_Strap_IMAC_FT_10xDilution_FAIMS_ID_01_FAIMS_Merry_03Feb23_REP-22-11-13", file4Data);

            // This is a SRM dataset
            var file5Data = new Dictionary<int, string>
            {
                 { 1390, "2     3 20.67 0 907  907 3.1E+0 1151.503 1.0E+0   833.37 cid      positive True    0.00 + c NSI SRM ms2 833.374 [907.432-907.434, 1022.459-1022.461, 1...    0.0"},
                 { 1391, "2     3 20.68 0 637  637 5.4E+0  750.345 3.4E+0   526.26 cid      positive True    0.00 + c NSI SRM ms2 526.258 [637.260-637.262, 750.344-750.346, 938...    0.0"},
                 { 1392, "2     3 20.68 0 645  645 5.2E+0  645.275 2.4E+0   530.27 cid      positive True    0.00 + c NSI SRM ms2 530.265 [645.274-645.276, 758.358-758.360, 946...    0.0"},
                 { 1393, "2     3 20.68 0 602  602 5.7E+2  988.506 2.8E+2   647.33 cid      positive True    0.00 + c NSI SRM ms2 647.325 [602.325-602.327, 715.409-715.411, 988...    0.0"},
                 { 1394, "2     3 20.68 0 610  610 4.2E+2  610.340 2.3E+2   651.33 cid      positive True    0.00 + c NSI SRM ms2 651.332 [610.339-610.341, 723.423-723.425, 996...    0.0"},
                 { 1395, "2     3 20.69 0 897  897 3.6E+0  897.425 1.2E+0   828.37 cid      positive True    0.00 + c NSI SRM ms2 828.370 [897.424-897.426, 1012.451-1012.453, 1...    0.0"},
                 { 1396, "2     3 20.69 0 907  907 3.7E+0 1151.503 1.3E+0   833.37 cid      positive True    0.00 + c NSI SRM ms2 833.374 [907.432-907.434, 1022.459-1022.461, 1...    0.0"},
                 { 1397, "2     3 20.69 0 637  637 3.5E+0  750.345 1.2E+0   526.26 cid      positive True    0.00 + c NSI SRM ms2 526.258 [637.260-637.262, 750.344-750.346, 938...    0.0"},
                 { 1398, "2     3 20.69 0 645  645 7.7E+1  946.439 7.0E+1   530.27 cid      positive True    0.00 + c NSI SRM ms2 530.265 [645.274-645.276, 758.358-758.360, 946...    0.0"},
                 { 1399, "2     3 20.70 0 602  602 8.5E+2  988.506 3.3E+2   647.33 cid      positive True    0.00 + c NSI SRM ms2 647.325 [602.325-602.327, 715.409-715.411, 988...    0.0"},
                 { 1400, "2     3 20.70 0 610  610 8.5E+2  996.520 3.3E+2   651.33 cid      positive True    0.00 + c NSI SRM ms2 651.332 [610.339-610.341, 723.423-723.425, 996...    0.0"},
                 { 1401, "2     3 20.70 0 897  897 4.9E+0 1012.452 2.6E+0   828.37 cid      positive True    0.00 + c NSI SRM ms2 828.370 [897.424-897.426, 1012.451-1012.453, 1...    0.0"},
                 { 1402, "2     3 20.71 0 907  907 4.9E+0 1151.503 2.7E+0   833.37 cid      positive True    0.00 + c NSI SRM ms2 833.374 [907.432-907.434, 1022.459-1022.461, 1...    0.0"},
            };
            expectedData.Add("Blank04_29Mar17_Smeagol", file5Data);

            // This is a MRM dataset
            var file6Data = new Dictionary<int, string>
            {
                 { 5, "1 12857 0.04 0 150 1050 1.8E+10  508.083 2.6E+8     0.00          positive False   0.00 + p NSI Q3MS [150.152-1050.082]                                      0.0"},
                 { 6, "1 12857 0.05 0 150 1050 1.8E+10  508.293 2.6E+8     0.00          positive False   0.00 + p NSI Q3MS [150.152-1050.082]                                      0.0"},
                 { 7, "1 12857 0.06 0 150 1050 2.0E+10  508.363 2.6E+8     0.00          positive False   0.00 + p NSI Q3MS [150.152-1050.082]                                      0.0"},
                 { 8, "1 12857 0.07 0 150 1050 1.8E+10  508.293 2.6E+8     0.00          positive False   0.00 + p NSI Q3MS [150.152-1050.082]                                      0.0"},
                 { 9, "1 12857 0.08 0 150 1050 1.8E+10  508.223 2.6E+8     0.00          positive False   0.00 + p NSI Q3MS [150.152-1050.082]                                      0.0"},
                 { 10, "1 12857 0.09 0 150 1050 1.9E+10  508.293 2.6E+8     0.00          positive False   0.00 + p NSI Q3MS [150.152-1050.082]                                      0.0"},
                 { 11, "1 12857 0.10 0 150 1050 2.1E+10  508.083 2.6E+8     0.00          positive False   0.00 + p NSI Q3MS [150.152-1050.082]                                      0.0"},
                 { 12, "1 12857 0.10 0 150 1050 1.9E+10  508.363 2.6E+8     0.00          positive False   0.00 + p NSI Q3MS [150.152-1050.082]                                      0.0"},
                 { 13, "1 12857 0.11 0 150 1050 1.8E+10  508.293 2.6E+8     0.00          positive False   0.00 + p NSI Q3MS [150.152-1050.082]                                      0.0"},
                 { 14, "1 12857 0.12 0 150 1050 2.0E+10  508.083 2.6E+8     0.00          positive False   0.00 + p NSI Q3MS [150.152-1050.082]                                      0.0"},
                 { 15, "1 12857 0.13 0 150 1050 1.9E+10  508.083 2.6E+8     0.00          positive False   0.00 + p NSI Q3MS [150.152-1050.082]                                      0.0"},
            };
            expectedData.Add("calmix_Q3_10192022_03", file6Data);

            var file7Data = new Dictionary<int, string>
            {
                { 10, "3   274 0.08 0  50  500 1.8E+7  158.111 2.8E+6   157.50 cid      positive True    1.38 FTMS + c NSI Full ms3 176.10@cid25.00 157.50@hcd35.00 [50.00-5...    1.0"},
                { 11, "3   225 0.09 0  50  500 1.5E+7  158.111 2.3E+6   157.50 cid      positive True    1.55 FTMS + c NSI Full ms3 176.10@cid25.00 157.50@hcd35.00 [50.00-5...    1.0"},
                { 12, "3   227 0.10 0  50  500 1.7E+7  158.111 2.7E+6   157.50 cid      positive True    1.38 FTMS + c NSI Full ms3 176.10@cid25.00 157.50@hcd35.00 [50.00-5...    1.0"},
                { 13, "3   226 0.11 0  50  500 1.5E+7  158.111 2.3E+6   157.50 cid      positive True    1.53 FTMS + c NSI Full ms3 176.10@cid25.00 157.50@hcd35.00 [50.00-5...    1.0"},
                { 14, "3   192 0.12 0  50  500 1.3E+7  158.111 2.0E+6   157.50 cid      positive True    1.62 FTMS + c NSI Full ms3 176.10@cid25.00 157.50@hcd35.00 [50.00-5...    1.0"},
                { 15, "3   272 0.12 0  50  500 1.6E+7  158.111 2.3E+6   157.50 cid      positive True    1.62 FTMS + c NSI Full ms3 176.10@cid25.00 157.50@hcd35.00 [50.00-5...    1.0"},
                { 16, "3   206 0.13 0  50  500 1.4E+7  158.111 2.2E+6   157.50 cid      positive True    1.47 FTMS + c NSI Full ms3 176.10@cid25.00 157.50@hcd35.00 [50.00-5...    1.0"},
                { 17, "3   210 0.14 0  50  500 1.4E+7  158.111 2.4E+6   157.50 cid      positive True    1.53 FTMS + c NSI Full ms3 176.10@cid25.00 157.50@hcd35.00 [50.00-5...    1.0"},
                { 18, "3   258 0.15 0  50  500 1.6E+7  158.111 2.2E+6   157.50 cid      positive True    1.46 FTMS + c NSI Full ms3 176.10@cid25.00 157.50@hcd35.00 [50.00-5...    1.0"},
                { 19, "3   272 0.16 0  50  500 1.5E+7  158.111 2.2E+6   157.50 cid      positive True    1.78 FTMS + c NSI Full ms3 176.10@cid25.00 157.50@hcd35.00 [50.00-5...    1.0"},
                { 20, "3   257 0.17 0  50  500 1.7E+7  158.111 2.8E+6   157.50 cid      positive True    1.52 FTMS + c NSI Full ms3 176.10@cid25.00 157.50@hcd35.00 [50.00-5...    1.0"},
            };
            expectedData.Add("20181115_arginine_Gua13C_CIDcol25_158_HCDcol35", file7Data);

            var dataFile = GetRawDataFile(rawFileName, skipIfMissing);

            if (dataFile == null)
            {
                Console.WriteLine("Skipping unit tests for " + rawFileName);
                return;
            }

            using var reader = new MSDataFileReader(dataFile.FullName);

            var scanNumberToIndexMap = reader.GetScanToIndexMapping();

            Console.WriteLine("Scan info for {0}", dataFile.Name);
            Console.WriteLine("{0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11} {12} {13} {14} {15} {16}",
                "Scan", "MSLevel",
                "NumPeaks", "RetentionTime", "DriftTimeMsec",
                "LowMass", "HighMass", "TotalIonCurrent",
                "BasePeakMZ", "BasePeakIntensity",
                "ParentIonMZ", "ActivationType",
                "IonMode", "IsCentroided",
                "IonInjectionTime", "FilterText", "IsolationWindowWidth");

            var validateVsExpected = expectedMS1 + expectedMS2 > 0;

            var scanCountMS1 = 0;
            var scanCountMS2 = 0;

            foreach (var scan in scanNumberToIndexMap.Where(x => x.Key >= scanStart && x.Key <= scanEnd))
            {
                var scanNumber = scan.Key;
                var spectrumIndex = scan.Value;

                var spectrum = reader.GetSpectrum(spectrumIndex, getBinaryData: true);
                var spectrumParams = reader.GetSpectrumCVParamData(spectrumIndex);

                Assert.IsTrue(spectrum != null, "GetSpectrum returned a null object for scan {0}", scanNumber);

                var totalIonCurrent = CVParamUtilities.GetCvParamValueDbl(spectrumParams, CVParamUtilities.CVIDs.MS_TIC);
                var basePeakMZ = CVParamUtilities.GetCvParamValueDbl(spectrumParams, CVParamUtilities.CVIDs.MS_base_peak_m_z);
                var basePeakIntensity = CVParamUtilities.GetCvParamValueDbl(spectrumParams, CVParamUtilities.CVIDs.MS_base_peak_intensity);

                double parentIonMZ = 0;
                var activationType = string.Empty;

                if (spectrum.Precursors.Count > 0)
                {
                    var precursor = spectrum.Precursors[0];

                    parentIonMZ = precursor.PrecursorMz.GetValueOrDefault();

                    if (precursor.ActivationTypes != null)
                        activationType = string.Join(", ", precursor.ActivationTypes);
                }

                reader.GetScanMetadata(
                    spectrumIndex, out var scanStartTime, out var ionInjectionTime, out var filterText,
                    out var lowMass, out var highMass, out var isolationWindowWidth);

                var retentionTime = CVParamUtilities.CheckNull(spectrum.RetentionTime);
                Assert.AreEqual(retentionTime, scanStartTime, 0.0001, "Mismatch between spectrum.RetentionTime and CVParam MS_scan_start_time");

                var numPeaks = spectrum.Mzs.Length;
                var ionMode = spectrum.NegativeCharge ? "negative" : "positive";

                var scanSummary =
                    string.Format(
                        "{0} {1} {2,5} {3:0.00} {4:0} {5,3:0} {6,4:0} {7:0.0E+0} {8,8:0.000} {9:0.0E+0} {10,8:0.00} {11,-8} {12} {13,-5} {14,6:0.00} {15,-65} {16,6:0.0}",
                        scanNumber, spectrum.Level,
                        numPeaks, retentionTime,
                        CVParamUtilities.CheckNull(spectrum.IonMobility.Mobility),
                        lowMass, highMass,
                        totalIonCurrent,
                        basePeakMZ, basePeakIntensity, parentIonMZ,
                        activationType,
                        ionMode, spectrum.Centroided, ionInjectionTime,
                        filterText.Length > 65 ? filterText.Substring(0, 62) + "..." : filterText,
                        isolationWindowWidth);

                Console.WriteLine(scanSummary);

                if (spectrum.Level > 1)
                    scanCountMS2++;
                else
                    scanCountMS1++;

                var datasetName = Path.GetFileNameWithoutExtension(dataFile.Name);

                if (!expectedData.TryGetValue(datasetName, out var expectedDataThisFile))
                {
                    Assert.Fail("Dataset {0} not found in dictionary expectedData", datasetName);
                }

                if (expectedDataThisFile.TryGetValue(scanNumber, out var expectedScanSummary) && validateVsExpected)
                {
                    Assert.AreEqual(scanNumber + " " + expectedScanSummary, scanSummary,
                        "Scan summary mismatch, scan " + scanNumber);
                }

                var scanDescription = reader.GetScanDescription(spectrumIndex);
                Assert.IsTrue(string.IsNullOrWhiteSpace(scanDescription), "Scan description is typically null for Thermo .raw files");

                var expectedId = string.Format("0.1.{0}", scanNumber);
                var expectedNativeId = string.Format("controllerType=0 controllerNumber=1 scan={0}", scanNumber);

                Assert.AreEqual(spectrum.Id, expectedId, "Id is not in the expected format for scan {0}", scanNumber);

#pragma warning disable 618
                Assert.AreEqual(spectrum.NativeId, expectedNativeId, "NativeId is not in the expected format for scan {0}", scanNumber);
#pragma warning restore 618

                var nativeId = reader.GetSpectrumId(spectrumIndex);
                Assert.AreEqual(nativeId, expectedNativeId, "NativeId returned by GetSpectrumId() is not in the expected format for scan {0}", scanNumber);
            }

            Console.WriteLine("scanCountMS1={0}", scanCountMS1);
            Console.WriteLine("scanCountMS2={0}", scanCountMS2);

            if (validateVsExpected)
            {
                Assert.AreEqual(expectedMS1, scanCountMS1, "MS1 scan count mismatch");
                Assert.AreEqual(expectedMS2, scanCountMS2, "MS2 scan count mismatch");
            }
        }

        [Test]
        [TestCase("Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20.RAW", 1513, 1521)]
        [TestCase("HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53.raw", 16121, 16165)]
        [TestCase("Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20.RAW", 1513, 1521)]
        [TestCase("HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53.raw", 16121, 16165)]
        [TestCase("QC_mam_16_01_125ng_CPTACpt7-3s-a_02Nov17_Pippin_REP-17-10-01.raw", 65, 80)]
        [TestCase("Blank04_29Mar17_Smeagol.raw", 1390, 1402, 3)]                                    // SRM data
        [TestCase("calmix_Q3_10192022_03.raw", 5, 15)]                                              // MRM data (Q3MS)
        [TestCase("20181115_arginine_Gua13C_CIDcol25_158_HCDcol35.raw", 10, 20)]                    // MS3
        public void TestGetScanData(string rawFileName, int scanStart, int scanEnd, int expectedScanWindowCount = 1)
        {
            var expectedData = new Dictionary<string, Dictionary<int, Dictionary<string, string>>>();

            // Keys in this dictionary are the scan number of data being retrieved
            var file1Data = new Dictionary<int, Dictionary<string, string>>
            {
                {1513, new Dictionary<string, string>()},
                {1514, new Dictionary<string, string>()},
                {1515, new Dictionary<string, string>()},
                {1516, new Dictionary<string, string>()},
                {1517, new Dictionary<string, string>()}
            };

            // The KeySpec for each dictionary entry is Centroid
            file1Data[1513].Add("False", "851      851      409.615  4.8E+5   1227.956 1.6E+6   [400.0 - 2000.0]    + c ESI Full ms [400.00-2000.00]");
            file1Data[1514].Add("False", "109      109      281.601  2.4E+4   633.151  4.4E+4   [230.0 - 1780.0]    + c d Full ms2 884.41@cid45.00 [230.00-1780.00]");
            file1Data[1515].Add("False", "290      290      335.798  3.8E+4   1034.194 1.6E+4   [305.0 - 2000.0]    + c d Full ms2 1147.67@cid45.00 [305.00-2000.00]");
            file1Data[1516].Add("False", "154      154      461.889  7.3E+3   1203.274 2.6E+3   [400.0 - 2000.0]    + c d Full ms2 1492.90@cid45.00 [400.00-2000.00]");
            file1Data[1517].Add("False", "887      887      420.016  9.7E+5   1232.206 8.0E+5   [400.0 - 2000.0]    + c ESI Full ms [400.00-2000.00]");

            // Values are the same whether or not the data is centroided, so duplicate the "False" values
            foreach (var scan in file1Data.Keys.ToList())
            {
                file1Data[scan].Add("True", file1Data[scan]["False"]);
            }

            expectedData.Add("Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20", file1Data);

            var file2Data = new Dictionary<int, Dictionary<string, string>>
            {
                {16121, new Dictionary<string, string>()},
                {16122, new Dictionary<string, string>()},
                {16126, new Dictionary<string, string>()},
                {16131, new Dictionary<string, string>()},
                {16133, new Dictionary<string, string>()},
                {16141, new Dictionary<string, string>()}
            };

            // The KeySpec for each dictionary entry is Centroid

            file2Data[16121].Add("False", "11888    11888    346.518  0.0E+0   706.844  9.8E+4   [350.0 - 1550.0]    FTMS + p NSI Full ms [350.0000-1550.0000]");
            file2Data[16122].Add("False", "490      490      116.232  7.0E+1   403.932  1.1E+3   [106.0 - 817.0]     ITMS + c NSI r d Full ms2 403.2206@cid30.00 [106.0000-817.0000]");
            file2Data[16126].Add("False", "753      753      231.045  1.1E+1   1004.586 2.0E+1   [120.0 - 1627.0]    ITMS + c NSI r d sa Full ms2 538.8400@etd53.58@cid20.00 [120.0000-1627.0000]");
            file2Data[16131].Add("False", "29       29       984.504  9.5E+3   1931.917 2.4E+1   [120.0 - 1986.0]    ITMS + c NSI r d Full ms2 987.8934@etd120.55 [120.0000-1986.0000]");
            file2Data[16133].Add("False", "280      280      260.118  2.3E+1   663.160  7.7E+0   [120.0 - 853.0]     ITMS + c NSI r d sa Full ms2 421.2619@etd120.55@cid20.00 [120.0000-853.0000]");
            file2Data[16141].Add("False", "240      240      304.425  1.3E+1   1447.649 3.0E+1   [120.0 - 1760.0]    ITMS + c NSI r d sa Full ms2 874.8397@etd120.55@hcd20.00 [120.0000-1760.0000]");

            file2Data[16121].Add("True", "844      844      351.231  2.9E+5   712.813  2.9E+5   [350.0 - 1550.0]    FTMS + p NSI Full ms [350.0000-1550.0000]");
            file2Data[16122].Add("True", "490      490      116.232  7.0E+1   403.932  1.1E+3   [106.0 - 817.0]     ITMS + c NSI r d Full ms2 403.2206@cid30.00 [106.0000-817.0000]");
            file2Data[16126].Add("True", "753      753      231.045  1.1E+1   1004.586 2.0E+1   [120.0 - 1627.0]    ITMS + c NSI r d sa Full ms2 538.8400@etd53.58@cid20.00 [120.0000-1627.0000]");
            file2Data[16131].Add("True", "29       29       984.504  9.5E+3   1931.917 2.4E+1   [120.0 - 1986.0]    ITMS + c NSI r d Full ms2 987.8934@etd120.55 [120.0000-1986.0000]");
            file2Data[16133].Add("True", "280      280      260.118  2.3E+1   663.160  7.7E+0   [120.0 - 853.0]     ITMS + c NSI r d sa Full ms2 421.2619@etd120.55@cid20.00 [120.0000-853.0000]");
            file2Data[16141].Add("True", "240      240      304.425  1.3E+1   1447.649 3.0E+1   [120.0 - 1760.0]    ITMS + c NSI r d sa Full ms2 874.8397@etd120.55@hcd20.00 [120.0000-1760.0000]");

            expectedData.Add("HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53", file2Data);

            var file3Data = new Dictionary<int, Dictionary<string, string>>
            {
                {65, new Dictionary<string, string>()},
                {66, new Dictionary<string, string>()},
                {67, new Dictionary<string, string>()},
                {68, new Dictionary<string, string>()},
                {69, new Dictionary<string, string>()},
                {70, new Dictionary<string, string>()},
                {71, new Dictionary<string, string>()},
                {72, new Dictionary<string, string>()},
                {73, new Dictionary<string, string>()},
                {74, new Dictionary<string, string>()},
                {75, new Dictionary<string, string>()},
                {76, new Dictionary<string, string>()},
                {77, new Dictionary<string, string>()},
                {78, new Dictionary<string, string>()},
                {79, new Dictionary<string, string>()},
                {80, new Dictionary<string, string>()}
            };

            file3Data[65].Add("False", "10413    10413    346.520  0.0E+0   958.772  0.0E+0   [350.0 - 1800.0]    FTMS + p NSI Full ms [350.0000-1800.0000]");
            file3Data[66].Add("False", "28       28       111.163  4.7E+2   178.304  7.2E+2   [110.0 - 2000.0]    FTMS + c NSI d Full ms2 1704.2509@hcd30.00 [110.0000-2000.0000]");
            file3Data[67].Add("False", "34       34       113.093  4.7E+2   178.282  9.4E+3   [110.0 - 2000.0]    FTMS + c NSI d Full ms2 1714.6105@hcd30.00 [110.0000-2000.0000]");
            file3Data[68].Add("False", "75       75       114.498  4.4E+2   148.256  1.3E+3   [110.0 - 2000.0]    FTMS + c NSI d Full ms2 1327.0189@hcd30.00 [110.0000-2000.0000]");
            file3Data[69].Add("False", "11589    11589    346.520  0.0E+0   1016.351 0.0E+0   [350.0 - 1800.0]    FTMS + p NSI Full ms [350.0000-1800.0000]");
            file3Data[70].Add("False", "27       27       112.903  5.0E+2   188.835  7.6E+2   [110.0 - 2000.0]    FTMS + c NSI d Full ms2 1628.6455@hcd30.00 [110.0000-2000.0000]");
            file3Data[71].Add("False", "31       31       114.489  4.3E+2   202.329  6.3E+2   [110.0 - 2000.0]    FTMS + c NSI d Full ms2 1579.6287@hcd30.00 [110.0000-2000.0000]");
            file3Data[72].Add("False", "38       38       115.591  5.3E+2   178.282  8.2E+3   [110.0 - 2000.0]    FTMS + c NSI d Full ms2 1701.8641@hcd30.00 [110.0000-2000.0000]");
            file3Data[73].Add("False", "30       30       110.101  4.4E+2   178.283  4.1E+3   [110.0 - 2000.0]    FTMS + c NSI d Full ms2 1311.6626@hcd30.00 [110.0000-2000.0000]");
            file3Data[74].Add("False", "76       76       117.937  5.4E+2   148.239  1.4E+3   [110.0 - 2000.0]    FTMS + c NSI d Full ms2 1319.7051@hcd30.00 [110.0000-2000.0000]");
            file3Data[75].Add("False", "12606    12606    346.520  0.0E+0   822.732  0.0E+0   [350.0 - 1800.0]    FTMS + p NSI Full ms [350.0000-1800.0000]");
            file3Data[76].Add("False", "35       35       113.376  4.9E+2   290.110  7.0E+2   [110.0 - 2000.0]    FTMS + c NSI d Full ms2 1694.5530@hcd30.00 [110.0000-2000.0000]");
            file3Data[77].Add("False", "28       28       110.060  4.5E+2   254.098  6.9E+2   [110.0 - 2000.0]    FTMS + c NSI d Full ms2 1556.5758@hcd30.00 [110.0000-2000.0000]");
            file3Data[78].Add("False", "80       80       110.961  4.6E+2   148.257  1.2E+3   [110.0 - 2000.0]    FTMS + c NSI d Full ms2 1478.0348@hcd30.00 [110.0000-2000.0000]");
            file3Data[79].Add("False", "14314    14314    346.520  0.0E+0   805.777  0.0E+0   [350.0 - 1800.0]    FTMS + p NSI Full ms [350.0000-1800.0000]");
            file3Data[80].Add("False", "36       36       113.223  5.8E+2   178.279  1.5E+4   [110.0 - 2000.0]    FTMS + c NSI d Full ms2 1732.2192@hcd30.00 [110.0000-2000.0000]");

            file3Data[65].Add("True", "749      749      352.239  6.9E+3   1022.095 1.3E+4   [350.0 - 1800.0]    FTMS + p NSI Full ms [350.0000-1800.0000]");
            file3Data[66].Add("True", "28       28       111.163  4.7E+2   178.304  7.2E+2   [110.0 - 2000.0]    FTMS + c NSI d Full ms2 1704.2509@hcd30.00 [110.0000-2000.0000]");
            file3Data[67].Add("True", "34       34       113.093  4.7E+2   178.282  9.4E+3   [110.0 - 2000.0]    FTMS + c NSI d Full ms2 1714.6105@hcd30.00 [110.0000-2000.0000]");
            file3Data[68].Add("True", "75       75       114.498  4.4E+2   148.256  1.3E+3   [110.0 - 2000.0]    FTMS + c NSI d Full ms2 1327.0189@hcd30.00 [110.0000-2000.0000]");
            file3Data[69].Add("True", "845      845      353.908  1.1E+4   1093.054 1.4E+4   [350.0 - 1800.0]    FTMS + p NSI Full ms [350.0000-1800.0000]");
            file3Data[70].Add("True", "27       27       112.903  5.0E+2   188.835  7.6E+2   [110.0 - 2000.0]    FTMS + c NSI d Full ms2 1628.6455@hcd30.00 [110.0000-2000.0000]");
            file3Data[71].Add("True", "31       31       114.489  4.3E+2   202.329  6.3E+2   [110.0 - 2000.0]    FTMS + c NSI d Full ms2 1579.6287@hcd30.00 [110.0000-2000.0000]");
            file3Data[72].Add("True", "38       38       115.591  5.3E+2   178.282  8.2E+3   [110.0 - 2000.0]    FTMS + c NSI d Full ms2 1701.8641@hcd30.00 [110.0000-2000.0000]");
            file3Data[73].Add("True", "30       30       110.101  4.4E+2   178.283  4.1E+3   [110.0 - 2000.0]    FTMS + c NSI d Full ms2 1311.6626@hcd30.00 [110.0000-2000.0000]");
            file3Data[74].Add("True", "76       76       117.937  5.4E+2   148.239  1.4E+3   [110.0 - 2000.0]    FTMS + c NSI d Full ms2 1319.7051@hcd30.00 [110.0000-2000.0000]");
            file3Data[75].Add("True", "913      913      353.909  2.4E+4   875.248  1.3E+4   [350.0 - 1800.0]    FTMS + p NSI Full ms [350.0000-1800.0000]");
            file3Data[76].Add("True", "35       35       113.376  4.9E+2   290.110  7.0E+2   [110.0 - 2000.0]    FTMS + c NSI d Full ms2 1694.5530@hcd30.00 [110.0000-2000.0000]");
            file3Data[77].Add("True", "28       28       110.060  4.5E+2   254.098  6.9E+2   [110.0 - 2000.0]    FTMS + c NSI d Full ms2 1556.5758@hcd30.00 [110.0000-2000.0000]");
            file3Data[78].Add("True", "80       80       110.961  4.6E+2   148.257  1.2E+3   [110.0 - 2000.0]    FTMS + c NSI d Full ms2 1478.0348@hcd30.00 [110.0000-2000.0000]");
            file3Data[79].Add("True", "1035     1035     353.909  1.9E+4   828.673  5.3E+4   [350.0 - 1800.0]    FTMS + p NSI Full ms [350.0000-1800.0000]");
            file3Data[80].Add("True", "36       36       113.223  5.8E+2   178.279  1.5E+4   [110.0 - 2000.0]    FTMS + c NSI d Full ms2 1732.2192@hcd30.00 [110.0000-2000.0000]");

            expectedData.Add("QC_mam_16_01_125ng_CPTACpt7-3s-a_02Nov17_Pippin_REP-17-10-01", file3Data);

            var file4Data = new Dictionary<int, Dictionary<string, string>>
            {
                {1390, new Dictionary<string, string>()},
                {1391, new Dictionary<string, string>()},
                {1392, new Dictionary<string, string>()},
                {1393, new Dictionary<string, string>()},
                {1394, new Dictionary<string, string>()},
                {1395, new Dictionary<string, string>()},
                {1396, new Dictionary<string, string>()},
                {1397, new Dictionary<string, string>()},
                {1398, new Dictionary<string, string>()},
                {1399, new Dictionary<string, string>()},
                {1400, new Dictionary<string, string>()},
                {1401, new Dictionary<string, string>()},
                {1402, new Dictionary<string, string>()}
            };

            file4Data[1390].Add("False", "3        3        907.433  1.0E+0   1022.460 1.0E+0   [907.432 - 907.434, 1022.459 - 1022.461, 1151.5021 - 1151.504] + c NSI SRM ms2 833.374 [907.432-907.434, 1022.459-1022.461, 1151.502-1151.504]");
            file4Data[1391].Add("False", "3        3        637.261  1.0E+0   750.345  3.4E+0   [637.26 - 637.262, 750.344 - 750.3459, 938.424 - 938.426] + c NSI SRM ms2 526.258 [637.260-637.262, 750.344-750.346, 938.424-938.426]");
            file4Data[1392].Add("False", "3        3        645.275  2.4E+0   758.359  1.1E+0   [645.274 - 645.276, 758.358 - 758.36, 946.438 - 946.44] + c NSI SRM ms2 530.265 [645.274-645.276, 758.358-758.360, 946.438-946.440]");
            file4Data[1393].Add("False", "3        3        602.326  2.0E+2   715.410  8.5E+1   [602.325 - 602.327, 715.409 - 715.4109, 988.505 - 988.507] + c NSI SRM ms2 647.325 [602.325-602.327, 715.409-715.411, 988.505-988.507]");
            file4Data[1394].Add("False", "3        3        610.340  2.3E+2   723.424  1.3E+2   [610.3391 - 610.341, 723.423 - 723.425, 996.519 - 996.521] + c NSI SRM ms2 651.332 [610.339-610.341, 723.423-723.425, 996.519-996.521]");
            file4Data[1395].Add("False", "3        3        897.425  1.2E+0   1012.452 1.2E+0   [897.424 - 897.426, 1012.451 - 1012.453, 1141.493 - 1141.495] + c NSI SRM ms2 828.370 [897.424-897.426, 1012.451-1012.453, 1141.493-1141.495]");
            file4Data[1396].Add("False", "3        3        907.433  1.2E+0   1022.460 1.2E+0   [907.432 - 907.434, 1022.459 - 1022.461, 1151.5021 - 1151.504] + c NSI SRM ms2 833.374 [907.432-907.434, 1022.459-1022.461, 1151.502-1151.504]");
            file4Data[1397].Add("False", "3        3        637.261  1.2E+0   750.345  1.2E+0   [637.26 - 637.262, 750.344 - 750.3459, 938.424 - 938.426] + c NSI SRM ms2 526.258 [637.260-637.262, 750.344-750.346, 938.424-938.426]");
            file4Data[1398].Add("False", "3        3        645.275  1.2E+0   758.359  6.8E+0   [645.274 - 645.276, 758.358 - 758.36, 946.438 - 946.44] + c NSI SRM ms2 530.265 [645.274-645.276, 758.358-758.360, 946.438-946.440]");
            file4Data[1399].Add("False", "3        3        602.326  2.2E+2   715.410  2.9E+2   [602.325 - 602.327, 715.409 - 715.4109, 988.505 - 988.507] + c NSI SRM ms2 647.325 [602.325-602.327, 715.409-715.411, 988.505-988.507]");
            file4Data[1400].Add("False", "3        3        610.340  2.4E+2   723.424  2.8E+2   [610.3391 - 610.341, 723.423 - 723.425, 996.519 - 996.521] + c NSI SRM ms2 651.332 [610.339-610.341, 723.423-723.425, 996.519-996.521]");
            file4Data[1401].Add("False", "3        3        897.425  1.1E+0   1012.452 2.6E+0   [897.424 - 897.426, 1012.451 - 1012.453, 1141.493 - 1141.495] + c NSI SRM ms2 828.370 [897.424-897.426, 1012.451-1012.453, 1141.493-1141.495]");
            file4Data[1402].Add("False", "3        3        907.433  1.1E+0   1022.460 1.1E+0   [907.432 - 907.434, 1022.459 - 1022.461, 1151.5021 - 1151.504] + c NSI SRM ms2 833.374 [907.432-907.434, 1022.459-1022.461, 1151.502-1151.504]");

            // Values are the same whether or not the data is centroided, so duplicate the "False" values
            foreach (var scan in file4Data.Keys.ToList())
            {
                file4Data[scan].Add("True", file4Data[scan]["False"]);
            }

            expectedData.Add("Blank04_29Mar17_Smeagol", file4Data);

            var file5Data = new Dictionary<int, Dictionary<string, string>>
            {
                {5, new Dictionary<string, string>()},
                {6, new Dictionary<string, string>()},
                {7, new Dictionary<string, string>()},
                {8, new Dictionary<string, string>()},
                {9, new Dictionary<string, string>()},
                {10, new Dictionary<string, string>()},
                {11, new Dictionary<string, string>()},
                {12, new Dictionary<string, string>()},
                {13, new Dictionary<string, string>()},
                {14, new Dictionary<string, string>()},
                {15, new Dictionary<string, string>()}
            };

            file5Data[5].Add("False", "12857    12857    150.152  1.6E+5   599.951  1.1E+5   [150.082 - 1050.082] + p NSI Q3MS [150.152-1050.082]");
            file5Data[6].Add("False", "12857    12857    150.152  1.2E+5   599.951  1.6E+5   [150.082 - 1050.082] + p NSI Q3MS [150.152-1050.082]");
            file5Data[7].Add("False", "12857    12857    150.152  1.6E+5   599.951  1.0E+5   [150.082 - 1050.082] + p NSI Q3MS [150.152-1050.082]");
            file5Data[8].Add("False", "12857    12857    150.152  3.2E+5   599.951  1.1E+5   [150.082 - 1050.082] + p NSI Q3MS [150.152-1050.082]");
            file5Data[9].Add("False", "12857    12857    150.152  2.7E+5   599.951  5.1E+4   [150.082 - 1050.082] + p NSI Q3MS [150.152-1050.082]");
            file5Data[10].Add("False", "12857    12857    150.152  1.7E+5   599.951  1.3E+5   [150.082 - 1050.082] + p NSI Q3MS [150.152-1050.082]");
            file5Data[11].Add("False", "12857    12857    150.152  1.1E+5   599.951  2.0E+5   [150.082 - 1050.082] + p NSI Q3MS [150.152-1050.082]");
            file5Data[12].Add("False", "12857    12857    150.152  2.7E+5   599.951  3.4E+3   [150.082 - 1050.082] + p NSI Q3MS [150.152-1050.082]");
            file5Data[13].Add("False", "12857    12857    150.152  2.5E+5   599.951  6.0E+4   [150.082 - 1050.082] + p NSI Q3MS [150.152-1050.082]");
            file5Data[14].Add("False", "12857    12857    150.152  1.8E+5   599.951  2.5E+5   [150.082 - 1050.082] + p NSI Q3MS [150.152-1050.082]");
            file5Data[15].Add("False", "12857    12857    150.152  3.4E+5   599.951  9.6E+4   [150.082 - 1050.082] + p NSI Q3MS [150.152-1050.082]");

            file5Data[5].Add("True", "1122     1122     150.470  4.3E+6   595.959  1.8E+6   [150.082 - 1050.082] + p NSI Q3MS [150.152-1050.082]");
            file5Data[6].Add("True", "1128     1128     150.439  3.3E+6   591.324  5.2E+5   [150.082 - 1050.082] + p NSI Q3MS [150.152-1050.082]");
            file5Data[7].Add("True", "1122     1122     150.543  4.6E+6   596.642  1.8E+6   [150.082 - 1050.082] + p NSI Q3MS [150.152-1050.082]");
            file5Data[8].Add("True", "1121     1121     150.382  3.5E+6   593.464  6.5E+5   [150.082 - 1050.082] + p NSI Q3MS [150.152-1050.082]");
            file5Data[9].Add("True", "1128     1128     150.389  2.9E+6   595.982  1.7E+6   [150.082 - 1050.082] + p NSI Q3MS [150.152-1050.082]");
            file5Data[10].Add("True", "1136     1136     150.506  3.6E+6   596.356  2.0E+6   [150.082 - 1050.082] + p NSI Q3MS [150.152-1050.082]");
            file5Data[11].Add("True", "1124     1124     150.419  3.9E+6   590.106  1.4E+6   [150.082 - 1050.082] + p NSI Q3MS [150.152-1050.082]");
            file5Data[12].Add("True", "1133     1133     150.566  5.8E+6   594.073  1.3E+6   [150.082 - 1050.082] + p NSI Q3MS [150.152-1050.082]");
            file5Data[13].Add("True", "1111     1111     150.416  3.7E+6   590.092  1.7E+6   [150.082 - 1050.082] + p NSI Q3MS [150.152-1050.082]");
            file5Data[14].Add("True", "1139     1139     150.480  4.4E+6   594.775  1.8E+6   [150.082 - 1050.082] + p NSI Q3MS [150.152-1050.082]");
            file5Data[15].Add("True", "1126     1126     150.427  3.6E+6   595.926  2.1E+6   [150.082 - 1050.082] + p NSI Q3MS [150.152-1050.082]");

            expectedData.Add("calmix_Q3_10192022_03", file5Data);

            var file6Data = new Dictionary<int, Dictionary<string, string>>
            {
                {10, new Dictionary<string, string>()},
                {11, new Dictionary<string, string>()},
                {12, new Dictionary<string, string>()},
                {13, new Dictionary<string, string>()},
                {14, new Dictionary<string, string>()},
                {15, new Dictionary<string, string>()},
                {16, new Dictionary<string, string>()},
                {17, new Dictionary<string, string>()},
                {18, new Dictionary<string, string>()},
                {19, new Dictionary<string, string>()},
                {20, new Dictionary<string, string>()}
            };

            file6Data[10].Add("False", "274      274      50.387   3.9E+4   103.980  4.0E+4   [50.0 - 500.0]      FTMS + c NSI Full ms3 176.10@cid25.00 157.50@hcd35.00 [50.00-500.00]");
            file6Data[11].Add("False", "225      225      50.018   3.2E+4   116.300  3.5E+4   [50.0 - 500.0]      FTMS + c NSI Full ms3 176.10@cid25.00 157.50@hcd35.00 [50.00-500.00]");
            file6Data[12].Add("False", "227      227      50.000   3.8E+4   133.961  3.9E+4   [50.0 - 500.0]      FTMS + c NSI Full ms3 176.10@cid25.00 157.50@hcd35.00 [50.00-500.00]");
            file6Data[13].Add("False", "226      226      50.122   3.9E+4   117.858  4.1E+4   [50.0 - 500.0]      FTMS + c NSI Full ms3 176.10@cid25.00 157.50@hcd35.00 [50.00-500.00]");
            file6Data[14].Add("False", "192      192      50.239   3.5E+4   132.193  3.7E+4   [50.0 - 500.0]      FTMS + c NSI Full ms3 176.10@cid25.00 157.50@hcd35.00 [50.00-500.00]");
            file6Data[15].Add("False", "272      272      50.186   3.3E+4   114.787  3.5E+4   [50.0 - 500.0]      FTMS + c NSI Full ms3 176.10@cid25.00 157.50@hcd35.00 [50.00-500.00]");
            file6Data[16].Add("False", "206      206      50.058   3.6E+4   127.758  4.0E+4   [50.0 - 500.0]      FTMS + c NSI Full ms3 176.10@cid25.00 157.50@hcd35.00 [50.00-500.00]");
            file6Data[17].Add("False", "210      210      50.225   3.5E+4   123.739  3.8E+4   [50.0 - 500.0]      FTMS + c NSI Full ms3 176.10@cid25.00 157.50@hcd35.00 [50.00-500.00]");
            file6Data[18].Add("False", "258      258      50.148   3.6E+4   132.958  1.0E+5   [50.0 - 500.0]      FTMS + c NSI Full ms3 176.10@cid25.00 157.50@hcd35.00 [50.00-500.00]");
            file6Data[19].Add("False", "272      272      50.387   3.1E+4   116.912  4.1E+4   [50.0 - 500.0]      FTMS + c NSI Full ms3 176.10@cid25.00 157.50@hcd35.00 [50.00-500.00]");
            file6Data[20].Add("False", "257      257      51.003   4.3E+4   136.238  4.4E+4   [50.0 - 500.0]      FTMS + c NSI Full ms3 176.10@cid25.00 157.50@hcd35.00 [50.00-500.00]");

            // Values are the same whether or not the data is centroided, so duplicate the "False" values
            foreach (var scan in file6Data.Keys.ToList())
            {
                file6Data[scan].Add("True", file6Data[scan]["False"]);
            }

            expectedData.Add("20181115_arginine_Gua13C_CIDcol25_158_HCDcol35", file6Data);

            var dataFile = GetRawDataFile(rawFileName);

            var scanInfoExtractor = new Regex(@"^\d+ +(True|False) +(?<Metadata>.+)", RegexOptions.Compiled);

            for (var iteration = 1; iteration <= 2; iteration++)
            {
                var centroidData = (iteration > 1);

                using var reader = new MSDataFileReader(
                    dataFile.FullName,
                    requireVendorCentroidedMS1: centroidData,
                    requireVendorCentroidedMS2: centroidData);

                if (iteration == 1)
                {
                    Console.WriteLine("Scan data for {0}", dataFile.Name);
                    Console.WriteLine("{0} {1,8} {2,-8} {3,-8} {4,-8} {5,-8} {6,-8} {7,-8} {8,-19} {9}",
                        "Scan", "Centroid", "MzCount", "IntCount",
                        "FirstMz", "FirstInt", "MidMz", "MidInt", "MzRange", "ScanFilter");
                }

                var scanNumberToIndexMap = reader.GetScanToIndexMapping();

                foreach (var scan in scanNumberToIndexMap.Where(x => x.Key >= scanStart && x.Key <= scanEnd))
                {
                    var scanNumber = scan.Key;
                    var spectrumIndex = scan.Value;

                    var spectrum = reader.GetSpectrum(spectrumIndex, getBinaryData: true);

                    var cvScanInfo = reader.GetSpectrumScanInfo(spectrumIndex);

                    var dataPointsRead = spectrum.Mzs.Length;

                    Assert.IsTrue(dataPointsRead > 0, "GetScanData returned 0 for scan {0}", scanNumber);

                    Assert.IsTrue(cvScanInfo.Scans.Count > 0,
                        "The cvScanInfo instance obtained using GetSpectrumScanInfo has an empty scan list for scan {0}", scanNumber);

                    Assert.IsTrue(cvScanInfo.Scans[0].ScanWindowList.Count > 0,
                        "The cvScanInfo instance obtained using GetSpectrumScanInfo has an empty scan window list for scan {0}", scanNumber);

                    var expectedItemCount = expectedScanWindowCount * 2;

                    Assert.IsTrue(cvScanInfo.Scans[0].ScanWindowList.Count == expectedItemCount,
                        "The cvScanInfo instance obtained using GetSpectrumScanInfo has a scan window list with {0} items for scan {1}; expecting there to be {2} items",
                        cvScanInfo.Scans[0].ScanWindowList.Count, scanNumber, expectedItemCount);

                    var lowerMzName = cvScanInfo.Scans[0].ScanWindowList[0].CVName;
                    var upperMzName = cvScanInfo.Scans[0].ScanWindowList[1].CVName;

                    Assert.AreEqual("MS_scan_window_lower_limit", lowerMzName,
                        "CVName for the lower scan window limit was {0}; expecting {1}",
                        lowerMzName, "MS_scan_window_lower_limit");

                    Assert.AreEqual("MS_scan_window_upper_limit", upperMzName,
                        "CVName for the upper scan window limit was {0}; expecting {1}",
                        upperMzName, "MS_scan_window_upper_limit");

                    var scanWindowDescription = new StringBuilder();
                    scanWindowDescription.Append("[");

                    for (var i = 0; i < cvScanInfo.Scans[0].ScanWindowList.Count; i += 2)
                    {
                        if (i > 0)
                            scanWindowDescription.Append(", ");

                        var lowerMz = cvScanInfo.Scans[0].ScanWindowList[i].Value;
                        var upperMz = cvScanInfo.Scans[0].ScanWindowList[i + 1].Value;

                        if (double.TryParse(lowerMz, out var lowerMzValue) && double.TryParse(upperMz, out var upperMzValue))
                        {
                            scanWindowDescription.AppendFormat("{0:0.0###} - {1:0.0###}", lowerMzValue, upperMzValue);
                            continue;
                        }

                        scanWindowDescription.AppendFormat("{0} - {1}", lowerMz, upperMz);
                    }

                    scanWindowDescription.Append("]");

                    var midPoint = (int)(spectrum.Intensities.Length / 2f);

                    var filterText = reader.GetScanFilterText(spectrumIndex);

                    var scanSummary =
                        string.Format(
                            "{0} {1,8} {2,-8} {3,-8} {4,-8:0.000} {5,-8:0.0E+0} {6,-8:0.000} {7,-8:0.0E+0} {8,-19} {9}",
                            scanNumber, centroidData,
                            spectrum.Mzs.Length, spectrum.Intensities.Length,
                            spectrum.Mzs[0], spectrum.Intensities[0],
                            spectrum.Mzs[midPoint], spectrum.Intensities[midPoint],
                            scanWindowDescription,
                            filterText);

                    Console.WriteLine(scanSummary);

                    var datasetName = Path.GetFileNameWithoutExtension(dataFile.Name);

                    if (!expectedData.TryGetValue(datasetName, out var expectedDataThisFile))
                    {
                        Assert.Fail("Dataset {0} not found in dictionary expectedData", datasetName);
                    }

                    if (expectedDataThisFile.TryGetValue(scanNumber, out var expectedDataByType))
                    {
                        var keySpec = centroidData.ToString();

                        if (!expectedDataByType.TryGetValue(keySpec, out var expectedDataDetails))
                            continue;

                        // Extract the text after True or False in scanSummary

                        var match = scanInfoExtractor.Match(scanSummary);

                        if (!match.Success)
                            Assert.Fail("Regex match failed for {0}", scanSummary);

                        var actualDataDetails = match.Groups["Metadata"].Value;

                        Assert.AreEqual(expectedDataDetails, actualDataDetails,
                            "Scan details mismatch, scan " + scanNumber + ", keySpec " + keySpec);
                    }
                }
            }
        }

        [Test]
        [TestCase("Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20.RAW", 862, 2454)]
        [TestCase("Angiotensin_AllScans.raw", 87, 1688)]
        public void TestGetScanTimesAndMsLevel(string rawFileName, int expectedMS1, int expectedMS2)
        {
            var dataFile = GetRawDataFile(rawFileName);

            using var reader = new MSDataFileReader(dataFile.FullName);

            Console.WriteLine("Examining data in " + dataFile.Name);

            reader.GetScanTimesAndMsLevels(out var scanTimes, out var msLevels);

            var nextPreviewIndex = 0;
            var ms2Shown = false;

            var actualCountsByLevel = new Dictionary<byte, int>();

            for (var i = 0; i < scanTimes.Length; i++)
            {
                var msLevel = msLevels[i];

                if (actualCountsByLevel.TryGetValue(msLevel, out var scanCount))
                {
                    actualCountsByLevel[msLevel] = scanCount + 1;
                }
                else
                {
                    actualCountsByLevel.Add(msLevel, 1);
                }

                if (i != nextPreviewIndex && ms2Shown)
                {
                    continue;
                }

                Console.WriteLine("Scan {0,-4} at {1,-8:F2} minutes, MSLevel {2}", i + 1, scanTimes[i], msLevel);

                if (msLevel == 2)
                    ms2Shown = true;

                if (i != nextPreviewIndex)
                {
                    continue;
                }

                ms2Shown = msLevel > 1;

                if (nextPreviewIndex < 10)
                    nextPreviewIndex++;
                else
                    nextPreviewIndex *= 2;
            }

            Console.WriteLine();

            foreach (var item in actualCountsByLevel)
            {
                Console.WriteLine("MS Level {0} has {1} spectra", item.Key, item.Value);
            }

            Assert.AreEqual(expectedMS1, actualCountsByLevel[1], "Mismatch in scan count for MS1 spectra");
            Assert.AreEqual(expectedMS2, actualCountsByLevel[2], "Mismatch in scan count for MS2 spectra");
        }

        private void AddExpectedTupleAndCount(
            IDictionary<string, Dictionary<Tuple<string, string>, int>> expectedData,
            string fileName,
            string tupleKey1,
            string tupleKey2,
            int scanCount)
        {
            if (!expectedData.TryGetValue(fileName, out var expectedScanInfo))
            {
                expectedScanInfo = new Dictionary<Tuple<string, string>, int>();
                expectedData.Add(fileName, expectedScanInfo);
            }

            expectedScanInfo.Add(new Tuple<string, string>(tupleKey1, tupleKey2), scanCount);
        }

        /// <summary>
        /// Get a FileInfo object for the given .raw file
        /// </summary>
        /// <param name="rawFileName">Thermo raw file name</param>
        /// <param name="skipIfMissing">If true, return null if the file cannot be found</param>
        private static FileInfo GetRawDataFile(string rawFileName, bool skipIfMissing = false)
        {
            const string REMOTE_PATH = @"\\proto-2\UnitTest_Files\ThermoRawFileReader";

            if (InstrumentDataUtilities.FindInstrumentData(rawFileName, false, REMOTE_PATH, out var instrumentDataFile) &&
                instrumentDataFile is FileInfo rawFile)
            {
                return rawFile;
            }

            if (skipIfMissing)
            {
                // FindInstrumentData should have already shown a warning message
                return null;
            }

            Assert.Fail("File not found: " + rawFileName);
            return null;
        }
    }
}
