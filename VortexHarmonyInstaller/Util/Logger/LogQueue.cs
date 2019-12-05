using System;
using System.Collections.Generic;
using System.Threading;

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
        readonly object queueLock = new object();
        internal Queue<LogEntry> m_queue = new Queue<LogEntry>();

        internal void Enqueue(LogEntry log)
        {
            lock (queueLock)
            {
                m_queue.Enqueue(log);

                // Releases the lock.
                Monitor.Pulse(queueLock);
            }
        } 

        internal string Dequeue()
        {
            lock (queueLock)
            {
                while (m_queue.Count == 0)
                {
                    // Nothing to do here - wait.
                    Monitor.Wait(queueLock);
                }
            }

            return m_queue.Dequeue().ToString();
        }
    }
}
