using NUnit.Framework;

namespace ProteowizardWrapperUnitTests
{
    [SetUpFixture]
    class ProteowizardSetup
    {
        [OneTimeSetUp]
        public void Setup()
        {
            pwiz.ProteowizardWrapper.DependencyLoader.AddAssemblyResolver();
        }
    }
}
