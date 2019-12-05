using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using ObjectDumper;

namespace VortexHarmonyInstaller.Util
{
    public partial class Constants
    {
        internal const string DEFAULT_LOG_FILENAME = "harmony.log";

        internal const int MSEC_WRITE_DELAY = 500;

        internal const int MAX_ROWS = 20;
    }

    internal class LogWriter
    {
        private static string m_logFilePath;

        internal LogWriter(string logName = null)
        {
            string strVortexAppdata = 
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vortex");

            m_logFilePath = (logName == null)
                ? Path.Combine(strVortexAppdata, Constants.DEFAULT_LOG_FILENAME)
                : Path.Combine(strVortexAppdata, logName);
        }

        public void WriteToLog()
        {
            while (true)
            {
                FileStream fs = null;

                try
                {
                    fs = new FileStream(m_logFilePath, FileMode.Append);
                    using (StreamWriter writer = new StreamWriter(fs))
                    {
                        string logEntry = VortexPatcher.Logger.Dequeue();
                        Console.WriteLine(logEntry);
                        writer.WriteLine(logEntry);
                    }
                }
                catch (Exception exc)
                {
                    Console.WriteLine(string.Format("Failed to write to logfile: {0}", exc));
                    break;
                }
            }
        }
    }
}
