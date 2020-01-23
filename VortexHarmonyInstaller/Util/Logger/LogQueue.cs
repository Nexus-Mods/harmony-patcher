using System;
using System.Collections.Generic;
//using System.Threading;

namespace VortexHarmonyInstaller.Util
{
    internal partial class Enums
    {
        internal enum ESeverity
        {
            INFO,
            DEBUG,
            WARNING,
            ERROR,
        };
    }

    internal class LogEntry
    {
        private DateTime m_datetime;

        private string m_errorMessage;
        internal string ErrorMessage {
            get { return m_errorMessage; }
            private set { m_errorMessage = value; }
        }

        private Enums.ESeverity m_severity;
        internal Enums.ESeverity Severity { get { return m_severity; } }

        private Exception m_exception;
        internal Exception GetException { get { return m_exception; } }
        
        internal LogEntry(string errorMessage, Enums.ESeverity sev, Exception exc = null)
        {
            m_datetime = DateTime.Now;
            m_errorMessage = errorMessage;
            m_severity = sev;
            m_exception = exc;
        }

        public override string ToString()
        {
            string errorMessage = (m_exception != null)
                ? m_errorMessage + " - " + m_exception.ToString()
                : m_errorMessage;

            return $"{m_datetime.ToLocalTime()} - [{m_severity.ToString()}]: {errorMessage}";
        }
    }

    internal class LogQueue
    {
        private bool isLocked = false;
        private Queue<LogEntry> m_queue = new Queue<LogEntry>();
        private List<LogEntry> m_backlog = new List<LogEntry>();

        internal void Enqueue(LogEntry log)
        {
            if (Lock())
            {
                // We got the lock, enqueue backlog first, then the new log.
                foreach(LogEntry entry in m_backlog)
                {
                    m_queue.Enqueue(entry);
                }

                m_backlog.Clear();

                m_queue.Enqueue(log);
                Release();
            }
            else
            {
                // We don't have the lock, add to backlog
                m_backlog.Add(log);
            }
        } 

        internal string Dequeue()
        {
            string ret = string.Empty;
            if (Lock())
            {
                if (m_queue.Count > 0)
                    ret = m_queue.Dequeue().ToString();

                Release();
            }

            return ret;
        }

        /// <returns>true if we managed to get the lock, false otherwise</returns>
        private bool Lock()
        {
            if (isLocked)
                return false;

            isLocked = true;
            return true;
        }

        private void Release()
        {
            isLocked = false;
        }
    }
}
