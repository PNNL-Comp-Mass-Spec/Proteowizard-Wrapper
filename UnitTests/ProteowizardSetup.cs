using NUnit.Framework;

namespace ProteowizardWrapperUnitTests
{
    [SetUpFixture]
    internal class ProteowizardSetup
    {
        [OneTimeSetUp]
        public void Setup()
        {
            pwiz.ProteowizardWrapper.DependencyLoader.AddAssemblyResolver();
        }
    }
}
