using System;


namespace ProteowizardWrapper_Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var pwizPath = pwiz.ProteowizardWrapper.DependencyLoader.FindPwizPath();

            Console.WriteLine("DLLs will load from " + pwizPath);

            pwiz.ProteowizardWrapper.DependencyLoader.AddAssemblyResolver();
            TestRaw.TestReadRaw();
            Console.WriteLine("Done");
        }
    }
}
