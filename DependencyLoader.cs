using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;

namespace pwiz.ProteowizardWrapper
{
    /// <summary>
    /// ProteoWizard Dependency Loader
    /// </summary>
    /// <remarks>This class provides a custom AssemblyResolver to find an installation of ProteoWizard, specified in ProteoWizardReaderImplementation.
    /// This class is a wrapper around ProteoWizardReaderImplementation to encapsulate the usage of the custom AssemblyResolver, which must be 
    /// added to the AppDomain.CurrentDomain.AssemblyResolve event before the class is instantiated.</remarks>
    public static class DependencyLoader
    {
        /// <summary>
        /// Add the Assembly Resolver to the system assembly resolver chain
        /// </summary>
        /// <remarks>This should be called early in the program, so that the ProteoWizard Assembly Resolver will 
        /// already be in the resolver chain before any other use of ProteoWizardWrapper</remarks>
        public static void AddAssemblyResolver()
        {
            if (!_resolverAdded)
            {
#if DEBUG
                Console.WriteLine("Adding assembly resolver...");
#endif
                AppDomain.CurrentDomain.AssemblyResolve += DependencyLoader.ProteoWizardAssemblyResolver;
                _resolverAdded = true;
            }
        }

        private static bool _resolverAdded = false;
        
        #region AssemblyResolverHandler for finding ProteoWizard dlls

        /// <summary>
        /// On a missing DLL event, searches a path specified by FindPwizPath for the ProteoWizard dlls, and loads them
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static Assembly ProteoWizardAssemblyResolver(object sender, ResolveEventArgs args)
        {
#if DEBUG
            Console.WriteLine("Looking for: " + args.Name);
            //Console.WriteLine("Wanted by: " + args.RequestingAssembly);
#endif
            // TODO: Add names from other primary assemblies in the ProteoWizard directory
            if (!args.Name.ToLower().StartsWith("pwiz_bindings_cli"))
            {
                var found = false;
                var assm = args.Name.ToLower();
                var firstLetter = assm[0];
                // Check to see if it is a file that is in the ProteoWizard directory...
                foreach (var file in PwizPathFiles.Where(f => f[0] == firstLetter))
                {
                    if (assm.StartsWith(file))
                    {
                        found = true;
                        break;
                    }
                }
                
                if (!found)
                {
                    return Assembly.LoadFrom(""); // We are not interested in searching for anything else - resolving pwiz_bindings_cli provides the hint for all of its dependencies.
                    // This will actually trigger an exception, which is handled in the system code, and the dll search goes on down the chain.
                    // returning null results in this code being called multiple times, for the same dependency.
                }
            }
            Console.WriteLine("Searching for ProteoWizard files...");

            // https://support.microsoft.com/en-us/kb/837908
            //This handler is called only when the common language runtime tries to bind to the assembly and fails.
            if (string.IsNullOrWhiteSpace(PwizPath))
            {
                return null;
            }

            //Retrieve the list of referenced assemblies in an array of AssemblyName.
            string strTempAssmbPath = "";

            AssemblyName[] arrReferencedAssmbNames = Assembly.GetExecutingAssembly().GetReferencedAssemblies();

            //Loop through the array of referenced assembly names.
            foreach (AssemblyName strAssmbName in arrReferencedAssmbNames)
            {
                //Check for the assembly names that have raised the "AssemblyResolve" event.
                if (strAssmbName.FullName.Substring(0, strAssmbName.FullName.IndexOf(",")) == args.Name.Substring(0, args.Name.IndexOf(",")))
                {
                    //Console.WriteLine("Attempting to load DLL \"" + Path.Combine(pwizPath, args.Name.Substring(0, args.Name.IndexOf(",")) + ".dll") + "\"");
                    //Build the path of the assembly from where it has to be loaded.                
                    strTempAssmbPath = Path.Combine(PwizPath, args.Name.Substring(0, args.Name.IndexOf(",")) + ".dll");
                    break;
                }
            }
#if DEBUG
            Console.WriteLine("Loading file \"" + strTempAssmbPath + "\"");
#endif

            //Load the assembly from the specified path.  
            Assembly myAssembly = null;
            try
            {
                myAssembly = Assembly.LoadFrom(strTempAssmbPath);
            }
            catch (BadImageFormatException)
            {
                Console.WriteLine("Incompatible Assembly: \"" + strTempAssmbPath + "\"");
                throw;
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("Assembly not found: \"" + strTempAssmbPath + "\"");
                throw;
            }
            catch (FileLoadException)
            {
                Console.WriteLine("Invalid Assembly: \"" + strTempAssmbPath + "\". The assembly may be marked as \"Untrusted\" by Windows. Please unblock and try again.");
                throw;
            }
            catch (SecurityException)
            {
                Console.WriteLine("Assembly access denied: \"" + strTempAssmbPath + "\"");
                throw;
            }

            //Return the loaded assembly.
            return myAssembly;
        }

        #endregion

        #region Static stateful variable and populating functions

        /// <summary>
        /// The path to the most recent 64-bit ProteoWizard install
        /// If this is not null/empty, we can usually make a safe assumption that the ProteoWizard dlls are available.
        /// </summary>
        public static readonly string PwizPath;

        private static List<string> PwizPathFiles;

        /// <summary>
        /// Finds the path to the most recent 64-bit ProteoWizard install
        /// PwizPath is populated from this, but only causes a single search.
        /// </summary>
        /// <returns></returns>
        /// <remarks>Paths searched, in order: 
        /// "%ProteoWizard%"/"%ProteoWizard%_x86" environment variable data, 
        /// "C:\DMS_Programs\ProteoWizard"/"C:\DMS_Programs\ProteoWizard_x86", 
        /// "%ProgramFiles%\ProteoWizard\(highest sorted)"</remarks>
        public static string FindPwizPath()
        {
            string pwizPath = string.Empty;

            // Set the DMS_Programs ProteoWizard path based on if the process is 32- or 64-bit.
            var dmsProgPwiz = @"C:\DMS_Programs\ProteoWizard";
            if (!Environment.Is64BitProcess)
            {
                // Check for a x86 ProteoWizard environment variable
                pwizPath = Environment.GetEnvironmentVariable("ProteoWizard_x86");
                dmsProgPwiz = @"C:\DMS_Programs\ProteoWizard_x86";
            }

            // Check for a x64 ProteoWizard environment variable
            pwizPath = Environment.GetEnvironmentVariable("ProteoWizard");

            if (string.IsNullOrWhiteSpace(pwizPath) && Directory.Exists(dmsProgPwiz))
            {
                pwizPath = dmsProgPwiz;
            }
            if (string.IsNullOrWhiteSpace(pwizPath))
            {
                // NOTE: Should automatically function as-is to get 32-bit ProteoWizard for 32-bit process and 64-bit ProteoWizard for 64-bit process...
                var progFiles = Environment.GetEnvironmentVariable("ProgramFiles");
                if (string.IsNullOrWhiteSpace(progFiles))
                {
                    return null;
                }
                var progPwiz = Path.Combine(progFiles, "ProteoWizard");
                if (!Directory.Exists(progPwiz))
                {
                    return null;
                }
                var posPaths = Directory.GetDirectories(progPwiz, "ProteoWizard *");
                pwizPath = posPaths.Max(); // Try to get the "newest" folder
            }
            return pwizPath;
        }

        private static void SetPwizPathFiles()
        {
            var allFiles = Directory.GetFiles(PwizPath, "*.dll", SearchOption.AllDirectories);
            PwizPathFiles = new List<string>(allFiles.Length);
            foreach (var file in allFiles)
            {
                PwizPathFiles.Add(Path.GetFileNameWithoutExtension(file).ToLower());
            }
            PwizPathFiles.Sort();
        }

        static DependencyLoader()
        {
            PwizPath = FindPwizPath();
            SetPwizPathFiles();
        }

        #endregion
    }
}
