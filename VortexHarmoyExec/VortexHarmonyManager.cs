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
            bool bRemovePatch = false;
            bool bShowHelp = false;
            OptionSet options = new OptionSet
            {
                { "h", "Shows this message and closes program", h => bShowHelp = h != null },
                { "g|extension=", "Path to the game's extension folder", g => m_strExtensionPath = g },
                { "m|managed=", "Path to the game's managed folder/game assembly", m => m_dataPath = m },
                { "i|install=", "Path to Harmony Patcher's build folder.", i => m_installPath = i },
                { "e|entry=", "This game's entry point formatted as: 'Namespace.ClassName::MethodName'", e => m_entryPoint = e },
                { "x|modsfolder=", "The game's expected mods directory", x => m_modsfolder = x },
                { "r", "Will remove the harmony patcher", r => bRemovePatch = r != null },
            };


            List<string> extra;
            try
            {
                extra = options.Parse(args);
            }
            catch (OptionException)
            {
                bShowHelp = true;
            }

            if (bShowHelp || (args.Length < options.Count - 1))
            {
                ShowHelp(options);
                return;
            }

            Run(bRemovePatch);
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
