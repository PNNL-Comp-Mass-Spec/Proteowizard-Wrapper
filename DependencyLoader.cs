using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;

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
        /// already be in the resolver chain before any other use of ProteoWizardWrapper.
        /// Also, <see cref="DependencyLoader.ValidateLoader()"/> should be used to make sure a meaningful error message is thrown if ProteoWizard is not available.</remarks>
        /// <remarks>This must be called before any portion of <see cref="MsDataFileImpl"/> is used. It cannot be called in the same function
        /// as a constructor to that class; it must be called from at least 1 step higher on the call stack, to be in place before the
        /// program attempts to load the DLL</remarks>
        public static void AddAssemblyResolver()
        {
            if (!_resolverAdded)
            {
#if DEBUG
                Console.WriteLine("Adding assembly resolver...");
#endif
                try
                {
                    // Use ReflectionOnlyLoad because it doesn't load dependencies.
                    var asm = Assembly.ReflectionOnlyLoad("pwiz_bindings_cli, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
                    Console.WriteLine("Warning: Program will try to use \"" + asm.Location + "\" instead of searching for ProteoWizard installation.");
                }
                catch
                {
                    // Do nothing; we actually want to hit this, because if we can already resolve pwiz_bindings_cli, that will be used rather than going through this assembly resolver.
                }
                AppDomain.CurrentDomain.AssemblyResolve += ProteoWizardAssemblyResolver;
                _resolverAdded = true;
                ValidateLoader();
            }
        }

        /// <summary>
        /// Remove the Assembly Resolver from the system assembly resolver chain
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public static void RemoveAssemblyResolver()
        {
            if (_resolverAdded)
            {
                AppDomain.CurrentDomain.AssemblyResolve -= ProteoWizardAssemblyResolver;
                _resolverAdded = false;
            }
        }

        private static bool _resolverAdded;

        #region AssemblyResolverHandler for finding ProteoWizard DLLs

        /// <summary>
        /// On a missing DLL event, searches a path specified by FindPwizPath for the ProteoWizard DLLs, and loads them
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private static Assembly ProteoWizardAssemblyResolver(object sender, ResolveEventArgs args)
        {
#if DEBUG
            Console.WriteLine("Looking for: " + args.Name);
            //Console.WriteLine("Wanted by: " + args.RequestingAssembly);
#endif

            if (!args.Name.ToLower().StartsWith("pwiz_bindings_cli"))
            {
                // Check names from other primary assemblies in the ProteoWizard directory
                var found = false;
                var assembly = args.Name.ToLower();
                var firstLetter = assembly[0];

                // Check to see if it is a file that is in the ProteoWizard directory...
                foreach (var file in PwizPathFiles.Where(f => f[0] == firstLetter))
                {
                    if (assembly.StartsWith(file))
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
                ValidateLoaderByPath();
                return null;
            }

            // Retrieve the list of referenced assemblies in an array of AssemblyName.
            var tempAssemblyPath = "";

            var referencedAssemblyNames = Assembly.GetExecutingAssembly().GetReferencedAssemblies();

            // Loop through the array of referenced assembly names.
            foreach (var assemblyName in referencedAssemblyNames)
            {
                //Check for the assembly names that have raised the "AssemblyResolve" event.
                if (assemblyName.FullName.Substring(0, assemblyName.FullName.IndexOf(',')) == args.Name.Substring(0, args.Name.IndexOf(',')))
                {
                    //Console.WriteLine("Attempting to load DLL \"" + Path.Combine(pwizPath, args.Name.Substring(0, args.Name.IndexOf(",")) + ".dll") + "\"");
                    //Build the path of the assembly from where it has to be loaded.
                    tempAssemblyPath = Path.Combine(PwizPath, args.Name.Substring(0, args.Name.IndexOf(',')) + ".dll");
                    break;
                }
            }
#if DEBUG
            Console.WriteLine("Loading file \"" + tempAssemblyPath + "\"");
#endif
            var assemblyFile = new FileInfo(tempAssemblyPath);

            // Load the assembly from the specified path.
            Assembly myAssembly;
            try
            {
                myAssembly = Assembly.LoadFrom(assemblyFile.FullName);
            }
            catch (BadImageFormatException)
            {
                Console.WriteLine("Incompatible Assembly: \"" + assemblyFile.FullName + "\"");
                throw;
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("Assembly not found: \"" + assemblyFile.FullName + "\"");
                throw;
            }
            catch (FileLoadException)
            {
                Console.WriteLine("Invalid Assembly: \"" + assemblyFile.FullName + "\"");
                Console.WriteLine("The assembly may be marked as \"Untrusted\" by Windows. Please unblock and try again.");
                Console.WriteLine("Use the Streams tool (https://technet.microsoft.com/en-us/sysinternals/streams.aspx) to unblock, for example");
                if (assemblyFile.DirectoryName == null)
                    Console.WriteLine("streams -d *");
                else
                    Console.WriteLine("streams -d \"" + Path.Combine(assemblyFile.DirectoryName, "*") + "\"");
                throw;
            }
            catch (SecurityException)
            {
                Console.WriteLine("Assembly access denied: \"" + assemblyFile.FullName + "\"");
                throw;
            }

            //Return the loaded assembly.
            return myAssembly;
        }

        #endregion

        #region Static stateful variable and populating functions

        /// <summary>
        /// Name of the DLL we are checking for
        /// </summary>
        public const string TargetDllName = "pwiz_bindings_cli.dll";

        /// <summary>
        /// The path to the most recent 64-bit ProteoWizard install
        /// If this is not null/empty, we can usually make a safe assumption that the ProteoWizard DLLs are available.
        /// </summary>
        public static readonly string PwizPath;

        private static List<string> PwizPathFiles;

        /// <summary>
        /// Finds the path to the most recent 64-bit ProteoWizard install
        /// PwizPath is populated from this, but only causes a single search.
        /// </summary>
        /// <returns></returns>
        /// <remarks>Paths searched, in order:
        /// "%ProteoWizard%" or "%ProteoWizard%_x86" environment variable data,
        /// "C:\DMS_Programs\ProteoWizard" or "C:\DMS_Programs\ProteoWizard_x86",
        /// "%ProgramFiles%\ProteoWizard\(highest sorted)"</remarks>
        public static string FindPwizPath()
        {
            string pwizPath;

            // Set the DMS_Programs ProteoWizard path based on if the process is 32- or 64-bit.
            string dmsProgramsPwiz;

            if (!Environment.Is64BitProcess)
            {
                // Check for a x86 ProteoWizard environment variable
                pwizPath = Environment.GetEnvironmentVariable("ProteoWizard_x86");

                if (string.IsNullOrEmpty(pwizPath) && !Environment.Is64BitOperatingSystem)
                {
                    pwizPath = Environment.GetEnvironmentVariable("ProteoWizard");
                }

                dmsProgramsPwiz = @"C:\DMS_Programs\ProteoWizard_x86";
            }
            else
            {
                // Check for a x64 ProteoWizard environment variable
                pwizPath = Environment.GetEnvironmentVariable("ProteoWizard");
                dmsProgramsPwiz = @"C:\DMS_Programs\ProteoWizard";
            }

            if (string.IsNullOrWhiteSpace(pwizPath) && Directory.Exists(dmsProgramsPwiz) &&
                new DirectoryInfo(dmsProgramsPwiz).GetFiles(TargetDllName).Length > 0)
            {
                return dmsProgramsPwiz;
            }

            if (!string.IsNullOrWhiteSpace(pwizPath) && Directory.Exists(pwizPath) && new DirectoryInfo(pwizPath).GetFiles(TargetDllName).Length > 0)
            {
                return pwizPath;
            }

            // Look for per-user and per-machine ProteoWizard installs; use whichever install is newer.

            var possibleInstallDirs = new List<DirectoryInfo>();

            // Per-User ProteoWizard install detection
            var localAppDataDir = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Apps"));
            if (localAppDataDir.Exists)
            {
                var bitness = Environment.Is64BitProcess ? 64 : 32;
                possibleInstallDirs.AddRange(localAppDataDir.EnumerateDirectories($"ProteoWizard*{bitness}-bit"));
            }

            // NOTE: This call returns the 32-bit Program Files folder if the running process is 32-bit
            // or the 64-bit Program Files folder if the running process is 64-bit
            var programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
            if (string.IsNullOrWhiteSpace(programFiles))
            {
                return null;
            }

            // Construct a path of the form "C:\Program Files\ProteoWizard" or "C:\Program Files (x86)\ProteoWizard"
            var programFilesPwiz = Path.Combine(programFiles, "ProteoWizard");
            var pwizFolder = new DirectoryInfo(programFilesPwiz);
            if (pwizFolder.Exists)
            {
                if (pwizFolder.GetFiles(TargetDllName).Length > 0)
                {
                    return programFilesPwiz;
                }
            }
            else
            {
                // Update pwizFolder to be "C:\Program Files" or "C:\Program Files (x86)"
                pwizFolder = new DirectoryInfo(programFiles);
                if (!pwizFolder.Exists)
                {
                    return null;
                }
            }

            // Look for subfolders whose names start with ProteoWizard, for example "ProteoWizard 3.0.9490"
            possibleInstallDirs.AddRange(pwizFolder.EnumerateDirectories("ProteoWizard*"));

            if (possibleInstallDirs.Count <= 0)
            {
                return null;
            }

            // Try to sort by version, it properly handles the version rolling over powers of 10 (but string sorting does not)
            var byVersion = new List<Tuple<System.Version, DirectoryInfo>>();
            foreach (var folder in possibleInstallDirs)
            {
                try
                {
                    // Just ignoring the directory here if it has no version
                    var versionString = folder.Name.Trim().Split(' ').Last();
                    if (folder.Name.EndsWith("-bit", StringComparison.OrdinalIgnoreCase))
                    {
                        var split = folder.Name.Trim().Split(' ');
                        versionString = split[split.Length - 2];
                    }

                    if (string.IsNullOrWhiteSpace(versionString) || !versionString.Contains("."))
                    {
                        continue;
                    }

                    var versionSplit = versionString.Split('.');

                    if (System.Version.TryParse(versionString, out var version))
                    {
                        // Old pre-Git SCM conversion install - only has 3 components
                        byVersion.Add(new Tuple<System.Version, DirectoryInfo>(version, folder));
                    }
                    else if (versionSplit.Length > 3 && System.Version.TryParse(string.Join(".", versionSplit.Take(versionSplit.Length - 1)), out var version2))
                    {
                        // Post-Git SCM conversion install - last section of the version is a Git hash, and will not parse
                        byVersion.Add(new Tuple<System.Version, DirectoryInfo>(version2, folder));
                    }
                }
                catch (Exception)
                {
                    // Do nothing...
                }
            }
            if (byVersion.Count > 0)
            {
                // Reverse sort the list
                byVersion.Sort((x, y) => y.Item1.CompareTo(x.Item1));
                var subFoldersOrig = possibleInstallDirs.ToArray();
                possibleInstallDirs = byVersion.Select(x => x.Item2).ToList();
                // Guarantee that any folder where we couldn't parse a version is in the list, but at the end.
                foreach (var folder in subFoldersOrig)
                {
                    if (!possibleInstallDirs.Contains(folder))
                    {
                        possibleInstallDirs.Add(folder);
                    }
                }
            }
            else
            {
                // Sorting by version failed, try the old method.
                // reverse the sort order - this should give us the highest installed version of ProteoWizard first
                possibleInstallDirs.Sort((x, y) => string.Compare(y.FullName, x.FullName, StringComparison.Ordinal));
            }

            foreach (var folder in possibleInstallDirs)
            {
                if (folder.GetFiles(TargetDllName).Length > 0 && File.Exists(Path.Combine(folder.FullName, "pwiz_bindings_cli.dll")))
                {
                    return folder.FullName;
                }
            }
            // If the above failed, return the highest version installed
            return possibleInstallDirs[0].FullName;
        }

        private static void SetPwizPathFiles()
        {
            if (string.IsNullOrWhiteSpace(PwizPath))
            {
                return;
            }

            var allFiles = Directory.GetFiles(PwizPath, "*.dll", SearchOption.AllDirectories);
            PwizPathFiles = new List<string>(allFiles.Length);
            foreach (var file in allFiles)
            {
                var fileNameBase = Path.GetFileNameWithoutExtension(file);
                if (fileNameBase != null)
                {
                    PwizPathFiles.Add(fileNameBase.ToLower());
                }
            }
            PwizPathFiles.Sort();
        }

        /// <summary>
        /// Checks to make sure the path to ProteoWizard files is set. If not, throws an exception.
        /// </summary>
        /// <remarks>This function should generally only be called inside of a conditional statement to prevent the
        /// exception from being thrown when the ProteoWizard DLLs will not be needed.</remarks>
        public static void ValidateLoader()
        {
            if (!_resolverAdded)
            {
                AddAssemblyResolver();
            }

            try
            {
                Assembly.Load("pwiz_bindings_cli, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            }
            catch
            {
                var message = CannotFindExceptionMessage();

                Console.WriteLine(message);
                throw new TypeLoadException(message);
            }
        }

        private static void ValidateLoaderByPath()
        {
            if (string.IsNullOrWhiteSpace(PwizPath))
            {
                var message = CannotFindExceptionMessage();

                Console.WriteLine(message);
                throw new TypeLoadException(message);
            }
        }

        private static string CannotFindExceptionMessage()
        {
            var bits = Environment.Is64BitProcess ? "64" : "32";
            var message = "Cannot load ProteoWizard DLLs. Please ensure that " + bits
                + "-bit ProteoWizard is installed to its default install directory (\""
                + Environment.GetEnvironmentVariable("ProgramFiles") + "\\ProteoWizard\\ProteoWizard 3.0.[x]\")."
                + "\nCurrently trying to load ProteoWizard DLLs from path \"" + PwizPath + "\".";

            return message;
        }

        static DependencyLoader()
        {
            PwizPath = FindPwizPath();

#if DEBUG
            if (!string.IsNullOrEmpty(PwizPath))
            {
                Console.WriteLine("Using ProteoWizard at " + PwizPath);
            }
#endif
            SetPwizPathFiles();
            AddAssemblyResolver();
        }

        #endregion
    }
}
