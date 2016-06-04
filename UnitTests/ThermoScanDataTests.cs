using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ThermoRawFileReader;

namespace ProteowizardWrapperUnitTests
{
    [TestFixture]
    public class ThermoScanDataTests
    {
        private const bool USE_REMOTE_PATHS = true;

        private enum CVIDs
        {
            MS_filter_string = 1000512
        }

        [Test]
        [TestCase(@"Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20.RAW")]
        [TestCase(@"HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53.raw")]
        [TestCase(@"HCC-38_ETciD_EThcD_07Jan16_Pippin_15-08-53.raw")]
        [TestCase(@"MZ0210MnxEF889ETD.raw")]
        public void TestGetCollisionEnergy(string rawFileName)
        {
            // Keys in this Dictionary are filename, values are Collision Energies by scan
            var expectedData = new Dictionary<string, Dictionary<int, List<double>>>();

            var ce30 = new List<double> { 30.00 };
            var ce45 = new List<double> { 45.00 };
            var ce120 = new List<double> { 120.550003 };
            var ms1Scan = new List<double>();
            var etdScanBuggyResults = new List<double>();

            // Keys in this dictionary are scan number and values are collision energies
            var file1Data = new Dictionary<int, List<double>> {
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

            var file2Data = new Dictionary<int, List<double>> {
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

            var file3Data = new Dictionary<int, List<double>> {
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

            var file4Data = new Dictionary<int, List<double>> {
                {1, etdScanBuggyResults},   // This is an ETD scan with collision energy 30.00
                {2, etdScanBuggyResults},   // This is an ETD scan with collision energy 30.00
               
            };
            expectedData.Add("MZ0210MnxEF889ETD", file4Data);

            
            var dataFile = GetRawDataFile(rawFileName);

            Dictionary<int, List<double>> collisionEnergiesThisFile;
            if (!expectedData.TryGetValue(Path.GetFileNameWithoutExtension(dataFile.Name), out collisionEnergiesThisFile))
            {
                Assert.Fail("Dataset {0} not found in dictionary expectedData", dataFile.Name);
            }

            // Keys are scan number, values are the list of collision energies
            var collisionEnergiesActual = new Dictionary<int, List<double>>();

            // Keys are scan number, values are msLevel
            var msLevelsActual = new Dictionary<int, int>();

            // Keys are scan number, values are the ActivationType (or list of activation types), for example cid, etd, hcd
            var activationTypesActual = new Dictionary<int, List<string>>();

            using (var oWrapper = new pwiz.ProteowizardWrapper.MSDataFileReader(dataFile.FullName))
            {

                Console.WriteLine("Examining data in " + dataFile.Name);

                var scanNumberToIndexMap = oWrapper.GetThermoScanToIndexMapping();

                foreach (var scanNumber in collisionEnergiesThisFile.Keys)
                {
                    int spectrumIndex;
                    if (!scanNumberToIndexMap.TryGetValue(scanNumber, out spectrumIndex))
                    {
                        Assert.Fail("ScanToIndexMap does not contain scan number " + scanNumber);
                    }

                    var spectrum = oWrapper.GetSpectrum(spectrumIndex, false);

                    Assert.IsTrue(spectrum != null, "GetSpectrum returned a null object for scan " + scanNumber);

                    var precursors = oWrapper.GetPrecursors(spectrumIndex);

                    var collisionEnergiesThisScan = (from precursor in precursors
                                                     select precursor.PrecursorCollisionEnergy into collisionEnergy
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

                            Console.WriteLine("{0,-5} {1,-5} {2}", isValid, scanNumber, actualEnergy.ToString("0.00"));

                            Assert.IsTrue(isValid, "Unexpected collision energy {0} for scan {1}", actualEnergy.ToString("0.00"), scanNumber);
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
        [TestCase(@"Shew_246a_LCQa_15Oct04_Andro_0904-2_4-20.RAW", 3316)]
        [TestCase(@"HCC-38_ETciD_EThcD_4xdil_20uL_3hr_3_08Jan16_Pippin_15-08-53.raw", 71147)]
        public void TestGetNumScans(string rawFileName, int expectedResult)
        {
            var dataFile = GetRawDataFile(rawFileName);

            using (var oWrapper = new pwiz.ProteowizardWrapper.MSDataFileReader(dataFile.FullName))
            {
                var scanCount = oWrapper.SpectrumCount;

                Console.WriteLine("Scan count for {0}: {1}", dataFile.Name, scanCount);
                Assert.AreEqual(expectedResult, scanCount, "Scan count mismatch");
            }
        }

        [Test]
        [TestCase(@"B5_50uM_MS_r1.RAW", 1, 20, 20, 0)]
        [TestCase(@"MNSLTFKK_ms.raw", 1, 88, 88, 0)]
        [TestCase(@"QCShew200uL.raw", 4000, 4100, 101, 0)]
        [TestCase(@"Wrighton_MT2_SPE_200avg_240k_neg_330-380.raw", 1, 200, 200, 0)]
        [TestCase(@"1229_02blk1.raw", 6000, 6100, 77, 24)]
        [TestCase(@"MCF7_histone_32_49B_400min_HCD_ETD_01172014_b.raw", 2300, 2400, 18, 83)]
        [TestCase(@"lowdose_IMAC_iTRAQ1_PQDMSA.raw", 15000, 15100, 16, 85)]
        [TestCase(@"MZ20150721blank2.raw", 1, 434, 62, 372)]
        [TestCase(@"OG_CEPC_PU_22Oct13_Legolas_13-05-12.raw", 5000, 5100, 9, 92)]
        [TestCase(@"blank_MeOH-3_18May16_Rainier_Thermo_10344958.raw", 1500, 1900, 190, 211)]
        [TestCase(@"HCC-38_ETciD_EThcD_07Jan16_Pippin_15-08-53.raw", 25200, 25600, 20, 381)]
        [TestCase(@"MeOHBlank03POS_11May16_Legolas_HSS-T3_A925.raw", 5900, 6000, 8, 93)]
        [TestCase(@"IPA-blank-07_25Oct13_Gimli.raw", 1750, 1850, 101, 0)]
        public void TestGetScanCountsByScanType(string rawFileName, int scanStart, int scanEnd, int expectedMS1, int expectedMS2)
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

            using (var oWrapper = new pwiz.ProteowizardWrapper.MSDataFileReader(dataFile.FullName))
            {
                Console.WriteLine("Parsing scan headers for {0}", dataFile.Name);

                var scanNumberToIndexMap = oWrapper.GetThermoScanToIndexMapping();

                var scanCountMS1 = 0;
                var scanCountMS2 = 0;
                var scanTypeCountsActual = new Dictionary<Tuple<string, string>, int>();
                var lastProgress = DateTime.Now;

                foreach (var scan in scanNumberToIndexMap)
                {
                    var spectrumIndex = scan.Key;
                    var scanNumber = scan.Value;

                    var spectrum = oWrapper.GetSpectrum(spectrumIndex, false);

                    var cvScanInfo = oWrapper.GetSpectrumScanInfo(spectrumIndex);

                    Assert.IsTrue(cvScanInfo != null, "GetSpectrumScanInfo returned a null object for scan {0}", scanNumber);

                    string filterText = null;

                    foreach(var scanEntry in cvScanInfo.Scans)
                    {
                        var query = (from item in scanEntry.CVParams where item.CVId == (int)CVIDs.MS_filter_string select item).ToList();

                        if (query.Count == 0)
                            continue;

                        filterText = query.First().Value;
                        break;

                    }

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

                    int observedScanCount;
                    if (scanTypeCountsActual.TryGetValue(scanTypeKey, out observedScanCount))
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

                Dictionary<Tuple<string, string>, int> expectedScanInfo;
                if (!expectedData.TryGetValue(Path.GetFileNameWithoutExtension(dataFile.Name), out expectedScanInfo))
                {
                    Assert.Fail("Dataset {0} not found in dictionary expectedData", dataFile.Name);
                }

                Console.WriteLine("{0,-5} {1,5} {2}", "Valid", "Count", "ScanType");

                foreach (var scanType in (from item in scanTypeCountsActual orderby item.Key select item))
                {
                    int expectedScanCount;
                    if (expectedScanInfo.TryGetValue(scanType.Key, out expectedScanCount))
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

        private void AddExpectedTupleAndCount(
            IDictionary<string, Dictionary<Tuple<string, string>, int>> expectedData,
            string fileName,
            string tupleKey1,
            string tupleKey2,
            int scanCount)
        {

            Dictionary<Tuple<string, string>, int> expectedScanInfo;
            if (!expectedData.TryGetValue(fileName, out expectedScanInfo))
            {
                expectedScanInfo = new Dictionary<Tuple<string, string>, int>();
                expectedData.Add(fileName, expectedScanInfo);
            }

            expectedScanInfo.Add(new Tuple<string, string>(tupleKey1, tupleKey2), scanCount);
        }

        private FileInfo GetRawDataFile(string rawFileName)
        {
            FileInfo dataFile;

            if (USE_REMOTE_PATHS)
            {
                dataFile = new FileInfo(Path.Combine(@"\\proto-2\UnitTest_Files\ThermoRawFileReader", rawFileName));
            }
            else
            {
                dataFile = new FileInfo(Path.Combine(@"F:\MSData\UnitTestFiles", rawFileName));
            }

            if (!dataFile.Exists)
            {
                Assert.Fail("File not found: " + dataFile.FullName);
            }
          
            return dataFile;
        }

    }
}
