using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using pwiz.ProteowizardWrapper;
using ThermoRawFileReader;

// ReSharper disable StringLiteralTypo
namespace ProteowizardWrapperUnitTests
{
    [TestFixture]
    public class ThermoScanDataTests
    {

        [Test]
        [TestCase("Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20.RAW")]
        [TestCase("HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53.raw")]
        [TestCase("HCC-38_ETciD_EThcD_07Jan16_Pippin_15-08-53.raw")]
        [TestCase("MZ0210MnxEF889ETD.raw")]
        public void TestGetCollisionEnergy(string rawFileName)
        {
            // Keys in this Dictionary are filename, values are Collision Energies by scan
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
                {39007, ce120},                 // Actually has two collision energies (120.55 and 20.00) but Proteowizard only reports 120.55
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

            if (!expectedData.TryGetValue(Path.GetFileNameWithoutExtension(dataFile.Name), out var collisionEnergiesThisFile))
            {
                Assert.Fail("Dataset {0} not found in dictionary expectedData", dataFile.Name);
            }

            // Keys are scan number, values are the list of collision energies
            var collisionEnergiesActual = new Dictionary<int, List<double>>();

            // Keys are scan number, values are msLevel
            var msLevelsActual = new Dictionary<int, int>();

            // Keys are scan number, values are the ActivationType (or list of activation types), for example cid, etd, hcd
            var activationTypesActual = new Dictionary<int, List<string>>();

            using (var reader = new MSDataFileReader(dataFile.FullName))
            {

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
                                // Proteowizard has a bug where the collision energy is not reported correctly for etd spectra
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
        }

        [Test]
        [TestCase("blank_MeOH-3_18May16_Rainier_Thermo_10344958.raw", 1500, 1900, 190, 211, 0, 0)]
        [TestCase("Corrupt_Qc_Shew_13_04_pt1_a_5Sep13_Cougar_13-06-14.raw", 500, 600, 0, 0, 500, 600)]
        [TestCase("Corrupt_QC_Shew_07_03_pt25_e_6Apr08_Falcon_Fst-75-1.raw", 500, 600, 0, 0, 500, 600)]
        // This file causes .NET to become unstable and aborts the unit tests
        // [TestCase("Corrupt_Scans6920-7021_AID_STM_013_101104_06_LTQ_16Nov04_Earth_0904-8.raw", 6900, 7050, 10, 40, 6920, 7021)]
        public void TestCorruptDataHandling(
            string rawFileName,
            int scanStart,
            int scanEnd,
            int expectedMS1,
            int expectedMS2,
            int corruptScanStart,
            int corruptScanEnd)
        {
            var dataFile = GetRawDataFile(rawFileName);

            try
            {
                using (var reader = new MSDataFileReader(dataFile.FullName))
                {

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
                            var spectrum = reader.GetSpectrum(spectrumIndex, true);

                            var cvScanInfo = reader.GetSpectrumScanInfo(spectrumIndex);

                            Assert.IsTrue(cvScanInfo != null, "GetSpectrumScanInfo returned a null object for scan {0}", scanNumber);

                            GetScanFilterText(cvScanInfo, out var filterText);

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
        [TestCase("Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20.RAW", 3316)]
        [TestCase("HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53.raw", 71147)]
        public void TestGetNumScans(string rawFileName, int expectedResult)
        {
            var dataFile = GetRawDataFile(rawFileName);

            using (var reader = new MSDataFileReader(dataFile.FullName))
            {
                var scanCount = reader.SpectrumCount;

                Console.WriteLine("Scan count for {0}: {1}", dataFile.Name, scanCount);
                Assert.AreEqual(expectedResult, scanCount, "Scan count mismatch");
            }
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
        public void TestGetScanCountsByScanType(
            string rawFileName,
            int scanStart,
            int scanEnd,
            int expectedMS1,
            int expectedMS2,
            int expectedTotalScanCount)
        {
            // Keys in this Dictionary are filename, values are ScanCounts by collision mode, where the key is a Tuple of ScanType and FilterString
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


            var dataFile = GetRawDataFile(rawFileName);
            var errorCount = 0;

            using (var reader = new MSDataFileReader(dataFile.FullName))
            {
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

                    GetScanFilterText(cvScanInfo, out var filterText);

                    if (filterText == null)
                    {
                        Console.WriteLine("No filter string for scan {0}", scanNumber);
                        errorCount += 1;

                        if (errorCount > 25)
                            return;

                        continue;
                    }

                    var scanType = XRawFileIO.GetScanTypeNameFromFinniganScanFilterText(filterText);
                    var genericScanFilter = XRawFileIO.MakeGenericFinniganScanFilter(filterText);

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

                if (!expectedData.TryGetValue(Path.GetFileNameWithoutExtension(dataFile.Name), out var expectedScanInfo))
                {
                    Assert.Fail("Dataset {0} not found in dictionary expectedData", dataFile.Name);
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
                        Console.WriteLine("Unexpected scan type found: {0}", scanType.Key);
                        Assert.Fail("Unexpected scan type found: {0}", scanType.Key);
                    }
                }
            }
        }

        [Test]
        [TestCase("Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20.RAW", 1513, 1521, 3, 6)]
        [TestCase("HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53.raw", 16121, 16165, 3, 42)]
        public void TestGetScanInfo(string rawFileName, int scanStart, int scanEnd, int expectedMS1, int expectedMS2)
        {
            var expectedData = new Dictionary<string, Dictionary<int, string>>();

            // Keys in this dictionary are the scan number whose metadata is being retrieved
            var file1Data = new Dictionary<int, string>
            {
                // Scan MSLevel NumPeaks RetentionTime DriftTimeMsec LowMass HighMass TotalIonCurrent BasePeakMZ BasePeakIntensity ParentIonMZ ActivationType IonMode IsCentroided ScanStartTime IonInjectionTime FilterText
                {1513, "1   851 44.57 0 400 2000 6.3E+8 1089.978 1.2E+7     0.00          positive True    1.50 + c ESI Full..."},
                {1514, "2   109 44.60 0 230 1780 5.0E+6  528.128 7.2E+5   884.41 cid      positive True   28.96 + c d Full m..."},
                {1515, "2   290 44.63 0 305 2000 2.6E+7 1327.414 6.0E+6  1147.67 cid      positive True   14.13 + c d Full m..."},
                {1516, "2   154 44.66 0 400 2000 7.6E+5 1251.554 3.7E+4  1492.90 cid      positive True  123.30 + c d Full m..."},
                {1517, "1   887 44.69 0 400 2000 8.0E+8 1147.613 1.0E+7     0.00          positive True    1.41 + c ESI Full..."},
                {1518, "2   190 44.71 0 380 2000 4.6E+6 1844.618 2.7E+5  1421.21 cid      positive True   40.91 + c d Full m..."},
                {1519, "2   165 44.74 0 380 2000 6.0E+6 1842.547 6.9E+5  1419.24 cid      positive True   37.84 + c d Full m..."},
                {1520, "2   210 44.77 0 265 2000 1.5E+6 1361.745 4.2E+4  1014.93 cid      positive True   96.14 + c d Full m..."},
                {1521, "1   860 44.80 0 400 2000 6.9E+8 1126.627 2.9E+7     0.00          positive True    1.45 + c ESI Full..."}
            };
            expectedData.Add("Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20", file1Data);

            var file2Data = new Dictionary<int, string>
            {
                {16121, "1 11888 47.68 0 350 1550 1.9E+9  503.565 3.4E+8     0.00          positive False   0.44 FTMS + p NSI..."},
                {16122, "2   490 47.68 0 106  817 1.6E+6  550.309 2.1E+5   403.22 cid      positive True   11.82 ITMS + c NSI..."},
                {16123, "2   785 47.68 0 143 1627 5.5E+5  506.272 4.9E+4   538.84 cid      positive True   26.07 ITMS + c NSI..."},
                {16124, "2   996 47.68 0 208 2000 7.8E+5  737.530 7.0E+4   775.94 cid      positive True   24.65 ITMS + c NSI..."},
                {16125, "2   703 47.68 0 120 1627 2.1E+5  808.486 2.2E+4   538.84 etd      positive True   42.48 ITMS + c NSI..."},
                {16126, "2   753 47.68 0 120 1627 1.4E+5  536.209 9.0E+3   538.84 cid, etd positive True   58.96 ITMS + c NSI..."},
                {16127, "2   872 47.68 0 120 1627 1.3E+5  808.487 1.4E+4   538.84 etd, hcd positive True   58.96 ITMS + c NSI..."},
                {16128, "2   972 47.69 0 225 1682 4.4E+5  805.579 2.3E+4   835.88 cid      positive True   42.71 ITMS + c NSI..."},
                {16129, "2   937 47.69 0 266 1986 3.4E+5  938.679 2.9E+4   987.40 cid      positive True   35.75 ITMS + c NSI..."},
                {16130, "2   622 47.69 0 110  853 2.7E+5  411.977 1.2E+4   421.26 cid      positive True   50.98 ITMS + c NSI..."},
                {16131, "2    29 47.69 0 120 1986 2.1E+4  984.504 9.5E+3   987.40 etd      positive True   26.55 ITMS + c NSI..."},
                {16132, "2   239 47.69 0 120  853 1.2E+4  421.052 6.8E+2   421.26 etd      positive True  127.21 ITMS + c NSI..."},
                {16133, "2   280 47.70 0 120  853 1.5E+4  421.232 1.2E+3   421.26 cid, etd positive True  110.21 ITMS + c NSI..."},
                {16134, "2   343 47.70 0 120  853 1.4E+4  838.487 7.5E+2   421.26 etd, hcd positive True  110.21 ITMS + c NSI..."},
                {16135, "2    38 47.70 0 120 1986 2.1E+4  984.498 9.2E+3   987.40 cid, etd positive True   31.82 ITMS + c NSI..."},
                {16136, "2    93 47.71 0 120 1986 2.3E+4  984.491 9.4E+3   987.40 etd, hcd positive True   31.82 ITMS + c NSI..."},
                {16137, "2  1172 47.71 0 336 2000 3.5E+5 1536.038 4.7E+3  1240.76 cid      positive True   30.70 ITMS + c NSI..."},
                {16138, "2   925 47.72 0 235 1760 2.9E+5  826.095 2.5E+4   874.84 cid      positive True   40.56 ITMS + c NSI..."},
                {16139, "2    96 47.72 0 120 1760 1.6E+4  875.506 2.1E+3   874.84 etd      positive True   45.88 ITMS + c NSI..."},
                {16140, "2   174 47.72 0 120 1760 1.8E+4 1749.846 2.0E+3   874.84 cid, etd positive True   54.15 ITMS + c NSI..."},
                {16141, "2   240 47.72 0 120 1760 1.6E+4  874.664 1.6E+3   874.84 etd, hcd positive True   54.15 ITMS + c NSI..."},
                {16142, "1 13501 47.73 0 350 1550 1.3E+9  503.565 1.9E+8     0.00          positive False   0.79 FTMS + p NSI..."},
                {16143, "2   651 47.73 0 128  981 6.5E+5  444.288 6.4E+4   485.28 cid      positive True   22.26 ITMS + c NSI..."},
                {16144, "2   512 47.73 0 101 1561 5.0E+5  591.309 4.0E+4   387.41 cid      positive True   28.19 ITMS + c NSI..."},
                {16145, "2   817 47.73 0 162 1830 4.0E+5  567.912 2.8E+4   606.29 cid      positive True   37.30 ITMS + c NSI..."},
                {16146, "2   573 47.73 0  99  770 1.9E+5  532.308 3.4E+4   379.72 cid      positive True  100.00 ITMS + c NSI..."},
                {16147, "2   813 47.74 0 120 1830 3.8E+5  603.095 3.1E+4   606.29 etd      positive True   25.47 ITMS + c NSI..."},
                {16148, "2   882 47.74 0 120 1830 1.5E+5  603.076 1.3E+4   606.29 cid, etd positive True   61.48 ITMS + c NSI..."},
                {16149, "2  1121 47.74 0 120 1830 1.6E+5  603.027 1.1E+4   606.29 etd, hcd positive True   61.48 ITMS + c NSI..."},
                {16150, "2   625 47.74 0  95 1108 3.8E+5  418.536 1.2E+5   365.88 cid      positive True  134.71 ITMS + c NSI..."},
                {16151, "2   679 47.75 0 146 1656 2.8E+5  501.523 4.3E+4   548.54 cid      positive True   30.59 ITMS + c NSI..."},
                {16152, "2  1171 47.75 0 328 2000 1.8E+5  848.497 2.2E+3  1210.06 cid      positive True   38.05 ITMS + c NSI..."},
                {16153, "2   600 47.75 0 120 1656 1.3E+5  548.396 1.3E+4   548.54 etd      positive True   50.35 ITMS + c NSI..."},
                {16154, "2   566 47.75 0 120 1656 4.2E+4  548.450 4.2E+3   548.54 cid, etd positive True  122.26 ITMS + c NSI..."},
                {16155, "2   753 47.76 0 120 1656 4.2E+4  550.402 3.6E+3   548.54 etd, hcd positive True  122.26 ITMS + c NSI..."},
                {16156, "2  1120 47.76 0 324 2000 1.5E+5 1491.872 1.0E+4  1197.16 cid      positive True   63.61 ITMS + c NSI..."},
                {16157, "2   714 47.76 0 124  950 2.2E+5  420.689 2.2E+4   469.71 cid      positive True  100.00 ITMS + c NSI..."},
                {16158, "2   692 47.76 0 306 2000 1.3E+5 1100.042 3.5E+3  1132.02 cid      positive True   27.79 ITMS + c NSI..."},
                {16159, "2   667 47.76 0 122  935 1.9E+5  445.117 2.7E+4   462.15 cid      positive True   69.09 ITMS + c NSI..."},
                {16160, "2   694 47.77 0 145 1646 3.4E+5  539.065 6.0E+4   544.84 cid      positive True   28.97 ITMS + c NSI..."},
                {16161, "2   737 47.77 0 157 1191 2.8E+5  541.462 6.0E+4   590.28 cid      positive True   37.92 ITMS + c NSI..."},
                {16162, "2   288 47.77 0 120 1191 8.4E+4 1180.615 5.1E+3   590.28 etd      positive True   38.31 ITMS + c NSI..."},
                {16163, "2   305 47.77 0 120 1191 1.8E+4 1184.614 9.0E+2   590.28 cid, etd positive True  109.20 ITMS + c NSI..."},
                {16164, "2   372 47.77 0 120 1191 1.7E+4 1184.644 8.7E+2   590.28 etd, hcd positive True  109.20 ITMS + c NSI..."},
                {16165, "1 13816 47.78 0 350 1550 1.2E+9  503.565 1.6E+8     0.00          positive False   0.76 FTMS + p NSI..."}
            };
            expectedData.Add("HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53", file2Data);

            var dataFile = GetRawDataFile(rawFileName);

            using (var reader = new MSDataFileReader(dataFile.FullName))
            {
                var scanNumberToIndexMap = reader.GetScanToIndexMapping();

                Console.WriteLine("Scan info for {0}", dataFile.Name);
                Console.WriteLine("{0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11} {12} {13} {14} {15}",
                                  "Scan", "MSLevel",
                                  "NumPeaks", "RetentionTime", "DriftTimeMsec",
                                  "LowMass", "HighMass", "TotalIonCurrent",
                                  "BasePeakMZ", "BasePeakIntensity",
                                  "ParentIonMZ", "ActivationType",
                                  "IonMode", "IsCentroided",
                                  "IonInjectionTime", "FilterText");

                var scanCountMS1 = 0;
                var scanCountMS2 = 0;

                foreach (var scan in scanNumberToIndexMap.Where(x => x.Key >= scanStart && x.Key <= scanEnd))
                {
                    var scanNumber = scan.Key;
                    var spectrumIndex = scan.Value;

                    var spectrum = reader.GetSpectrum(spectrumIndex, true);
                    var spectrumParams = reader.GetSpectrumCVParamData(spectrumIndex);
                    var cvScanInfo = reader.GetSpectrumScanInfo(spectrumIndex);

                    Assert.IsTrue(spectrum != null, "GetSpectrum returned a null object for scan {0}", scanNumber);

                    var totalIonCurrent = cvParamUtilities.GetCvParamValueDbl(spectrumParams, cvParamUtilities.CVIDs.MS_TIC);
                    var basePeakMZ = cvParamUtilities.GetCvParamValueDbl(spectrumParams, cvParamUtilities.CVIDs.MS_base_peak_m_z);
                    var basePeakIntensity = cvParamUtilities.GetCvParamValueDbl(spectrumParams, cvParamUtilities.CVIDs.MS_base_peak_intensity);

                    double parentIonMZ = 0;
                    var activationType = string.Empty;

                    if (spectrum.Precursors.Length > 0)
                    {
                        var precursor = spectrum.Precursors[0];

                        parentIonMZ = precursor.PrecursorMz.GetValueOrDefault();
                        if (precursor.ActivationTypes != null)
                            activationType = string.Join(", ", precursor.ActivationTypes);
                    }

                    GetScanMetadata(cvScanInfo, out var scanStartTime, out var ionInjectionTime, out var filterText, out var lowMass, out var highMass);

                    var retentionTime = cvParamUtilities.CheckNull(spectrum.RetentionTime);
                    Assert.AreEqual(retentionTime, scanStartTime, 0.0001, "Mismatch between spectrum.RetentionTime and CVParam MS_scan_start_time");

                    var numPeaks = spectrum.Mzs.Length;
                    var ionMode = spectrum.NegativeCharge ? "negative" : "positive";

                    var scanSummary =
                        string.Format(
                            "{0} {1} {2,5} {3:0.00} {4:0} {5,3:0} {6,4:0} {7:0.0E+0} {8,8:0.000} {9:0.0E+0} {10,8:0.00} {11,-8} {12} {13,-5} {14,6:0.00} {15}",
                            scanNumber, spectrum.Level,
                            numPeaks, retentionTime,
                            cvParamUtilities.CheckNull(spectrum.DriftTimeMsec),
                            lowMass, highMass,
                            totalIonCurrent,
                            basePeakMZ, basePeakIntensity, parentIonMZ,
                            activationType,
                            ionMode, spectrum.Centroided, ionInjectionTime,
                            filterText.Substring(0, 12) + "...");

                    Console.WriteLine(scanSummary);

                    if (spectrum.Level > 1)
                        scanCountMS2++;
                    else
                        scanCountMS1++;

                    if (!expectedData.TryGetValue(Path.GetFileNameWithoutExtension(dataFile.Name), out var expectedDataThisFile))
                    {
                        Assert.Fail("Dataset {0} not found in dictionary expectedData", dataFile.Name);
                    }

                    if (expectedDataThisFile.TryGetValue(scanNumber, out var expectedScanSummary))
                    {
                        Assert.AreEqual(scanNumber + " " + expectedScanSummary, scanSummary,
                                        "Scan summary mismatch, scan " + scanNumber);
                    }

                    var expectedNativeId = string.Format("controllerType=0 controllerNumber=1 scan={0}", scanNumber);
                    Assert.AreEqual(spectrum.NativeId, expectedNativeId, "NativeId is not in the expected format for scan {0}", scanNumber);
                }

                Console.WriteLine("scanCountMS1={0}", scanCountMS1);
                Console.WriteLine("scanCountMS2={0}", scanCountMS2);

                Assert.AreEqual(expectedMS1, scanCountMS1, "MS1 scan count mismatch");
                Assert.AreEqual(expectedMS2, scanCountMS2, "MS2 scan count mismatch");
            }
        }

        [Test]
        [TestCase("Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20.RAW", 1513, 1521)]
        [TestCase("HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53.raw", 16121, 16165)]
        public void TestGetScanData(string rawFileName, int scanStart, int scanEnd)
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
            file1Data[1513].Add("False", "851      851      409.615  4.8E+5   1227.956 1.6E+6    + c ESI Full ms [400.00-2000.00]");
            file1Data[1514].Add("False", "109      109      281.601  2.4E+4   633.151  4.4E+4    + c d Full ms2 884.41@cid45.00 [230.00-1780.00]");
            file1Data[1515].Add("False", "290      290      335.798  3.8E+4   1034.194 1.6E+4    + c d Full ms2 1147.67@cid45.00 [305.00-2000.00]");
            file1Data[1516].Add("False", "154      154      461.889  7.3E+3   1203.274 2.6E+3    + c d Full ms2 1492.90@cid45.00 [400.00-2000.00]");
            file1Data[1517].Add("False", "887      887      420.016  9.7E+5   1232.206 8.0E+5    + c ESI Full ms [400.00-2000.00]");

            file1Data[1513].Add("True", "851      851      409.615  4.8E+5   1227.956 1.6E+6    + c ESI Full ms [400.00-2000.00]");
            file1Data[1514].Add("True", "109      109      281.601  2.4E+4   633.151  4.4E+4    + c d Full ms2 884.41@cid45.00 [230.00-1780.00]");
            file1Data[1515].Add("True", "290      290      335.798  3.8E+4   1034.194 1.6E+4    + c d Full ms2 1147.67@cid45.00 [305.00-2000.00]");
            file1Data[1516].Add("True", "154      154      461.889  7.3E+3   1203.274 2.6E+3    + c d Full ms2 1492.90@cid45.00 [400.00-2000.00]");
            file1Data[1517].Add("True", "887      887      420.016  9.7E+5   1232.206 8.0E+5    + c ESI Full ms [400.00-2000.00]");

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

            file2Data[16121].Add("False", " 11888    11888    346.518  0.0E+0   706.844  9.8E+4    FTMS + p NSI Full ms [350.0000-1550.0000]");
            file2Data[16122].Add("False", " 490      490      116.232  7.0E+1   403.932  1.1E+3    ITMS + c NSI r d Full ms2 403.2206@cid30.00 [106.0000-817.0000]");
            file2Data[16126].Add("False", " 753      753      231.045  1.1E+1   1004.586 2.0E+1    ITMS + c NSI r d sa Full ms2 538.8400@etd53.58@cid20.00 [120.0000-1627.0000]");
            file2Data[16131].Add("False", " 29       29       984.504  9.5E+3   1931.917 2.4E+1    ITMS + c NSI r d Full ms2 987.8934@etd120.55 [120.0000-1986.0000]");
            file2Data[16133].Add("False", " 280      280      260.118  2.3E+1   663.160  7.7E+0    ITMS + c NSI r d sa Full ms2 421.2619@etd120.55@cid20.00 [120.0000-853.0000]");
            file2Data[16141].Add("False", " 240      240      304.425  1.3E+1   1447.649 3.0E+1    ITMS + c NSI r d sa Full ms2 874.8397@etd120.55@hcd20.00 [120.0000-1760.0000]");

            file2Data[16121].Add("True", " 833      833      351.231  2.9E+5   712.813  2.9E+5    FTMS + p NSI Full ms [350.0000-1550.0000]");
            file2Data[16122].Add("True", " 490      490      116.232  7.0E+1   403.932  1.1E+3    ITMS + c NSI r d Full ms2 403.2206@cid30.00 [106.0000-817.0000]");
            file2Data[16126].Add("True", " 753      753      231.045  1.1E+1   1004.586 2.0E+1    ITMS + c NSI r d sa Full ms2 538.8400@etd53.58@cid20.00 [120.0000-1627.0000]");
            file2Data[16131].Add("True", " 29       29       984.504  9.5E+3   1931.917 2.4E+1    ITMS + c NSI r d Full ms2 987.8934@etd120.55 [120.0000-1986.0000]");
            file2Data[16133].Add("True", " 280      280      260.118  2.3E+1   663.160  7.7E+0    ITMS + c NSI r d sa Full ms2 421.2619@etd120.55@cid20.00 [120.0000-853.0000]");
            file2Data[16141].Add("True", " 240      240      304.425  1.3E+1   1447.649 3.0E+1    ITMS + c NSI r d sa Full ms2 874.8397@etd120.55@hcd20.00 [120.0000-1760.0000]");

            expectedData.Add("HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53", file2Data);

            var dataFile = GetRawDataFile(rawFileName);

            for (var iteration = 1; iteration <= 2; iteration++)
            {
                var centroidData = (iteration > 1);

                using (var reader = new MSDataFileReader(
                    dataFile.FullName,
                    requireVendorCentroidedMS1: centroidData,
                    requireVendorCentroidedMS2: centroidData))
                {
                    if (iteration == 1)
                    {
                        Console.WriteLine("Scan data for {0}", dataFile.Name);
                        Console.WriteLine("{0} {1,8} {2,-8} {3,-8} {4,-8} {5,-8} {6,-8} {7,-8}  {8}",
                                          "Scan", "Centroid", "MzCount", "IntCount",
                                          "FirstMz", "FirstInt", "MidMz", "MidInt", "ScanFilter");
                    }

                    var scanNumberToIndexMap = reader.GetScanToIndexMapping();

                    foreach (var scan in scanNumberToIndexMap.Where(x => x.Key >= scanStart && x.Key <= scanEnd))
                    {
                        var scanNumber = scan.Key;
                        var spectrumIndex = scan.Value;

                        var spectrum = reader.GetSpectrum(spectrumIndex, true);

                        var cvScanInfo = reader.GetSpectrumScanInfo(spectrumIndex);

                        var dataPointsRead = spectrum.Mzs.Length;

                        Assert.IsTrue(dataPointsRead > 0, "GetScanData returned 0 for scan {0}", scanNumber);

                        var midPoint = (int)(spectrum.Intensities.Length / 2f);

                        GetScanFilterText(cvScanInfo, out var filterText);

                        var scanSummary =
                            string.Format(
                                "{0} {1,8} {2,-8} {3,-8} {4,-8:0.000} {5,-8:0.0E+0} {6,-8:0.000} {7,-8:0.0E+0}  {8}",
                                scanNumber, centroidData,
                                spectrum.Mzs.Length, spectrum.Intensities.Length,
                                spectrum.Mzs[0], spectrum.Intensities[0],
                                spectrum.Mzs[midPoint], spectrum.Intensities[midPoint],
                                filterText);

                        Console.WriteLine(scanSummary);

                        if (!expectedData.TryGetValue(Path.GetFileNameWithoutExtension(dataFile.Name), out var expectedDataThisFile))
                        {
                            Assert.Fail("Dataset {0} not found in dictionary expectedData", dataFile.Name);
                        }

                        if (expectedDataThisFile.TryGetValue(scanNumber, out var expectedDataByType))
                        {
                            var keySpec = centroidData.ToString();
                            if (expectedDataByType.TryGetValue(keySpec, out var expectedDataDetails))
                            {
                                Assert.AreEqual(expectedDataDetails, scanSummary.Substring(14),
                                                "Scan details mismatch, scan " + scanNumber + ", keySpec " + keySpec);
                            }
                        }

                    }
                }
            }

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
        /// <returns></returns>
        private FileInfo GetRawDataFile(string rawFileName)
        {
            const string REMOTE_PATH = @"\\proto-2\UnitTest_Files\ThermoRawFileReader";

            if (InstrumentDataUtilities.FindInstrumentData(rawFileName, false, REMOTE_PATH, out var instrumentDataFile))
            {
                if (instrumentDataFile is FileInfo rawFile)
                    return rawFile;
            }

            Assert.Fail("File not found: " + rawFileName);
            return null;
        }

        private static void GetScanFilterText(SpectrumScanContainer cvScanInfo, out string filterText)
        {
            GetScanMetadata(cvScanInfo, out _, out _, out filterText, out _, out _);
        }

        private static void GetScanMetadata(
            SpectrumScanContainer cvScanInfo,
            out double scanStartTime,
            out double ionInjectionTime,
            out string filterText,
            out double lowMass,
            out double highMass)
        {
            scanStartTime = 0;
            ionInjectionTime = 0;
            filterText = string.Empty;
            lowMass = 0;
            highMass = 0;

            // Lookup details on the scan associated with this spectrum
            // (cvScanInfo.Scans is a list, but Thermo .raw files typically have a single scan for each spectrum)
            foreach (var scanEntry in cvScanInfo.Scans)
            {
                scanStartTime = cvParamUtilities.GetCvParamValueDbl(scanEntry.CVParams, cvParamUtilities.CVIDs.MS_scan_start_time);
                ionInjectionTime = cvParamUtilities.GetCvParamValueDbl(scanEntry.CVParams, cvParamUtilities.CVIDs.MS_ion_injection_time);
                filterText = cvParamUtilities.GetCvParamValue(scanEntry.CVParams, cvParamUtilities.CVIDs.MS_filter_string);

                lowMass = cvParamUtilities.GetCvParamValueDbl(scanEntry.ScanWindowList, cvParamUtilities.CVIDs.MS_scan_window_lower_limit);
                highMass = cvParamUtilities.GetCvParamValueDbl(scanEntry.ScanWindowList, cvParamUtilities.CVIDs.MS_scan_window_upper_limit);

                break;
            }

        }
    }
}
