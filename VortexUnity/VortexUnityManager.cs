using System;
using System.Collections.Generic;

using System.IO;
using System.Linq;
using System.Reflection;

using VortexHarmonyInstaller;
using VortexHarmonyInstaller.Delegates;

namespace VortexUnity
{
    public class VortexUnityManager
    {
        private static Assembly AssemblyResolver(object sender, ResolveEventArgs args)
        {
            string currentDir = Directory.GetCurrentDirectory();
            string[] libs = Directory.GetFiles(currentDir, "*.dll", SearchOption.AllDirectories);

            string assemblyPath = libs
                .Where(lib => Path.GetFileName(lib).Contains(args.Name))
                .SingleOrDefault();

            return (assemblyPath != null)
                ? Assembly.LoadFile(assemblyPath)
                : null;
        }

        public static void RunUnityPatcher()
        {
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += new ResolveEventHandler(AssemblyResolver);

            VortexPatcher.ModsInjectionComplete += LoadVortexUI;
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
