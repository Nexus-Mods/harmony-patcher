using System;

namespace VortexHarmonyInstaller.Delegates
{
    public static class LoggerDelegates
    {
        public delegate void OnInfo(object something);
        public static OnInfo LogInfo = (object something) => VortexHarmonyInstaller.VortexPatcher.Logger.Info(something);

        public delegate void OnError(object something, Exception e = null);
        public static OnError LogError = (object data, Exception e) => VortexHarmonyInstaller.VortexPatcher.Logger.Error(data, e);

        public delegate void OnDebug(object data);
        public static OnDebug LogDebug = (object data) => VortexHarmonyInstaller.VortexPatcher.Logger.Error(data);
    }
}
