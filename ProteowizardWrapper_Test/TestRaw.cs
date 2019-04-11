using System;

namespace ProteowizardWrapper_Test
{
    public class TestRaw
    {
        public static void TestReadBruker()
        {
            try
            {
                var dataFilePath = @"\\proto-6\12T_FTICR_B\2014_4\2014_09_30_Stegen_ALK-3_ACN_Core05-org-1_000001\2014_09_30_Stegen_ALK-3_ACN_Core05-org-1_000001.d";

                var reader = new pwiz.ProteowizardWrapper.MSDataFileReader(dataFilePath);

                var isABSciexFile = reader.IsABFile;
                var isThermoFile = reader.IsThermoFile;

                Console.WriteLine();
                Console.WriteLine(System.IO.Path.GetFileName(dataFilePath) + " IsABSciexFile: " + isABSciexFile);
                Console.WriteLine(System.IO.Path.GetFileName(dataFilePath) + " IsThermoFile: " + isThermoFile);

                const int targetIndex = 0;
                var spectrum = reader.GetSpectrum(targetIndex);
                Console.WriteLine("Spectrum at index {0} is scan {1} with {2} points", targetIndex, spectrum.NativeId, spectrum.Mzs.Length);
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
                var dataFilePath = @"..\..\Data\Angiotensin_AllScans.raw";

                var reader = new pwiz.ProteowizardWrapper.MSDataFileReader(dataFilePath);

                var isABSciexFile = reader.IsABFile;
                var isThermoFile = reader.IsThermoFile;

                Console.WriteLine();
                Console.WriteLine(System.IO.Path.GetFileName(dataFilePath) + " IsABSciexFile: " + isABSciexFile);
                Console.WriteLine(System.IO.Path.GetFileName(dataFilePath) + " IsThermoFile: " + isThermoFile);

                for (var targetIndex = 1; targetIndex < reader.SpectrumCount; targetIndex *= 2)
                {
                    var spectrum = reader.GetSpectrum(targetIndex);
                    Console.WriteLine("Spectrum at index {0,-4} is scan {1,-45} with {2,4} points", targetIndex, spectrum.NativeId, spectrum.Mzs.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }
}
