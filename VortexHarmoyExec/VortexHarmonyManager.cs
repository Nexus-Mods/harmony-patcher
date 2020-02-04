using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Mono.Options;

namespace VortexHarmonyExec
{
    internal partial class Constants
    {
        public const string VORTEX_LIB = nameof(VortexHarmonyInstaller) + ".dll";

        public const string VORTEX_GUI_LIB = "VortexUnity.dll";
    }

    internal partial class Util
    {
        /// <summary>
        /// Given an absolute path to an assembly, this function will load the assembly
        ///  and attempt to find a referenced assembly name.
        /// </summary>
        /// <param name="absPath">The absolute path to the assembly we wish to load</param>
        /// <param name="assemblyName">The name of the assembly reference we want to check (Not the FullName)</param>
        /// <returns>An assembly name if we managed to find the reference - null if we couldn't find the ref</returns>
        internal static AssemblyName FindAssemblyRef(string absPath, string assemblyName)
        {
            if (!File.Exists(absPath))
            {
                string error = JSONResponse.CreateSerializedResponse("Couldn't find game assembly", Enums.EErrorCode.MISSING_FILE);
                Console.Error.WriteLine(error);
                return null;
            }

            Assembly gameAssembly = null;
            try { gameAssembly = Assembly.ReflectionOnlyLoadFrom(absPath); }
            catch (Exception exc)
            {
                string error = JSONResponse.CreateSerializedResponse("Unable to load game assembly", Enums.EErrorCode.UNKNOWN, exc);
                Console.Error.WriteLine(error);
                return null;
            }

            AssemblyName[] references = gameAssembly.GetReferencedAssemblies();
            AssemblyName assemblyRef = references.Where(reference => reference.Name == assemblyName).FirstOrDefault();
            if (assemblyRef == null)
            {
                string error = JSONResponse.CreateSerializedResponse($"Assembly does not contain {assemblyName} as a reference", Enums.EErrorCode.MISSING_ASSEMBLY_REF);
                Console.Error.WriteLine(error);
                return null;
            }

            return assemblyRef;
        }
    }

    class VortexHarmonyManager
    {
        private static Injector m_injector;
        private static string m_dataPath;

        private static bool m_injectVIGO;
        public static bool InjectVIGO { get { return m_injectVIGO; } }

        private static string m_installPath;
        public static string InstallPath { get { return m_installPath; } }

        private static string m_strExtensionPath;
        public static string ExtensionPath { get { return m_strExtensionPath; } }

        private static string m_entryPoint;

        private static string m_modsfolder;
        public static string ModsFolder { get { return m_modsfolder; } }

        public static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            // check for assemblies already loaded
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
            if (assembly != null)
                return assembly;

            // Try to load by filename - split out the filename of the full assembly name
            // and append the base path of the original assembly (i.e. look in the same dir)
            string filename = args.Name.Split(',')[0] + ".dll".ToLower();
            string asmFile = Path.Combine(".\\lib", filename);

            return Assembly.LoadFrom(asmFile);
        }

        static void RunOptions(string[] args)
        {
            bool removePatch = false;
            bool showHelp = false;

            // (The -q argument should be used on its own as all other arguments will be ignored)
            //  string will hold the path to the assembly we want to check for the .NET version.
            //  if an absolute path is not provided, Vortex will assume that we're looking for Unity's
            //  default assembly (Assembly-CSharp.dll)
            //  This is currently used by Vortex to ascertain which .NET version we want to use when
            //  building VIGO.
            string queryNETAssembly = string.Empty;

            // (This -a should be used on its own as all other arguments will be ignored)
            //  string will hold both the path to the assembly we want to load and the
            //  assembly name reference we want to verify. Value should have the following
            //  format: "{Assembly_Path}::{Assembly_Name}" e.g. "C:/somePath/assembly.dll::mscorlib"
            string queryAssemblyName = string.Empty;

            OptionSet options = new OptionSet()
                .Add("h", "Shows this message and closes program", h => showHelp = h != null)
                .Add("g|extension=", "Path to the game's extension folder", g => m_strExtensionPath = g)
                .Add("m|managed=", "Path to the game's managed folder/game assembly", m => m_dataPath = m)
                .Add("i|install=", "Path to Harmony Patcher's build folder.", i => m_installPath = i)
                .Add("e|entry=", "This game's entry point formatted as: 'Namespace.ClassName::MethodName'", e => m_entryPoint = e)
                .Add("x|modsfolder=", "The game's expected mods directory", x => m_modsfolder = x)
                .Add("r", "Will remove the harmony patcher", r => removePatch = r != null)
                .Add("q|querynet=", "(optional) Query the .NET version of the assembly file we attempt to patch", q => queryNETAssembly = q)
                .Add("v", "(optional) Used to decide whether we want to use VIGO or not", v => m_injectVIGO = v != null)
                .Add("a|queryassemblyname=", "(optional) Used to check assembly references. Expected format: 'Assembly_Path::Assembly_Name'" , a => queryAssemblyName = a);

            List<string> extra;
            try
            {
                extra = options.Parse(args);
            }
            catch (OptionException)
            {
                showHelp = true;
            }

            if (showHelp || (args.Length == 0))
            {
                ShowHelp(options);
                return;
            }

            if (!string.IsNullOrEmpty(queryNETAssembly))
            {
                const string FRAMEWORK_PREFIX = "FrameworkVersion=";
                bool bFoundVersion = false;
                string assemblyFile = (queryNETAssembly.EndsWith(".dll"))
                    ? queryNETAssembly : Path.Combine(queryNETAssembly, Constants.UNITY_ASSEMBLY_LIB);

                AssemblyName assemblyName = Util.FindAssemblyRef(assemblyFile, "mscorlib");
                if (assemblyName != null)
                {
                    // Found a reference
                    Console.WriteLine($"{FRAMEWORK_PREFIX}{assemblyName.Version.ToString()}");
                    bFoundVersion = true;
                }

                // We couldn't find a NET reference - lets see if there's a mscorlib assembly
                //  next to the game assembly.
                string potentialMscorlibFilePath = Path.Combine(Path.GetDirectoryName(assemblyFile), "mscorlib.dll");
                if (!bFoundVersion && File.Exists(potentialMscorlibFilePath))
                {
                    string version = System.Diagnostics.FileVersionInfo.GetVersionInfo(potentialMscorlibFilePath).FileVersion;
                    Console.WriteLine($"{FRAMEWORK_PREFIX}{version}");
                    bFoundVersion = true;
                }

                if (!bFoundVersion)
                {
                    // We will have to rely on the assembly's runtime version.
                    Assembly gameAss = Assembly.ReflectionOnlyLoadFrom(assemblyFile);
                    Console.WriteLine($"{FRAMEWORK_PREFIX}{gameAss.ImageRuntimeVersion}");
                }

                // This is a query operation, as mentioned above
                //  we're not going to patch the game assembly.
                return;
            }

            if (!string.IsNullOrEmpty(queryAssemblyName))
            {
                string[] parsed = queryAssemblyName.Split(new string[] { "::" }, StringSplitOptions.None);
                if (parsed.Length != 2)
                {
                    string strError = JSONResponse.CreateSerializedResponse("Invalid value, please respect format: 'Assembly_Path::Ref_Assembly_Name'", Enums.EErrorCode.INVALID_ARGUMENT);
                    Console.Error.WriteLine(strError);
                    return;
                }

                string assemblyFile = (parsed[0].EndsWith(".dll"))
                    ? parsed[0] : Path.Combine(parsed[0], Constants.UNITY_ASSEMBLY_LIB);

                AssemblyName assemblyName = Util.FindAssemblyRef(assemblyFile, parsed[1]);
                if (assemblyName != null)
                {
                    Console.WriteLine($"FoundAssembly={assemblyName.FullName}");
                }

                // This is a query operation, as mentioned above
                //  we're not going to patch the game assembly.
                return;
            }

            Run(removePatch);
        }

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            RunOptions(args);
            Environment.Exit(0);
        }

        private static void ShowHelp(OptionSet options)
        {
            Console.WriteLine("Usage: VortexHarmonyManager.exe -m 'managedFolderPath' -e 'ClassName::MethodName'");
            Console.WriteLine();
            Console.WriteLine("Options:");
            options.WriteOptionDescriptions(Console.Out);
        }

        private static void Run(bool bRemove)
        {
            m_injector = new Injector(m_dataPath, m_entryPoint);
            if (bRemove)
            {
                m_injector.RemovePatch();
            }
            else
            {
                m_injector.Inject();
            }
        }
    }
}
