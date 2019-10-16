using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;

namespace VortexHarmonyInstaller.Util
{
    public partial class Constants
    {
        public const string ILASM_EXEC = "ilasm.exe";
    }

    public class Assembler
    {
        private const string m_strIlasmArguments = "\"{0}\" /dll /output:\"{1}\"";

        public static string ILAsmFileLocation
        {
            get
            {
                return Path.Combine(@"c:\Windows\Microsoft.NET\Framework\v4.0.30319\", Constants.ILASM_EXEC);
            }
        }

        static Assembler()
        {
            //extract the ildasm file to the executing assembly location
            //ExtractFileToLocation(Constants.ILASM_EXEC, ILAsmFileLocation);
        }

        public static void AssembleFile(string strILFilePath, string strOutputFilePath)
        {
            if (!File.Exists(strILFilePath))
            {
                throw new InvalidOperationException(string.Format("The file {0} does not exist!", strILFilePath));
            }

            var startInfo = new ProcessStartInfo(ILAsmFileLocation, string.Format(m_strIlasmArguments, strILFilePath, strOutputFilePath));
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;

            using (var process = System.Diagnostics.Process.Start(startInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        string.Format("Generating dll assembly from file {0} failed with exit code - {1}. Log: {2}",
                        strILFilePath, process.ExitCode, output));
                }
            }
        }
    }
}
