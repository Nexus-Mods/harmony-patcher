using Harmony;
using Mono.Cecil;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Unity;
using VortexHarmonyInstaller.ModTypes;
using log4net;

namespace VortexHarmonyInstaller
{
    public partial class Constants
    {
        public const string INSTALLER_ASSEMBLY_NAME = nameof(VortexHarmonyInstaller);

        public const string LOG_CONFIG = "log4net.config";

        public const string HARMONY_LOG_FILENAME = "harmony.log";

        public const string UNITY_ENGINE_LIB = "UnityEngine.dll";
    }

    public class VortexPatcher
    {
        private static AssemblyDefinition m_InstallerAssembly = null;
        public static AssemblyDefinition InstallerAssembly { get { return m_InstallerAssembly; } }

        private static List<IModType> m_liMods = new List<IModType>();
        public static List<IModType> GameMods { get { return m_liMods; } }

        public static Version InstallerVersion { get { return m_InstallerAssembly.Name.Version; } }

        public static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public delegate void ModsInjectionCompleteHandler(List<IExposedMod> exposedMods);
        public static event ModsInjectionCompleteHandler ModsInjectionComplete;

        private static string m_strDataPath;
        public static string CurrentDataPath { get { return m_strDataPath; } }

        private static void OnModsInjectionComplete(List<IExposedMod> exposedMods)
        {
            if (ModsInjectionComplete != null)
            {
                ModsInjectionComplete(exposedMods);
            }
        }

        public static void Patch()
        {
            // Set log file destination.
            string strVortexAppdata = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vortex");
            log4net.GlobalContext.Properties["LogFilePath"] = Path.Combine(strVortexAppdata, Constants.HARMONY_LOG_FILENAME);

            // Look up the UnityEngine file
            string strCurrentDir = Directory.GetCurrentDirectory();
            string strUnityEngine = Directory.GetFiles(strCurrentDir, "*.dll", SearchOption.AllDirectories)
                .Where(file => file.EndsWith(Constants.UNITY_ENGINE_LIB))
                .SingleOrDefault();

            if (null == strUnityEngine)
            {
                Logger.Error("Unable to find the game's managed datapath, please re-install the game");
                return;
            }

            m_strDataPath = Path.GetDirectoryName(strUnityEngine);

            // Load log4net configuration
            FileInfo logfile = new FileInfo(Path.Combine(CurrentDataPath, Constants.LOG_CONFIG));
            log4net.Config.XmlConfigurator.Configure(logfile);

            Logger.Info("===============");
            Logger.Info("Patcher started");
            string strAssemblyPath = Path.Combine(CurrentDataPath, Constants.INSTALLER_ASSEMBLY_NAME + ".dll");
            m_InstallerAssembly = AssemblyDefinition.ReadAssembly(strAssemblyPath);

            HarmonyInstance harmony = HarmonyInstance.Create("com.blacktreegaming.harmonypatcher");
            harmony.PatchAll();

            string modsFolder = Path.Combine(CurrentDataPath, "VortexMods");

            // All dll files within the VortexMods folder are considered mods.
            FileInfo[] modLibFiles = new DirectoryInfo(modsFolder).GetFiles("*.dll", SearchOption.AllDirectories);
            ResolveModList(modLibFiles);

            Logger.Info("Starting to inject mods");
            foreach (IModType mod in m_liMods)
            {
                try
                {
                    Logger.InfoFormat("Injecting mod: [{0}]", mod.GetModName());
                    mod.InjectPatches();
                }
                catch (Exception exc)
                {
                    Logger.Error("Invalid mod entry skipped", exc);
                }
            }

            Logger.Info("Finished patching");

            // Notify any subscribers we're finished patching.
            OnModsInjectionComplete(BaseModType.ExposedMods);
        }

        public static void ResolveModList(FileInfo[] modFiles)
        {
            foreach (FileInfo dll in modFiles)
            {
                var modType = IdentifyModType(dll.DirectoryName);
                if (modType == null)
                {
                    Logger.ErrorFormat("Unidentified modType {0}", dll.Name);
                    continue;
                }

                // Attempt to run assembly conversions.
                if (!modType.ConvertAssemblyReferences(dll.FullName))
                {
                    Logger.ErrorFormat("Failed to convert assembly references {0}", dll.Name);
                    continue;
                }

                // We managed to identify the modType, add it.
                m_liMods.Add(modType);
            }
        }

        public static IModType IdentifyModType(string dllRoot)
        {
            var interfaceType = typeof(IModType);
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => interfaceType.IsAssignableFrom(p)
                    && p.FullName != "VortexHarmonyInstaller.IModType");

            UnityContainer container = new UnityContainer();
            foreach (Type type in types)
            {
                container.RegisterType(typeof(IModType), type);
                IModType modType = container.Resolve(type) as IModType;
                if (modType.ParseModData(dllRoot))
                {
                    return modType;
                }
            }

            return null;
        }
    }
}
