using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VortexHarmonyInstaller.Delegates
{
    public static class LoggerDelegates
    {
        public delegate void OnInfo(object something);
        public static OnInfo LogInfo => VortexHarmonyInstaller.VortexPatcher.Logger.Info;

        public delegate void OnError(object something);
        public static OnError LogError => VortexHarmonyInstaller.VortexPatcher.Logger.Error;

        public delegate void OnDebug(object something);
        public static OnDebug LogDebug => VortexHarmonyInstaller.VortexPatcher.Logger.Error;
    }
}
