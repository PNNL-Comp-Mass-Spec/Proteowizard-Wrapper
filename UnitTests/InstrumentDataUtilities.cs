using System;
using System.IO;
using PRISM;

namespace ProteowizardWrapperUnitTests
{
    class InstrumentDataUtilities
    {
        private const string UNIT_TEST_SHARE_PATH = @"\\proto-2\UnitTest_Files\ProteowizardWrapper";

        /// <summary>
        /// Look for the file in a directory named Data, in a parent directory to the work directory
        /// If not found, look on Proto-2
        /// </summary>
        /// <param name="fileOrDirectoryToFind"></param>
        /// <param name="isDirectory">True if looking for a directory, false if a file</param>
        /// <param name="instrumentDataFileOrDirectory">Output: matching instrument data file, or null if not found</param>
        /// <returns></returns>
        public static bool FindInstrumentData(
            string fileOrDirectoryToFind,
            bool isDirectory,
            out FileSystemInfo instrumentDataFileOrDirectory)
        {
            return FindInstrumentData(fileOrDirectoryToFind, isDirectory, UNIT_TEST_SHARE_PATH, out instrumentDataFileOrDirectory);
        }

        /// <summary>
        /// Look for the file in a directory named Data, in a parent directory to the work directory
        /// If not found, look in remotePathToSearch
        /// </summary>
        /// <param name="fileOrDirectoryToFind"></param>
        /// <param name="isDirectory">True if looking for a directory, false if a file</param>
        /// <param name="remotePathToSearch">Remote directory to check if fileOrDirectoryToFind is not found locally</param>
        /// <param name="instrumentDataFileOrDirectory">Output: matching instrument data file, or null if not found</param>
        /// <returns></returns>
        public static bool FindInstrumentData(
            string fileOrDirectoryToFind,
            bool isDirectory,
            string remotePathToSearch,
            out FileSystemInfo instrumentDataFileOrDirectory)
        {
            string datasetType;
            if (isDirectory)
                datasetType = "directory";
            else
                datasetType = "file";

            try
            {
                var startingDirectory = new DirectoryInfo(".");
                var directoryToCheck = new DirectoryInfo(".");

                while (true)
                {
                    var matchingDirectories = directoryToCheck.GetDirectories("Data");
                    if (matchingDirectories.Length > 0)
                    {
                        if (FindInstrumentData(fileOrDirectoryToFind, isDirectory, matchingDirectories[0], out instrumentDataFileOrDirectory))
                            return true;

                        break;
                    }

                    if (directoryToCheck.Parent == null)
                    {
                        break;
                    }

                    directoryToCheck = directoryToCheck.Parent;
                }

                // Look in the unit test share
                var remoteShare = new DirectoryInfo(remotePathToSearch);
                if (remoteShare.Exists)
                {
                    if (FindInstrumentData(fileOrDirectoryToFind, isDirectory, remoteShare, out instrumentDataFileOrDirectory))
                    {
                        return true;
                    }
                }

                ConsoleMsgUtils.ShowWarning("Could not find {0} {1} in a Data directory above {2}", datasetType, fileOrDirectoryToFind, startingDirectory.FullName);

                instrumentDataFileOrDirectory = null;
                return false;
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError(ex, "Error looking for {0} {1}", datasetType, fileOrDirectoryToFind);

                instrumentDataFileOrDirectory = null;
                return false;
            }
        }

        /// <summary>
        /// Look for the file in a directory named Data, in a parent directory to the work directory
        /// If not found, look on Proto-2
        /// </summary>
        /// <param name="fileOrDirectoryToFind"></param>
        /// <param name="isDirectory">True if looking for a directory, false if a file</param>
        /// <param name="directoryToCheck">Directory to search in</param>
        /// <param name="instrumentDataFileOrDirectory">Output: matching instrument data file, or null if not found</param>
        /// <returns></returns>
        private static bool FindInstrumentData(
            string fileOrDirectoryToFind,
            bool isDirectory,
            DirectoryInfo directoryToCheck,
            out FileSystemInfo instrumentDataFileOrDirectory)
        {

            if (isDirectory)
            {
                var matchingDatasetDirectories = directoryToCheck.GetDirectories(fileOrDirectoryToFind);
                if (matchingDatasetDirectories.Length > 0)
                {
                    instrumentDataFileOrDirectory = matchingDatasetDirectories[0];
                    return true;
                }
            }
            else
            {
                var matchingDatasetFiles = directoryToCheck.GetFiles(fileOrDirectoryToFind);
                if (matchingDatasetFiles.Length > 0)
                {
                    instrumentDataFileOrDirectory = matchingDatasetFiles[0];
                    return true;
                }
            }

            instrumentDataFileOrDirectory = null;
            return false;
        }
    }
}
