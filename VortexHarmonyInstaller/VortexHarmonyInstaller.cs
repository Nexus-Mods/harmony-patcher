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

        public const string LOAD_ORDER_FILENAME = "load_order.txt";
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

            string expectedLoadOrderFileLocation = Path.Combine(m_modsPath, Constants.LOAD_ORDER_FILENAME);
            if (File.Exists(expectedLoadOrderFileLocation))
            {
                Logger.Info("Sorting mod load order");
                Queue<FileInfo> sorted = new Queue<FileInfo>();
                string[] loadOrder = File.ReadAllLines(expectedLoadOrderFileLocation);
                foreach (string orderEntry in loadOrder)
                {
                    if (orderEntry == string.Empty)
                        continue;

                    FileInfo modAssembly = modLibFiles.FirstOrDefault(file => file.Name == orderEntry);
                    if (modAssembly != null)
                    {
                        sorted.Enqueue(modAssembly);
                    }
                    else
                    {
                        // We couldn't find the assembly for this orderEntry... Log this and continue.
                        Logger.Warn($"Cannot find mod assembly for order entry: {orderEntry}");
                        continue;
                    }
                }

                // Check if we managed to sort all assemblies and queue any leftover mods.
                if (modLibFiles.Length != sorted.Count)
                {
                    List<FileInfo> diff = modLibFiles.Where(modLib => !sorted.Any(assemblyName => assemblyName.Name == modLib.Name)).ToList();
                    string missingEntries = string.Join(", ", diff.Select(entry => entry.Name).ToArray());
                    Logger.Warn($"Load order did contain the following mods: {missingEntries}; - these will be queued at the end of the mod list");

                    foreach (FileInfo file in diff)
                        sorted.Enqueue(file);
                }

                string finalList = string.Join(", ", sorted.Select(entry => entry.Name).ToArray());
                Logger.Debug($"Final list is: {finalList}");

                modLibFiles = sorted.ToArray();
                Logger.Info("Finished sorting");
            }
            else
            {
                Logger.Warn($"Load order file is missing, expected file location is: {expectedLoadOrderFileLocation}");
            }

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
            // Certain mods may contain multiple dll files at the mod's root directory
            //  when encountering such a scenario, we expect the mod author to specify
            //  which assembly to use in the manifest file. The func will return true
            //  if the mod is to be inserted into the mod list; false otherwise.
            Func<FileInfo, IModType, bool> IsModAssembly = (dllFile, modType) =>
            {
                string modDir = dllFile.DirectoryName;
                FileInfo[] assemblies = new DirectoryInfo(modDir).GetFiles("*.dll", SearchOption.TopDirectoryOnly);
                if (assemblies.Length > 1)
                {
                    string targetAssembly = modType.GetModData().GetTargetAssemblyFileName();
                    if (string.IsNullOrEmpty(targetAssembly))
                        return false;

                    return (targetAssembly.ToLower() == dllFile.Name.ToLower());
                }
                else
                {
                    return true;
                }
            };

            foreach (FileInfo dll in modFiles)
            {
                var modType = IdentifyModType(dll.DirectoryName);
                if (modType == null)
                {
                    Logger.ErrorFormat("Unidentified modType {0}", dll.Name);
                    continue;
                }

                if (!IsModAssembly(dll, modType))
                    continue;

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
