using System;

namespace ProteowizardWrapper_Test
{
    public class TestRaw
    {
        public static void TestReadRaw()
        {
            try
            {
                var dataFilePath = @"F:\MSData\VOrbitrap\MPMP_SKBR3_Peptidome_02_05Dec13_Pippin_13-07-06.raw";
                dataFilePath = @"\\proto-6\12T_FTICR_B\2014_4\2014_09_30_Stegen_ALK-3_ACN_Core05-org-1_000001\2014_09_30_Stegen_ALK-3_ACN_Core05-org-1_000001.d";

                var oWrapper = new pwiz.ProteowizardWrapper.MSDataFileReader(dataFilePath);


                var isAbFile = oWrapper.IsABFile;
                var isThermo = oWrapper.IsThermoFile;
                
                var oSpectrum = oWrapper.GetSpectrum(0);
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine(System.IO.Path.GetFileName(dataFilePath) + " IsThermoFile: " + isThermo);
                System.Threading.Thread.Sleep(1500);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }
}
