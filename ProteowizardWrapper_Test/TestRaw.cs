using System;

namespace ProteowizardWrapper_Test
{
    internal static class TestRaw
    {
        public static void TestReadBruker()
        {
            try
            {
                const string dataFilePath = @"\\proto-6\12T_FTICR_B\2014_4\2014_09_30_Stegen_ALK-3_ACN_Core05-org-1_000001\2014_09_30_Stegen_ALK-3_ACN_Core05-org-1_000001.d";

                var reader = new pwiz.ProteowizardWrapper.MSDataFileReader(dataFilePath);

                var isABSciexFile = reader.IsABFile;
                var isThermoFile = reader.IsThermoFile;

                Console.WriteLine();
                Console.WriteLine(System.IO.Path.GetFileName(dataFilePath) + " IsABSciexFile: " + isABSciexFile);
                Console.WriteLine(System.IO.Path.GetFileName(dataFilePath) + " IsThermoFile: " + isThermoFile);

                const int targetIndex = 0;
                var spectrum = reader.GetSpectrum(targetIndex);
                Console.WriteLine("Spectrum at index {0} is scan {1} with {2} points", targetIndex, spectrum.Id, spectrum.Mzs.Length);

                var precursors = reader.GetPrecursors(targetIndex);
                foreach (var item in precursors)
                {
                    Console.WriteLine("  Precursor: {0:F2} m/z", item.IsolationMz);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        public static void TestReadRaw()
        {
            try
            {
                const string dataFilePath = @"..\..\..\UnitTests\Data\Angiotensin_AllScans.raw";

                var reader = new pwiz.ProteowizardWrapper.MSDataFileReader(dataFilePath);

                var isABSciexFile = reader.IsABFile;
                var isThermoFile = reader.IsThermoFile;

                Console.WriteLine();
                Console.WriteLine(System.IO.Path.GetFileName(dataFilePath) + " IsABSciexFile: " + isABSciexFile);
                Console.WriteLine(System.IO.Path.GetFileName(dataFilePath) + " IsThermoFile: " + isThermoFile);

                for (var targetIndex = 0; targetIndex < reader.SpectrumCount; targetIndex *= 2)
                {
                    var spectrum = reader.GetSpectrum(targetIndex);
                    Console.WriteLine("Spectrum at index {0,-4} is scan {1,-45} with {2,4} points", targetIndex, spectrum.Id, spectrum.Mzs.Length);

                    var precursors = reader.GetPrecursors(targetIndex);
                    foreach (var item in precursors)
                    {
                        Console.WriteLine("  Precursor: {0:F2} m/z", item.IsolationMz);
                    }

                    if (targetIndex == 0)
                        targetIndex = 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }
}
