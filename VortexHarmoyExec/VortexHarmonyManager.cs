﻿using System;
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
        public static bool IsFullPath(string path)
        {
            return !String.IsNullOrWhiteSpace(path)
                && path.IndexOfAny(Path.GetInvalidPathChars().ToArray()) == -1
                && Path.IsPathRooted(path)
                && !Path.GetPathRoot(path).Equals(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal);
        }
    }
    class VortexHarmonyManager
    {
        private static Injector m_injector;
        private static string m_dataPath;
        private static string m_installPath;
        public static string InstallPath { get { return m_installPath; } }

        private static string m_entryPoint;

        public static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            // check for assemblies already loaded
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
            if (assembly != null)
                return assembly;

            // Try to load by filename - split out the filename of the full assembly name
            // and append the base path of the original assembly (ie. look in the same dir)
            string filename = args.Name.Split(',')[0] + ".dll".ToLower();
            string asmFile = Path.Combine(@".\", "lib", filename);

            return System.Reflection.Assembly.LoadFrom(asmFile);
        }

        static void RunOptions(string[] args)
        {
            bool bRemovePatch = false;
            bool bShowHelp = false;
            OptionSet options = new OptionSet
            {
                { "h", "Shows this message and closes program", h => bShowHelp = h != null },
                { "m|managed=", "Path to the game's managed folder.", m => m_dataPath = m },
                { "i|install=", "Path to Harmony Patcher's build folder.", i => m_installPath = i },
                { "e|entry=", "This game's entry point formatted as: 'Namespace.ClassName::MethodName'", e => m_entryPoint = e },
                { "r", "Will remove", r => bRemovePatch = r != null },
            };


            List<string> extra;
            try
            {
                extra = options.Parse(args);
            }
            catch (OptionException e)
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
