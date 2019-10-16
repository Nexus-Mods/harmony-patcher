using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VortexHarmonyInstaller.Util
{
    public interface ILoggableMod
    {
        void LogInfo(string strMessage, Exception exc);

        void LogInfo(object obj, string strType);

        void LogDebug(string strMessage, Exception exc);

        void LogDebug(object obj, string strType);

        void LogError(string strMessage, Exception exc);

        void LogError(object obj, string strType);
    }
}
