using System;
using System.IO;

using Newtonsoft.Json;

using VortexHarmonyInstaller.Delegates;

namespace VortexHarmonyInstaller.ModTypes
{
    internal partial class Constants
    {
        internal const string VORTEX_SETTINGS_FILE = "Settings.json";
    }

    public class VortexModSettings : ISettings
    {
        public virtual string GetSettingsPath(IExposedMod mod)
        {
            VortexMod modEntry = mod as VortexMod;
            return Path.Combine(modEntry.VortexData.ModPath, Constants.VORTEX_SETTINGS_FILE);
        }

        public T Load<T, U>(IExposedMod mod) where T : U, new()
        {
            string strSettingsPath = GetSettingsPath(mod);
            if (File.Exists(strSettingsPath))
            {
                try
                {
                    T deserialized = JsonConvert.DeserializeObject<T>(strSettingsPath);
                    return deserialized;
                }
                catch (Exception e)
                {
                    LoggerDelegates.LogError($"Can't read {strSettingsPath}.", e);
                }
            }

            return new T();
        }

        public virtual void Save(IExposedMod mod)
        {
            VortexMod vortexMod = mod as VortexMod;
            Save(this, vortexMod);
        }

        public static void Save<T>(T data, VortexMod mod) where T : VortexModSettings
        {
            try
            {
                string strSettingsPath = data.GetSettingsPath(mod);
                string strSerializedSettings = JsonConvert.SerializeObject(data);
                File.WriteAllText(strSettingsPath, strSerializedSettings);
            }
            catch (Exception exc)
            {
                VortexPatcher.Logger.Error("Failed to save mod settings", exc);
            }
        }

        public static T Load<T>(VortexMod mod) where T : VortexModSettings, new()
        {
            T t = new T();
            string strSettingsPath = t.GetSettingsPath(mod);
            if (File.Exists(strSettingsPath))
            {
                try
                {
                    string fileContents = File.ReadAllText(strSettingsPath);
                    T deserialized = JsonConvert.DeserializeObject<T>(fileContents);
                    return deserialized;
                }
                catch (Exception e)
                {
                    LoggerDelegates.LogError($"Can't read {strSettingsPath}.", e);
                }
            }

            return t;
        }
    }
}
