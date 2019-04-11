using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using pwiz.ProteowizardWrapper;

namespace ProteowizardWrapperUnitTests
{
    [TestFixture]
    class UimfScanDataTests
    {
        private const bool USE_REMOTE_PATHS = true;

        [Test]
        [TestCase("20160211_Agilent_tunemix_pos_0002.UIMF", 1, 20, 2809, 0, 2799)]
        [TestCase("QC_Shew_IMER_500ng_Run-1_4May16_Oak_15-01-16.UIMF", 50, 70, 2078, 0, 2078)]
        [TestCase("Corrupt_20160504-4uMppep3_100mMformic-NOMeoh_20msec_50c.UIMF", 1, 1, 0, 0, 0)]
        public void TestCorruptDataHandling(
            string uimfFileName,
            int frameStart,
            int frameEnd,
            int expectedMS1,
            int expectedMS2,
            int expectedScansWithData)
        {
            var dataFile = GetUimfDataFile(uimfFileName);

            try
            {
                using (var oWrapper = new MSDataFileReader(dataFile.FullName))
                {

                    var scanCount = oWrapper.SpectrumCount;
                    Console.WriteLine("Scan count for {0}: {1}", dataFile.Name, scanCount);

                    if (expectedMS1 + expectedMS2 == 0)
                    {
                        Assert.IsTrue(scanCount == 0, "ScanCount is non-zero, while we expected it to be 0");
                    }
                    else
                    {
                        Assert.IsTrue(scanCount > 0, "ScanCount is zero, while we expected it to be > 0");
                    }

                    var frameScanPairToIndexMap = oWrapper.GetUimfFrameScanPairToIndexMapping();

                    var scanCountMS1 = 0;
                    var scanCountMS2 = 0;
                    var scansWithData = 0;

                    foreach (var frame in frameScanPairToIndexMap)
                    {
                        var frameNumber = frame.Key.Key;
                        var scanNumber = frame.Key.Value;
                        var spectrumIndex = frame.Value;

                        if (frameNumber < frameStart || frameNumber > frameEnd)
                        {
                            continue;
                        }

                        try
                        {
                            var spectrum = oWrapper.GetSpectrum(spectrumIndex, true);

                            var cvScanInfo = oWrapper.GetSpectrumScanInfo(spectrumIndex);

                            Assert.IsTrue(cvScanInfo != null, "GetSpectrumScanInfo returned a null object for frame {0}, scan {1} ", frameNumber, scanNumber);

                            if (spectrum.Level > 1)
                                scanCountMS2++;
                            else
                                scanCountMS1++;

                            var dataPointCount = spectrum.Mzs.Length;
                            if (dataPointCount > 0)
                                scansWithData++;

                            Assert.IsTrue(spectrum.Mzs.Length == spectrum.Intensities.Length, "Array length mismatch for m/z and intensity data for frame {0}, scan {1} ", frameNumber, scanNumber);

                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Exception reading scan {0}: {1}", scanNumber, ex.Message);
                            Assert.Fail("Exception reading scan {0}", scanNumber);
                        }
                    }

                    Console.WriteLine("scanCountMS1={0}", scanCountMS1);
                    Console.WriteLine("scanCountMS2={0}", scanCountMS2);
                    Console.WriteLine("scansWithData={0}", scansWithData);


                    Assert.AreEqual(expectedMS1, scanCountMS1, "MS1 scan count mismatch");
                    Assert.AreEqual(expectedMS2, scanCountMS2, "MS2 scan count mismatch");
                    Assert.AreEqual(expectedScansWithData, scansWithData, "ScansWithData count mismatch");
                }
            }
            catch (Exception ex)
            {
                if (expectedMS1 + expectedMS2 == 0)
                {
                    Console.WriteLine("Error opening .uimf file (this was expected):\n{0}", ex.Message);
                }
                else
                {
                    var msg = string.Format("Exception opening .uimf file {0}:\n{1}", uimfFileName, ex.Message);
                    Console.WriteLine(msg);
                    Assert.Fail(msg);
                }
            }

        }

        [Test]
        [TestCase("9_Peptide_Mix_16Oct14_Cedar_Infuse.UIMF", 1, 5, 1356, 0, 14763)]
        [TestCase("9pep_mix_1uM_4bit_50_3Jun16.UIMF", 1, 100, 184, 0, 184)]
        [TestCase("QC_Shew_IMER_500ng_Run-1_4May16_Oak_15-01-16.UIMF", 515, 533, 2122, 0, 205239)]
        [TestCase("20160524_TuneMix_1574V_neg_001.UIMF", 20, 30, 8250, 0, 26250)]
        public void TestGetScanCountsByScanType(
            string uimfFileName,
            int frameStart,
            int frameEnd,
            int expectedMS1,
            int expectedMS2,
            int expectedTotalScanCount)
        {
            var dataFile = GetUimfDataFile(uimfFileName);

            using (var oWrapper = new MSDataFileReader(dataFile.FullName))
            {
                Console.WriteLine("Parsing scan headers for {0}", dataFile.Name);

                var scanCount = oWrapper.SpectrumCount;
                Console.WriteLine("Total scans: {0}", scanCount);
                Assert.AreEqual(expectedTotalScanCount, scanCount, "Total scan count mismatch");
                Console.WriteLine();

                var frameScanPairToIndexMap = oWrapper.GetUimfFrameScanPairToIndexMapping();

                var scanCountMS1 = 0;
                var scanCountMS2 = 0;

                foreach (var frame in frameScanPairToIndexMap)
                {
                    var frameNumber = frame.Key.Key;
                    var scanNumber = frame.Key.Value;
                    var spectrumIndex = frame.Value;

                    if (frameNumber < frameStart || frameNumber > frameEnd)
                    {
                        continue;
                    }

                    var spectrum = oWrapper.GetSpectrum(spectrumIndex, false);

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
        }

        [Test]
        [TestCase("9_Peptide_Mix_16Oct14_Cedar_Infuse.UIMF", 1, 5, 17, 0)]
        [TestCase("9pep_mix_1uM_4bit_50_3Jun16.UIMF", 1, 5, 5, 0)]
        [TestCase("QC_Shew_IMER_500ng_Run-1_4May16_Oak_15-01-16.UIMF", 1, 5, 7, 0)]
        [TestCase("20160524_TuneMix_1574V_neg_001.UIMF", 1, 4, 32, 0)]
        public void TestGetScanInfo(string uimfFileName, int frameStart, int frameEnd, int expectedMS1, int expectedMS2)
        {
            var expectedData = new Dictionary<string, Dictionary<KeyValuePair<int, int>, string>>();

            // Keys in this dictionary are the FrameNum, ScanNum whose metadata is being retrieved
            var file1Data = new Dictionary<KeyValuePair<int, int>, string>
            {
                // Scan MSLevel NumPeaks RetentionTime DriftTimeMsec IonMobilityDriftTime LowMass HighMass TotalIonCurrent BasePeakMZ BasePeakIntensity ParentIonMZ ActivationType IonMode IsCentroided ScanStartTime IonInjectionTime FilterText
                 {new KeyValuePair<int, int>(1, 0), "   1    0 1     0   0.01   0.01     0.00     0.00   0 14384 0.0E+0    0.000 0.0E+0     0.00          positive False  7192.23"},
                 {new KeyValuePair<int, int>(1, 100), "   1  100 1  1317   0.01   0.01    24.68    24.68   0 14384 0.0E+0    0.000 0.0E+0     0.00          positive False  7192.23"},
                 {new KeyValuePair<int, int>(1, 200), "   1  200 1     3   0.01   0.01    49.36    49.36   0 14384 0.0E+0    0.000 0.0E+0     0.00          positive False  7192.23"},
                 {new KeyValuePair<int, int>(2, 0), "   2    0 1     0   0.25   0.25     0.00     0.00   0 14384 0.0E+0    0.000 0.0E+0     0.00          positive False  7192.23"},
                 {new KeyValuePair<int, int>(2, 1), "   2    1 1     6   0.25   0.25     0.25     0.25   0 14384 0.0E+0    0.000 0.0E+0     0.00          positive False  7192.23"},
                 {new KeyValuePair<int, int>(2, 100), "   2  100 1  3429   0.25   0.25    24.70    24.70   0 14384 0.0E+0    0.000 0.0E+0     0.00          positive False  7192.23"},
                 {new KeyValuePair<int, int>(2, 200), "   2  200 1     9   0.25   0.25    49.39    49.39   0 14384 0.0E+0    0.000 0.0E+0     0.00          positive False  7192.23"},
                 {new KeyValuePair<int, int>(3, 0), "   3    0 1     0   0.50   0.50     0.00     0.00   0 14384 0.0E+0    0.000 0.0E+0     0.00          positive False  7192.23"},
                 {new KeyValuePair<int, int>(3, 100), "   3  100 1  4216   0.50   0.50    24.65    24.65   0 14384 0.0E+0    0.000 0.0E+0     0.00          positive False  7192.23"},
                 {new KeyValuePair<int, int>(3, 200), "   3  200 1     3   0.50   0.50    49.31    49.31   0 14384 0.0E+0    0.000 0.0E+0     0.00          positive False  7192.23"},
                 {new KeyValuePair<int, int>(4, 0), "   4    0 1     0   0.75   0.75     0.00     0.00   0 14384 0.0E+0    0.000 0.0E+0     0.00          positive False  7192.23"},
                 {new KeyValuePair<int, int>(4, 1), "   4    1 1     6   0.75   0.75     0.25     0.25   0 14384 0.0E+0    0.000 0.0E+0     0.00          positive False  7192.23"},
                 {new KeyValuePair<int, int>(4, 100), "   4  100 1  5640   0.75   0.75    24.58    24.58   0 14384 0.0E+0    0.000 0.0E+0     0.00          positive False  7192.23"},
                 {new KeyValuePair<int, int>(4, 200), "   4  200 1     6   0.75   0.75    49.16    49.16   0 14384 0.0E+0    0.000 0.0E+0     0.00          positive False  7192.23"},
                 {new KeyValuePair<int, int>(5, 0), "   5    0 1     0   1.00   1.00     0.00     0.00   0 14384 0.0E+0    0.000 0.0E+0     0.00          positive False  7192.23"},
                 {new KeyValuePair<int, int>(5, 100), "   5  100 1  4605   1.00   1.00    24.70    24.70   0 14384 0.0E+0    0.000 0.0E+0     0.00          positive False  7192.23"},
                 {new KeyValuePair<int, int>(5, 200), "   5  200 1     3   1.00   1.00    49.39    49.39   0 14384 0.0E+0    0.000 0.0E+0     0.00          positive False  7192.23"}
            };
            expectedData.Add("9_Peptide_Mix_16Oct14_Cedar_Infuse", file1Data);

            var file2Data = new Dictionary<KeyValuePair<int, int>, string>
            {
                 {new KeyValuePair<int, int>(1, 38), "   1   38 1    15   0.01   0.01     6.11     6.11   0  2646 0.0E+0    0.000 0.0E+0     0.00          positive False  1322.76"},
                 {new KeyValuePair<int, int>(1, 114), "   1  114 1  1215   0.01   0.01    18.34    18.34   0  2646 0.0E+0    0.000 0.0E+0     0.00          positive False  1322.76"},
                 {new KeyValuePair<int, int>(1, 152), "   1  152 1   222   0.01   0.01    24.45    24.45   0  2646 0.0E+0    0.000 0.0E+0     0.00          positive False  1322.76"},
                 {new KeyValuePair<int, int>(1, 228), "   1  228 1     3   0.01   0.01    36.67    36.67   0  2646 0.0E+0    0.000 0.0E+0     0.00          positive False  1322.76"},
                 {new KeyValuePair<int, int>(1, 304), "   1  304 1     9   0.01   0.01    48.89    48.89   0  2646 0.0E+0    0.000 0.0E+0     0.00          positive False  1322.76"}
            };
            expectedData.Add("9pep_mix_1uM_4bit_50_3Jun16", file2Data);

            var file3Data = new Dictionary<KeyValuePair<int, int>, string>
            {
                 {new KeyValuePair<int, int>(1, 100), "   1  100 1   504   0.01   0.01    16.37    16.37   0  2646 0.0E+0    0.000 0.0E+0     0.00          positive False  1322.79"},
                 {new KeyValuePair<int, int>(2, 100), "   2  100 1   494   0.06   0.06    16.42    16.42   0  2646 0.0E+0    0.000 0.0E+0     0.00          positive False  1322.79"},
                 {new KeyValuePair<int, int>(3, 100), "   3  100 1  1109   0.11   0.11    16.43    16.43   0  2646 0.0E+0    0.000 0.0E+0     0.00          positive False  1322.79"},
                 {new KeyValuePair<int, int>(4, 0), "   4    0 1     0   0.17   0.17     0.00     0.00   0  2646 0.0E+0    0.000 0.0E+0     0.00          positive False  1322.79"},
                 {new KeyValuePair<int, int>(4, 100), "   4  100 1  1061   0.17   0.17    16.41    16.41   0  2646 0.0E+0    0.000 0.0E+0     0.00          positive False  1322.79"},
                 {new KeyValuePair<int, int>(5, 0), "   5    0 1     0   0.22   0.22     0.00     0.00   0  2646 0.0E+0    0.000 0.0E+0     0.00          positive False  1322.79"},
                 {new KeyValuePair<int, int>(5, 100), "   5  100 1  1087   0.22   0.22    16.44    16.44   0  2646 0.0E+0    0.000 0.0E+0     0.00          positive False  1322.79"},
            };
            expectedData.Add("QC_Shew_IMER_500ng_Run-1_4May16_Oak_15-01-16", file3Data);

            var file4Data = new Dictionary<KeyValuePair<int, int>, string>
            {
                 {new KeyValuePair<int, int>(1, 1), "   1    1 1   344   0.00   0.00     0.12     0.12   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(1, 100), "   1  100 1   345   0.00   0.00    12.10    12.10   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(1, 200), "   1  200 1   577   0.00   0.00    24.20    24.20   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(1, 300), "   1  300 1   996   0.00   0.00    36.30    36.30   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(1, 400), "   1  400 1   469   0.00   0.00    48.40    48.40   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(1, 500), "   1  500 1   336   0.00   0.00    60.50    60.50   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(1, 600), "   1  600 1   369   0.00   0.00    72.60    72.60   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(1, 700), "   1  700 1   423   0.00   0.00    84.70    84.70   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(2, 1), "   2    1 1   381   0.00   0.00     0.12     0.12   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(2, 100), "   2  100 1   243   0.00   0.00    12.10    12.10   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(2, 200), "   2  200 1   669   0.00   0.00    24.20    24.20   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(2, 300), "   2  300 1   770   0.00   0.00    36.30    36.30   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(2, 400), "   2  400 1   372   0.00   0.00    48.40    48.40   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(2, 500), "   2  500 1   355   0.00   0.00    60.50    60.50   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(2, 600), "   2  600 1   441   0.00   0.00    72.60    72.60   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(2, 700), "   2  700 1   328   0.00   0.00    84.70    84.70   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(3, 1), "   3    1 1   405   0.00   0.00     0.12     0.12   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(3, 100), "   3  100 1   419   0.00   0.00    12.10    12.10   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(3, 200), "   3  200 1   705   0.00   0.00    24.20    24.20   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(3, 300), "   3  300 1  1080   0.00   0.00    36.30    36.30   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(3, 400), "   3  400 1   387   0.00   0.00    48.40    48.40   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(3, 500), "   3  500 1   423   0.00   0.00    60.50    60.50   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(3, 600), "   3  600 1   402   0.00   0.00    72.60    72.60   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(3, 700), "   3  700 1   333   0.00   0.00    84.70    84.70   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(4, 1), "   4    1 1   348   0.00   0.00     0.12     0.12   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(4, 100), "   4  100 1   343   0.00   0.00    12.10    12.10   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(4, 200), "   4  200 1   625   0.00   0.00    24.20    24.20   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(4, 300), "   4  300 1  1125   0.00   0.00    36.29    36.29   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(4, 400), "   4  400 1   384   0.00   0.00    48.39    48.39   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(4, 500), "   4  500 1   399   0.00   0.00    60.49    60.49   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(4, 600), "   4  600 1   362   0.00   0.00    72.59    72.59   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"},
                 {new KeyValuePair<int, int>(4, 700), "   4  700 1   360   0.00   0.00    84.69    84.69   0  1700 0.0E+0    0.000 0.0E+0     0.00          positive False   849.87"}
            };
            expectedData.Add("20160524_TuneMix_1574V_neg_001", file4Data);


            var dataFile = GetUimfDataFile(uimfFileName);

            using (var oWrapper = new MSDataFileReader(dataFile.FullName))
            {
                Console.WriteLine("Scan info for {0}", dataFile.Name);
                Console.WriteLine("{0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11} {12} {13} {14} {15} {16} {17}",
                                  "Frame", "Scan", "MSLevel",
                                  "NumPeaks", "RetentionTime",
                                  "ScanStartTime",
                                  "DriftTimeMsec",
                                  "IonMobilityDriftTime",
                                  "LowMass", "HighMass", "TotalIonCurrent",
                                  "BasePeakMZ", "BasePeakIntensity",
                                  "ParentIonMZ", "ActivationType",
                                  "IonMode", "IsCentroided",
                                  "IsolationMZ");

                var frameScanPairToIndexMap = oWrapper.GetUimfFrameScanPairToIndexMapping();

                var scanCountMS1 = 0;
                var scanCountMS2 = 0;

                foreach (var frame in frameScanPairToIndexMap)
                {
                    var frameNumber = frame.Key.Key;
                    var scanNumber = frame.Key.Value;
                    var spectrumIndex = frame.Value;

                    if (frameNumber < frameStart || frameNumber > frameEnd)
                    {
                        continue;
                    }

                    if (string.Equals(uimfFileName, "9pep_mix_1uM_4bit_50_3Jun16.uimf", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (!(scanNumber == 1 || scanNumber % 38 == 0))
                            continue;
                    }
                    else
                    {
                        if (!(scanNumber == 1 || scanNumber % 100 == 0))
                            continue;
                    }

                    var spectrum = oWrapper.GetSpectrum(spectrumIndex, true);
                    var spectrumParams = oWrapper.GetSpectrumCVParamData(spectrumIndex);
                    var cvScanInfo = oWrapper.GetSpectrumScanInfo(spectrumIndex);

                    Assert.IsTrue(spectrum != null, "GetSpectrum returned a null object for frame {0}, scan {1} ", frameNumber, scanNumber);

                    var totalIonCurrent = cvParamUtilities.GetCvParamValueDbl(spectrumParams, cvParamUtilities.CVIDs.MS_TIC);
                    var basePeakMZ = cvParamUtilities.GetCvParamValueDbl(spectrumParams, cvParamUtilities.CVIDs.MS_base_peak_m_z);
                    var basePeakIntensity = cvParamUtilities.GetCvParamValueDbl(spectrumParams, cvParamUtilities.CVIDs.MS_base_peak_intensity);

                    double isolationMZ = 0;
                    double parentIonMZ = 0;
                    var activationType = string.Empty;

                    if (spectrum.Precursors.Length > 0)
                    {
                        var precursor = spectrum.Precursors[0];

                        isolationMZ = precursor.IsolationMz.GetValueOrDefault();
                        parentIonMZ = precursor.PrecursorMz.GetValueOrDefault();

                        if (precursor.ActivationTypes != null)
                            activationType = string.Join(", ", precursor.ActivationTypes);
                    }

                    GetScanMetadata(cvScanInfo, out var scanStartTime, out var ionMobilityDriftTime, out var lowMass, out var highMass);

                    var retentionTime = cvParamUtilities.CheckNull(spectrum.RetentionTime);

                    var numPeaks = spectrum.Mzs.Length;
                    var ionMode = spectrum.NegativeCharge ? "negative" : "positive";

                    var scanSummary =
                        string.Format(
                            "{0,4} {1,4} {2} {3,5} {4,6} {5,6} {6,8} {7,8} {8,3} {9,5} {10,6} {11,8} {12,6} {13,8} {14,-8} {15} {16,-5} {17,8}",
                            frameNumber, scanNumber, spectrum.Level,
                            numPeaks, retentionTime.ToString("0.00"),
                            scanStartTime.ToString("0.00"),
                            cvParamUtilities.CheckNull(spectrum.DriftTimeMsec).ToString("0.00"),
                            ionMobilityDriftTime.ToString("0.00"),
                            lowMass.ToString("0"), highMass.ToString("0"),
                            totalIonCurrent.ToString("0.0E+0"), basePeakMZ.ToString("0.000"),
                            basePeakIntensity.ToString("0.0E+0"), parentIonMZ.ToString("0.00"),
                            activationType,
                            ionMode, spectrum.Centroided,
                            isolationMZ.ToString("0.00"));

                    Console.WriteLine(scanSummary);

                    if (spectrum.Level > 1)
                        scanCountMS2++;
                    else
                        scanCountMS1++;

                    if (!expectedData.TryGetValue(Path.GetFileNameWithoutExtension(dataFile.Name), out var expectedDataThisFile))
                    {
                        Assert.Fail("Dataset {0} not found in dictionary expectedData", dataFile.Name);
                    }

                    if (expectedDataThisFile.TryGetValue(new KeyValuePair<int, int>(frameNumber, scanNumber), out var expectedScanSummary))
                    {
                        Assert.AreEqual(expectedScanSummary, scanSummary,
                                        "Scan summary mismatch, scan " + scanNumber);
                    }

                    var expectedNativeId = string.Format("frame={0} scan={1} frameType=1", frameNumber, scanNumber);
                    Assert.AreEqual(spectrum.NativeId, expectedNativeId, "NativeId is not in the expected format for frame {0}, scan {1} ", frameNumber, scanNumber);
                }

                Console.WriteLine("scanCountMS1={0}", scanCountMS1);
                Console.WriteLine("scanCountMS2={0}", scanCountMS2);

                Assert.AreEqual(expectedMS1, scanCountMS1, "MS1 scan count mismatch");
                Assert.AreEqual(expectedMS2, scanCountMS2, "MS2 scan count mismatch");
            }

        }


        [Test]
        [TestCase("9_Peptide_Mix_16Oct14_Cedar_Infuse.UIMF", 21, 23)]
        [TestCase("QC_Shew_IMER_500ng_Run-1_4May16_Oak_15-01-16.UIMF", 50, 55)]
        public void TestGetScanData(string uimfFileName, int frameStart, int frameEnd)
        {
            var expectedData = new Dictionary<string, Dictionary<KeyValuePair<int, int>, string>>();

            // Keys in this dictionary are the FrameNum, ScanNum whose metadata is being retrieved
            var file1Data = new Dictionary<KeyValuePair<int, int>, string>
            {
                {new KeyValuePair<int, int>(21, 1), "  21    1 171      171      73.143   0        4313.446 6"},
                {new KeyValuePair<int, int>(21, 45), "  21   45 132      132      49.420   0        2871.914 0"},
                {new KeyValuePair<int, int>(21, 90), "  21   90 58061    58061    33.139   0        892.171  0"},
                {new KeyValuePair<int, int>(21, 135), "  21  135 1332     1332     59.655   0        1455.314 0"},
                {new KeyValuePair<int, int>(21, 180), "  21  180 213      213      35.827   0        3477.878 16"},
                {new KeyValuePair<int, int>(21, 225), "  21  225 198      198      34.311   0        4724.490 0"},
                {new KeyValuePair<int, int>(21, 270), "  21  270 156      156      137.671  0        4960.353 0"},
                {new KeyValuePair<int, int>(22, 1), "  22    1 171      171      33.352   0        2700.500 2"},
                {new KeyValuePair<int, int>(22, 45), "  22   45 123      123      60.048   0        2958.660 2"},
                {new KeyValuePair<int, int>(22, 90), "  22   90 57811    57811    33.519   0        893.895  0"},
                {new KeyValuePair<int, int>(22, 135), "  22  135 1558     1558     33.800   0        1442.189 141"},
                {new KeyValuePair<int, int>(22, 180), "  22  180 165      165      104.876  0        2716.046 4"},
                {new KeyValuePair<int, int>(22, 225), "  22  225 165      165      49.372   0        5356.743 8"},
                {new KeyValuePair<int, int>(22, 270), "  22  270 168      168      41.214   0        3317.148 0"},
                {new KeyValuePair<int, int>(23, 1), "  23    1 159      159      44.206   0        2656.092 1"},
                {new KeyValuePair<int, int>(23, 45), "  23   45 179      179      66.354   0        4174.595 0"},
                {new KeyValuePair<int, int>(23, 90), "  23   90 59421    59421    33.319   0        901.566  17"},
                {new KeyValuePair<int, int>(23, 135), "  23  135 1398     1398     36.437   0        1429.296 0"},
                {new KeyValuePair<int, int>(23, 180), "  23  180 228      228      124.690  0        2244.101 0"},
                {new KeyValuePair<int, int>(23, 225), "  23  225 186      186      35.140   0        2326.136 0"},
                {new KeyValuePair<int, int>(23, 270), "  23  270 162      162      134.804  0        4518.062 0"}
            };

            expectedData.Add("9_Peptide_Mix_16Oct14_Cedar_Infuse", file1Data);

            var file2Data = new Dictionary<KeyValuePair<int, int>, string>
            {
                {new KeyValuePair<int, int>(50, 90), "  50   90 304      304      166.939  0        316.855  71"},
                {new KeyValuePair<int, int>(50, 135), "  50  135 36       36       479.959  0        532.950  0"},
                {new KeyValuePair<int, int>(51, 90), "  51   90 516      516      166.939  0        320.853  0"},
                {new KeyValuePair<int, int>(51, 135), "  51  135 27       27       474.824  0        532.934  38"},
                {new KeyValuePair<int, int>(51, 315), "  51  315 3        3        250.922  0        250.922  58"},
                {new KeyValuePair<int, int>(52, 90), "  52   90 579      579      166.939  0        319.857  61"},
                {new KeyValuePair<int, int>(52, 135), "  52  135 30       30       479.959  0        532.934  0"},
                {new KeyValuePair<int, int>(52, 315), "  52  315 3        3        250.922  0        250.922  67"},
                {new KeyValuePair<int, int>(53, 90), "  53   90 625      625      164.932  0        321.351  0"},
                {new KeyValuePair<int, int>(53, 135), "  53  135 39       39       391.282  0        532.934  47"},
                {new KeyValuePair<int, int>(54, 90), "  54   90 456      456      164.941  0        318.876  0"},
                {new KeyValuePair<int, int>(54, 135), "  54  135 33       33       391.282  0        530.978  17"},
                {new KeyValuePair<int, int>(55, 90), "  55   90 467      467      164.932  0        318.888  0"},
                {new KeyValuePair<int, int>(55, 135), "  55  135 24       24       479.959  0        532.950  0"}
            };
            expectedData.Add("QC_Shew_IMER_500ng_Run-1_4May16_Oak_15-01-16", file2Data);

            var dataFile = GetUimfDataFile(uimfFileName);

            using (var oWrapper = new MSDataFileReader(dataFile.FullName))
            {

                Console.WriteLine("Scan data for {0}", dataFile.Name);
                Console.WriteLine("{0,4} {1,4} {2,-8} {3,-8} {4,-8} {5,-8} {6,-8} {7}",
                                    "Frame", "Scan", "MzCount", "IntCount",
                                    "FirstMz", "FirstInt", "MidMz", "MidInt");

                var frameScanPairToIndexMap = oWrapper.GetUimfFrameScanPairToIndexMapping();

                foreach (var frame in frameScanPairToIndexMap)
                {
                    var frameNumber = frame.Key.Key;
                    var scanNumber = frame.Key.Value;
                    var spectrumIndex = frame.Value;

                    if (frameNumber < frameStart || frameNumber > frameEnd)
                    {
                        continue;
                    }

                    if (!(scanNumber == 1 || scanNumber % 45 == 0))
                        continue;

                    var spectrum = oWrapper.GetSpectrum(spectrumIndex, true);

                    var dataPointsRead = spectrum.Mzs.Length;

                    if (dataPointsRead == 0)
                    {
                        Console.WriteLine("Frame {0}, scan {1} has no data; ignoring", frameNumber, scanNumber);
                        continue;
                    }

                    var midPoint = (int)(spectrum.Intensities.Length / 2f);

                    var scanSummary =
                        string.Format(
                            "{0,4} {1,4} {2,-8} {3,-8} {4,-8} {5,-8} {6,-8} {7}",
                            frameNumber, scanNumber,
                            spectrum.Mzs.Length, spectrum.Intensities.Length,
                            spectrum.Mzs[0].ToString("0.000"), spectrum.Intensities[0].ToString("0"),
                            spectrum.Mzs[midPoint].ToString("0.000"), spectrum.Intensities[midPoint].ToString("0"));

                    Console.WriteLine(scanSummary);

                    if (!expectedData.TryGetValue(Path.GetFileNameWithoutExtension(dataFile.Name), out var expectedDataThisFile))
                    {
                        Assert.Fail("Dataset {0} not found in dictionary expectedData", dataFile.Name);
                    }

                    if (expectedDataThisFile.TryGetValue(new KeyValuePair<int, int>(frameNumber, scanNumber), out var expectedDataDetails))
                    {
                        Assert.AreEqual(expectedDataDetails, scanSummary,
                                        "Scan details mismatch, scan " + scanNumber);
                    }

                }
            }

        }

        /// <summary>
        /// Get a FileInfo object for the given .raw file
        /// </summary>
        /// <param name="rawFileName">Thermo raw file name</param>
        /// <returns></returns>
        private FileInfo GetUimfDataFile(string rawFileName)
        {
            FileInfo dataFile;

#pragma warning disable 0162
            if (USE_REMOTE_PATHS)
            {
                dataFile = new FileInfo(Path.Combine(@"\\proto-2\UnitTest_Files\ProteowizardWrapper", rawFileName));
            }
            else
            {
                dataFile = new FileInfo(Path.Combine(@"F:\MSData\UnitTestFiles", rawFileName));
            }
#pragma warning restore 0162

            if (!dataFile.Exists)
            {
                var msg = "File not found: " + dataFile.FullName;
                Console.WriteLine(msg);
                Assert.Fail(msg);
            }

            return dataFile;
        }

        private static void GetScanMetadata(
            SpectrumScanContainer cvScanInfo,
            out double scanStartTime,
            out double ionMobilityDriftTime,
            out double lowMass,
            out double highMass)
        {
            scanStartTime = 0;
            ionMobilityDriftTime = 0;
            lowMass = 0;
            highMass = 0;

            // Lookup details on the scan associated with this spectrum
            // (cvScanInfo.Scans is a list, but .uimf files typically have a single scan for each spectrum)
            foreach (var scanEntry in cvScanInfo.Scans)
            {
                scanStartTime = cvParamUtilities.GetCvParamValueDbl(scanEntry.CVParams, cvParamUtilities.CVIDs.MS_scan_start_time);
                ionMobilityDriftTime= cvParamUtilities.GetCvParamValueDbl(scanEntry.CVParams, cvParamUtilities.CVIDs.MS_ion_mobility_drift_time);

                lowMass = cvParamUtilities.GetCvParamValueDbl(scanEntry.ScanWindowList, cvParamUtilities.CVIDs.MS_scan_window_lower_limit);
                highMass = cvParamUtilities.GetCvParamValueDbl(scanEntry.ScanWindowList, cvParamUtilities.CVIDs.MS_scan_window_upper_limit);

                break;
            }
        }

    }
}
