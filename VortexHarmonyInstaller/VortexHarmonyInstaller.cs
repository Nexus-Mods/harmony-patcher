using Mono.Cecil;

using System;
using System.Collections.Generic;

using System.IO;
using System.Linq;

using System.Reflection;

using Microsoft.Practices.Unity;

using VortexHarmonyInstaller.ModTypes;
using VortexHarmonyInstaller.Util;

namespace VortexHarmonyInstaller
{
    public partial class Constants
    {
        public const string INSTALLER_ASSEMBLY_NAME = nameof(VortexHarmonyInstaller);

        public const string UNITY_ENGINE_LIB = "UnityEngine.dll";
    }

    internal class MissingAssemblyResolver : BaseAssemblyResolver
    {
        private DefaultAssemblyResolver m_AssemblyResolver;
        private readonly string m_strAssemblyPath;

        public MissingAssemblyResolver(string strAssemblyPath)
        {
            m_AssemblyResolver = new DefaultAssemblyResolver();
            m_AssemblyResolver.AddSearchDirectory(strAssemblyPath);
            m_strAssemblyPath = strAssemblyPath;
        }

        public override AssemblyDefinition Resolve(AssemblyNameReference name)
        {
            AssemblyDefinition assembly = null;
            try
            {
                assembly = m_AssemblyResolver.Resolve(name);
            }
            catch (Exception)
            {
                string[] libraries = Directory.GetFiles(m_strAssemblyPath, "*.dll", SearchOption.AllDirectories);
                string missingLib = libraries.Where(lib => lib.Contains(name.Name)).SingleOrDefault();
                assembly = AssemblyDefinition.ReadAssembly(missingLib);
            }
            return assembly;
        }
    }

    public class VortexPatcher
    {
        private static AssemblyDefinition m_InstallerAssembly = null;
        public static AssemblyDefinition InstallerAssembly { get { return m_InstallerAssembly; } }

        private static List<IModType> m_liMods = new List<IModType>();
        public static List<IModType> GameMods { get { return m_liMods; } }

        public static Version InstallerVersion { get { return m_InstallerAssembly.Name.Version; } }

        public static readonly ILogger Logger = Singleton<LogManager>.Instance;

        public delegate void ModsInjectionCompleteHandler(List<IExposedMod> exposedMods);
        public static event ModsInjectionCompleteHandler ModsInjectionComplete;

        private static string m_strDataPath;
        public static string CurrentDataPath { get { return m_strDataPath; } }

        // Where we expect to find the mods.
        private static string m_modsPath;
        public static string CurrentModsPath { get { return m_modsPath; } }

        private static void OnModsInjectionComplete(List<IExposedMod> exposedMods)
        {
            ModsInjectionComplete?.Invoke(exposedMods);
        }

        public static IModType FindMod<T>(string id)
        {
            IModType[] mods = m_liMods.Where(mod => mod.GetType() == typeof(T)).ToArray();
            IModType modEntry = mods.FirstOrDefault(mod => mod.GetModName() == id);
            if (modEntry == null)
                Logger.Info($"Unable to find mod entry: {id}");

            return modEntry;
        }

        public static void Patch(string modsPath)
        {
            m_modsPath = modsPath;
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
            MissingAssemblyResolver resolver = new MissingAssemblyResolver(m_strDataPath);

            Logger.Info("===============");
            Logger.Info("Patcher started");
            string strAssemblyPath = Path.Combine(CurrentDataPath, Constants.INSTALLER_ASSEMBLY_NAME + ".dll");
            m_InstallerAssembly = AssemblyDefinition.ReadAssembly(strAssemblyPath);

            // All dll files within the provided mods folder are considered mod entries.
            FileInfo[] modLibFiles = new DirectoryInfo(m_modsPath).GetFiles("*.dll", SearchOption.AllDirectories);
            ResolveModList(modLibFiles);

            Logger.Info("Sorting mod load order");
            // We should now have the mod list populated and ready, we can now
            //  sort the mods according to what the user selected.
            //m_liMods.Sort(IComparer)

            Logger.Info("Starting to inject mods");
            foreach (var mod in m_liMods)
            {
                try
                {
                    Logger.Info($"Injecting mod: {mod.GetModName()}");
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
            var types = Assembly.GetExecutingAssembly().GetTypes()
                .Where(p => interfaceType.IsAssignableFrom(p)
                    && p.FullName != "VortexHarmonyInstaller.IModType");

            UnityContainer container = new UnityContainer();
            foreach (Type type in types)
            {
                try
                {
                    container.RegisterType(typeof(IModType), type);
                    IModType modType = container.Resolve(type) as IModType;
                    if (modType.ParseModData(dllRoot))
                        return modType;
                }
                catch (Exception exc)
                {
                    Logger.Error("Unable to parse mod data", exc);
                    continue;
                }
            }

            return null;
        }
    }
}
