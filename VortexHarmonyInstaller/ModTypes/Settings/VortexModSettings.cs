using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using VortexHarmonyInstaller.Delegates;

namespace VortexHarmonyInstaller.ModTypes
{
    internal partial class Constants
    {
        const string VORTEX_SETTINGS_FILE = "Settings.json";
    }

    class VortexModSettings : ISettings
    {
        public virtual string GetSettingsPath(IExposedMod mod)
        {
            VortexMod modEntry = mod as VortexMod;
            return Path.Combine(modEntry.DataPath, Constants.UMM_SETTINGS_FILE_NAME);
        }

        public T Load<T, U>(IExposedMod mod) where T : U, new()
        {
            throw new NotImplementedException();
        }

        public void Save(IExposedMod mod)
        {
            throw new NotImplementedException();
        }

        public static void Save<T>(T data, VortexMod mod) where T : VortexModSettings
        {
            
        }

        public static T Load<T>(VortexMod mod) where T : VortexModSettings, new()
        {
            throw new NotImplementedException();
        }
    }
}
