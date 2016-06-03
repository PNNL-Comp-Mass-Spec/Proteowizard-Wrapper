using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace ProteowizardWrapperUnitTests
{
    [TestFixture]
    public class ThermoScanDataTests
    {
        private const bool USE_REMOTE_PATHS = true;

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


        private FileInfo GetRawDataFile(string rawFileName)
        {
            FileInfo dataFile;

            if (USE_REMOTE_PATHS)
            {
                dataFile = new FileInfo(Path.Combine(@"\\proto-2\UnitTest_Files\ThermoRawFileReader", rawFileName));
            }
            else
            {
                dataFile = new FileInfo(Path.Combine(@"..\..\..\Test_ThermoRawFileReader\bin", rawFileName));
            }


            if (!dataFile.Exists)
            {
                Assert.Fail("File not found: " + dataFile.FullName);
            }

            var pwizPath = pwiz.ProteowizardWrapper.DependencyLoader.FindPwizPath();

            Console.WriteLine("DLLs will load from " + pwizPath);

            pwiz.ProteowizardWrapper.DependencyLoader.AddAssemblyResolver();

            return dataFile;
        }
    }
}
