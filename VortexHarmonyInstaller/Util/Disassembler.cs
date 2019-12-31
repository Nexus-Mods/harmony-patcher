using Mono.Cecil;

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace VortexHarmonyInstaller.Util
{
    public partial class Constants
    {
        public const string ILDASM_EXEC = "ildasm.exe";
    }

    public class Disassembler
    {
        private static Assembly CurrentAssembly() {
            return MethodBase.GetCurrentMethod().DeclaringType.Assembly;
        }

        private static string ExecutingAssemblyPath() {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        private static string[] Resources() {
            return CurrentAssembly().GetManifestResourceNames();
        }

        private const string m_strIldasmArguments = "/all /text \"{0}\"";

        public static string ILDasmFileLocation
        {
            get
            {
                return Path.Combine(ExecutingAssemblyPath(), Constants.ILDASM_EXEC);
            }
        }

        static Disassembler()
        {
            //extract the ildasm file to the executing assembly location
            ExtractFileToLocation(Constants.ILDASM_EXEC, ILDasmFileLocation);
        }

        /// <summary>
        /// Saves the file from embedded resource to a given location.
        /// </summary>
        /// <param name="embeddedResourceName">Name of the embedded resource.</param>
        /// <param name="fileName">Name of the file.</param>
        protected static void SaveFileFromEmbeddedResource(string embeddedResourceName, string fileName)
        {
            if (File.Exists(fileName))
            {
                //the file already exists, we can add deletion here if we want to change the version of the 7zip
                return;
            }
            FileInfo fileInfoOutputFile = new FileInfo(fileName);

            using (FileStream streamToOutputFile = fileInfoOutputFile.OpenWrite())
            using (Stream streamToResourceFile = CurrentAssembly().GetManifestResourceStream(embeddedResourceName))
            {
                const int size = 4096;
                byte[] bytes = new byte[4096];
                int numBytes;
                while ((numBytes = streamToResourceFile.Read(bytes, 0, size)) > 0)
                {
                    streamToOutputFile.Write(bytes, 0, numBytes);
                }

                streamToOutputFile.Close();
                streamToResourceFile.Close();
            }
        }

        /// <summary>
        /// Searches the embedded resource and extracts it to the given location.
        /// </summary>
        /// <param name="fileNameInDll">The file name in DLL.</param>
        /// <param name="outFileName">Name of the out file.</param>
        protected static void ExtractFileToLocation(string fileNameInDll, string outFileName)
        {
            string strOutFilePath = (!Path.HasExtension(outFileName))
                ? Path.Combine(outFileName, fileNameInDll)
                : outFileName;

            string resourcePath = Resources().Where(resource => resource.EndsWith(fileNameInDll, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
            if (resourcePath == null)
            {
                throw new Exception(string.Format("Cannot find {0} in the embedded resources of {1}", fileNameInDll, CurrentAssembly().FullName));
            }
            SaveFileFromEmbeddedResource(resourcePath, strOutFilePath);
        }

        public static string GetDisassembledFile(string assemblyFilePath)
        {
            if (!File.Exists(assemblyFilePath))
            {
                throw new FileNotFoundException(assemblyFilePath);
            }

            string tempFileName = Path.GetTempFileName();
            var startInfo = new ProcessStartInfo(ILDasmFileLocation, string.Format(m_strIldasmArguments, assemblyFilePath));
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;

            using (var process = System.Diagnostics.Process.Start(startInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode > 0)
                {
                    throw new InvalidOperationException(
                        string.Format("Generating IL code for file {0} failed with exit code - {1}. Log: {2}",
                        assemblyFilePath, process.ExitCode, output));
                }

                File.WriteAllText(tempFileName, output);
            }

            return tempFileName;
        }

        public static string DisassembleFile(string assemblyFilePath, bool extractResources = false)
        {
            if (extractResources)
            {
                // Will extract any embedded resource files we can find.
                //  We don't care for decryption or any sort of file manipulation,
                //  simply dumping the files next to the assembly will do.
                AssemblyDefinition assDef = AssemblyDefinition.ReadAssembly(assemblyFilePath);
                if (assDef.MainModule.HasResources)
                {
                    EmbeddedResource[] resources = assDef.MainModule.Resources
                        .Where(res => res.ResourceType == ResourceType.Embedded)
                        .Select(res => res as EmbeddedResource)
                        .ToArray();

                    foreach (EmbeddedResource res in resources)
                    {
                        File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(assemblyFilePath),
                            res.Name), res.GetResourceData());
                    }
                }

                // Let it go! let it go!
                assDef.Dispose();
            }

            string disassembledFile = GetDisassembledFile(assemblyFilePath);
            string disassembledIL = File.ReadAllText(disassembledFile);
            if (File.Exists(disassembledFile))
            {
                File.Delete(disassembledFile);
            }

            return disassembledIL;
        }
    }
}
