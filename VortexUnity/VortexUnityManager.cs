using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using VortexHarmonyInstaller;
using VortexHarmonyInstaller.Delegates;

namespace VortexUnity
{
    public class VortexUnityManager
    {
        public static void RunUnityPatcher()
        {
            VortexHarmonyInstaller.VortexPatcher.ModsInjectionComplete += LoadVortexUI;
        }

        public static void LoadVortexUI(List<IExposedMod> exposedMods)
        {
            try
            {
                VortexUI.Load(exposedMods);
            }
            catch (Exception exc)
            {
                LoggerDelegates.LogError(exc);
            }
        }
    }
}
