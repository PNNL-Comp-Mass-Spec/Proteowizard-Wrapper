using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProteowizardWrapper_Test
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var dataFilePath = @"F:\MSData\VOrbitrap\MPMP_SKBR3_Peptidome_02_05Dec13_Pippin_13-07-06.raw";

                // Note: the 64-bit version of the ProteoWizardReader fails for this file
                //       the 32-bit version works
                // Oddly, 64-bit MSConvert can successfully read this file to create a .mzXML file
                // dataFilePath = @"\\proto-6\12T_FTICR_B\2014_4\2014_09_30_Stegen_ALK-3_ACN_Core05-org-1_000001\2014_09_30_Stegen_ALK-3_ACN_Core05-org-1_000001.d";

                var oWrapper = new pwiz.ProteowizardWrapper.MSDataFileReader(dataFilePath);

                var isAbFile = oWrapper.IsABFile;
                var isThermo = oWrapper.IsThermoFile;

                var oSpectrum = oWrapper.GetSpectrum(1);

                Console.WriteLine(isThermo);

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }
}
