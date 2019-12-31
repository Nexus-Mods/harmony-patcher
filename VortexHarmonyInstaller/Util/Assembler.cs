using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace VortexHarmonyInstaller.Util
{
    public partial class Constants
    {
        public const string FRAMEWORK_PATH = @"c:\Windows\Microsoft.NET\Framework\";
        public const string ILASM_EXEC = "ilasm.exe";
        public const string ILASM_ARG = "\"{0}\" /dll /output:\"{1}\"";
    }

    internal partial class Exceptions
    {
        internal class AssemblerFailedException: Exception
        {
            internal AssemblerFailedException(string ILFilePath, string errorCode, string output)
                : base($"Generating dll assembly from file {ILFilePath} failed with exit code - {errorCode}. " +
                      $"Log: {output}") { }
        }
        internal class MissingNETAssemblerException : Exception
        {
            internal MissingNETAssemblerException(string version)
                : base($"Unable to find assembler version: {version}") { }
        }
    }

    public class Assembler
    {
        private static string[] m_Assemblers = null;
        private static string[] GetAssemblers()
        {
            if ((null == m_Assemblers) || (m_Assemblers.Length == 0))
                m_Assemblers = Directory.GetFiles(Constants.FRAMEWORK_PATH,
                                                  Constants.ILASM_EXEC,
                                                  SearchOption.AllDirectories);

            return m_Assemblers;
        }

        public static void AssembleFile(string ILFilePath, string outputFilePath, Version version)
        {
            if (!File.Exists(ILFilePath))
                throw new InvalidOperationException(string.Format("The file {0} does not exist!", ILFilePath));

            Regex rgx = new Regex($"v{version.Major}[0-9]*");
            string assemblerFileLocation = GetAssemblers()
                .Where(assembler => rgx.IsMatch(assembler))
                .SingleOrDefault();

            if (assemblerFileLocation == null)
                throw new Exceptions.MissingNETAssemblerException(version.ToString());

            var startInfo = new ProcessStartInfo(assemblerFileLocation, string.Format(Constants.ILASM_ARG, ILFilePath, outputFilePath));
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            using (var process = Process.Start(startInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string status = output + "\n" + error;
                    throw new Exceptions.AssemblerFailedException(ILFilePath, process.ExitCode.ToString(), status);
                }
            }
        }
    }
}
