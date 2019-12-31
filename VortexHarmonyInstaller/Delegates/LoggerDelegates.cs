using System;

namespace VortexHarmonyInstaller.Delegates
{
    public static class LoggerDelegates
    {
        public delegate void OnInfo(object something);
        public static OnInfo LogInfo = (object something) => VortexPatcher.Logger.Info(something);

        public delegate void OnError(object something, Exception e = null);
        public static OnError LogError = (object data, Exception e) => VortexPatcher.Logger.Error(data, e);

        public delegate void OnDebug(object data);
        public static OnDebug LogDebug = (object data) => VortexPatcher.Logger.Debug(data);
    }
}
