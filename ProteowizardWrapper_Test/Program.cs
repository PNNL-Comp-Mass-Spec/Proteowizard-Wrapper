using System;


namespace ProteowizardWrapper_Test
{
    class Program
    {
        // Ignore Spelling: uncheck

        static void Main(string[] args)
        {
            // Note: when compiling as AnyCPU, uncheck option "Prefer 32-bit" to assure that ProteoWizard loads from
            //       C:\DMS_Programs\ProteoWizard  or  C:\Program Files\ProteoWizard

            var pwizPath = pwiz.ProteowizardWrapper.DependencyLoader.FindPwizPath();

            Console.WriteLine("DLLs will load from " + pwizPath);

            pwiz.ProteowizardWrapper.DependencyLoader.AddAssemblyResolver();

            Console.WriteLine();
            TestRaw.TestReadRaw();
            Console.WriteLine();

            TestRaw.TestReadBruker();
            Console.WriteLine();

            Console.WriteLine("Done");
            System.Threading.Thread.Sleep(1000);
        }
    }
}

