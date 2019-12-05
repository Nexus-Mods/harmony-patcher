using System;
using System.Collections.Generic;
using System.Threading;

using ObjectDumper;

namespace VortexHarmonyInstaller.Util
{
    public class LogManager: ILogger
    {
        private LogWriter m_logWriter = new LogWriter();
        private LogQueue m_logQueue = new LogQueue();
        private LogManager()
        {
            Thread writerThread = new Thread(new ThreadStart(m_logWriter.WriteToLog));
            writerThread.Start();
        }

        public void Debug(object message)
        {
            Debug(message, null);
        }

        public void Debug(object message, Exception exception = null)
        {
            string strMess = (message.GetType() == typeof(System.String))
                ? (System.String)(message) : message.DumpToString(message.GetType().ToString());

            CreateAndQueueLogEntry(strMess, Enums.ESeverity.DEBUG, exception);
        }

        public void DebugFormat(string format, params object[] args)
        {
            string strMess = string.Format(format, args);
            CreateAndQueueLogEntry(strMess, Enums.ESeverity.DEBUG);
        }

        public void DebugFormat(string format, object arg0)
        {
            DebugFormat(format, new object[] { arg0 });
        }

        public void DebugFormat(string format, object arg0, object arg1)
        {
            DebugFormat(format, new object[] { arg0, arg1 });
        }

        public void DebugFormat(string format, object arg0, object arg1, object arg2)
        {
            DebugFormat(format, new object[] { arg0, arg1, arg2 });
        }

        public void DebugFormat(IFormatProvider provider, string format, params object[] args)
        {
            // Format provider? don't care.
            DebugFormat(format, args);
        }

        public void Error(object message)
        {
            Error(message, null);
        }

        public void Error(object message, Exception exception)
        {
            string strMess = (message.GetType() == typeof(System.String))
                ? (System.String)(message) : message.DumpToString(message.GetType().ToString());

            CreateAndQueueLogEntry(strMess, Enums.ESeverity.ERROR, exception);
        }

        public void ErrorFormat(string format, params object[] args)
        {
            string strMess = string.Format(format, args);
            CreateAndQueueLogEntry(strMess, Enums.ESeverity.ERROR);
        }

        public void ErrorFormat(string format, object arg0)
        {
            ErrorFormat(format, new object[] { arg0 });
        }

        public void ErrorFormat(string format, object arg0, object arg1)
        {
            ErrorFormat(format, new object[] { arg0, arg1 });
        }

        public void ErrorFormat(string format, object arg0, object arg1, object arg2)
        {
            ErrorFormat(format, new object[] { arg0, arg1, arg2 });
        }

        public void ErrorFormat(IFormatProvider provider, string format, params object[] args)
        {
            ErrorFormat(format, args);
        }

        public void Info(object message)
        {
            Info(message, null);
        }

        public void Info(object message, Exception exception = null)
        {
            string strMess = (message.GetType() == typeof(System.String))
                ? (System.String)(message) : message.DumpToString(message.GetType().ToString());

            CreateAndQueueLogEntry(strMess, Enums.ESeverity.INFO, exception);
        }

        public void InfoFormat(string format, params object[] args)
        {
            string strMess = string.Format(format, args);
            CreateAndQueueLogEntry(strMess, Enums.ESeverity.INFO);
        }

        public void InfoFormat(string format, object arg0)
        {
            InfoFormat(format, new object[] { arg0 });
        }

        public void InfoFormat(string format, object arg0, object arg1)
        {
            InfoFormat(format, new object[] { arg0, arg1 });
        }

        public void InfoFormat(string format, object arg0, object arg1, object arg2)
        {
            InfoFormat(format, new object[] { arg0, arg1, arg2 });
        }

        public void InfoFormat(IFormatProvider provider, string format, params object[] args)
        {
            InfoFormat(format, args);
        }

        public void Warn(object message)
        {
            Warn(message, null);
        }

        public void Warn(object message, Exception exception = null)
        {
            string strMess = (message.GetType() == typeof(System.String))
                ? (System.String)(message) : message.DumpToString(message.GetType().ToString());

            CreateAndQueueLogEntry(strMess, Enums.ESeverity.INFO, exception);
        }

        public void WarnFormat(string format, params object[] args)
        {
            string strMess = string.Format(format, args);
            CreateAndQueueLogEntry(strMess, Enums.ESeverity.WARNING);
        }

        public void WarnFormat(string format, object arg0)
        {
            WarnFormat(format, new object[] { arg0 });
        }

        public void WarnFormat(string format, object arg0, object arg1)
        {
            WarnFormat(format, new object[] { arg0, arg1 });
        }

        public void WarnFormat(string format, object arg0, object arg1, object arg2)
        {
            WarnFormat(format, new object[] { arg0, arg1, arg2 });
        }

        public void WarnFormat(IFormatProvider provider, string format, params object[] args)
        {
            WarnFormat(format, args);
        }

        public string[] DequeueRows(int rows)
        {
            List<string> rowList = new List<string>();
            for (int i = 0; i < rows; i++)
            {
                string logEntry = String.Empty;
                try
                {
                    logEntry = m_logQueue.Dequeue();
                }
                catch (Exception exc)
                {
                    if (string.IsNullOrEmpty(logEntry))
                        break;
                }

                rowList.Add(logEntry);
            }

            return rowList.ToArray();
        }

        public string Dequeue()
        {
            return m_logQueue.Dequeue().ToString();
        }

        private void CreateAndQueueLogEntry(string strMessage, Enums.ESeverity sev, Exception exc = null)
        {
            LogEntry logEntry = new LogEntry(strMessage, sev, exc);
            m_logQueue.Enqueue(logEntry);
        }
    }
}
