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

            // Highlights that we do not wish to patch the game assembly
            //  but query the .NET version instead. This is currently used
            //  by Vortex to ascertain which .NET version we want to use when
            //  building VIGO.
            string queryNETAssembly = string.Empty;

            OptionSet options = new OptionSet()
                .Add("h", "Shows this message and closes program", h => showHelp = h != null)
                .Add("g|extension=", "Path to the game's extension folder", g => m_strExtensionPath = g)
                .Add("m|managed=", "Path to the game's managed folder/game assembly", m => m_dataPath = m)
                .Add("i|install=", "Path to Harmony Patcher's build folder.", i => m_installPath = i)
                .Add("e|entry=", "This game's entry point formatted as: 'Namespace.ClassName::MethodName'", e => m_entryPoint = e)
                .Add("x|modsfolder=", "The game's expected mods directory", x => m_modsfolder = x)
                .Add("r", "Will remove the harmony patcher", r => removePatch = r != null)
                .Add("q|querynet=", "Query the .NET version of the assembly file we attempt to patch", q => queryNETAssembly = q)
                .Add("v", "Used to decide whether we want to use VIGO or not", v => m_injectVIGO = v != null);

            List<string> extra;
            try
            {
                extra = options.Parse(args);
            }
            catch (OptionException)
            {
                showHelp = true;
            }

            if (!string.IsNullOrEmpty(queryNETAssembly))
            {
                // This is a query operation, as mentioned above
                //  we're not going to patch the game assembly.
                QueryNETVersion(queryNETAssembly);
                return;
            }

            if (showHelp || (args.Length < options.Count - 1))
            {
                ShowHelp(options);
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

        private static void QueryNETVersion(string dataPath)
        {
            string assemblyFile = (dataPath.EndsWith(".dll"))
                ? dataPath : Path.Combine(dataPath, Constants.UNITY_ASSEMBLY_LIB);

            if (!File.Exists(assemblyFile))
            {
                string error = JSONResponse.CreateSerializedResponse("Couldn't find game assembly", Enums.EErrorCode.MISSING_FILE);
                Console.Error.WriteLine(error);
                return;
            }

            Assembly gameAssembly = null;
            try { gameAssembly = Assembly.ReflectionOnlyLoadFrom(assemblyFile); }
            catch (Exception exc)
            {
                string error = JSONResponse.CreateSerializedResponse("Unable to load game assembly", Enums.EErrorCode.UNKNOWN, exc);
                Console.Error.WriteLine(error);
                return;
            }

            AssemblyName[] references = gameAssembly.GetReferencedAssemblies();
            AssemblyName corlib = references.Where(reference => reference.Name == "mscorlib").FirstOrDefault();
            if (corlib == null)
            {
                string error = JSONResponse.CreateSerializedResponse("Assembly does not contain mscorlib reference", Enums.EErrorCode.MISSING_ASSEMBLY_REF);
                Console.Error.WriteLine(error);
                return;
            }

            Console.WriteLine($"FrameworkVersion={corlib.Version.ToString()}");
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
