using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProteowizardWrapper_Test_x86;

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
