using System;
using System.IO;
using System.Xml.Serialization;

using VortexHarmonyInstaller.Delegates;

using static UnityModManagerNet.UnityModManager;

namespace VortexHarmonyInstaller.ModTypes
{
    internal partial class Constants
    {
        internal const string UMM_SETTINGS_FILE_NAME = "Settings.xml";
    }

    // UMM's settings object.
    public class ModSettings: ISettings
    {
        public virtual string GetSettingsPath(IExposedMod mod)
        {
            ModEntry modEntry = mod as ModEntry;
            return Path.Combine(modEntry.Path, Constants.UMM_SETTINGS_FILE_NAME);
        }

        public virtual void Save(IExposedMod mod)
        {
            ModEntry modEntry = mod as ModEntry;
            Save(this, modEntry);
        }

        public static void Save<T>(T data, ModEntry mod) where T : ModSettings, new()
        {
            var filepath = data.GetSettingsPath(mod);
            try
            {
                using (var writer = new StreamWriter(filepath))
                {
                    var serializer = new XmlSerializer(typeof(T));
                    serializer.Serialize(writer, data);
                }
            }
            catch (Exception e)
            {
                LoggerDelegates.LogError($"Can't save {filepath}.", e);
            }
        }

        public virtual T Load<T, U>(IExposedMod mod) where T : U, new()
        {
            ModEntry modEntry = mod as ModEntry;
            var t = new T();
            var filepath = GetSettingsPath(modEntry);
            if (File.Exists(filepath))
            {
                try
                {
                    using (var stream = File.OpenRead(filepath))
                    {
                        var serializer = new XmlSerializer(typeof(T));
                        var result = (T)serializer.Deserialize(stream);
                        return result;
                    }
                }
                catch (Exception e)
                {
                    LoggerDelegates.LogError($"Can't read {filepath}.", e);
                }
            }

            return t;
        }

        public static T Load<T>(ModEntry modEntry) where T : ModSettings, new()
        {
            var t = new T();
            var filepath = t.GetSettingsPath(modEntry);
            if (File.Exists(filepath))
            {
                try
                {
                    using (var stream = File.OpenRead(filepath))
                    {
                        var serializer = new XmlSerializer(typeof(T));
                        var result = (T)serializer.Deserialize(stream);
                        return result;
                    }
                }
                catch (Exception e)
                {
                    LoggerDelegates.LogError($"Can't read {filepath}.", e);
                }
            }

            return t;
        }
    }
}
