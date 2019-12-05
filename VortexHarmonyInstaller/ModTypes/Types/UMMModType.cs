using Microsoft.Practices.Unity;

using Mono.Cecil;

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;

namespace VortexHarmonyInstaller.ModTypes
{
    internal partial class Constants
    {
        internal const string UMM_MANIFEST_FILENAME = "Info.json";

        internal const string UMM_ASSEMBLY_REF_NAME = "UnityModManager";

        internal const string VORTEX_TEMP_SUFFIX = "_vortex_temporary";

        internal const string VORTEX_BACKUP = "_vortex_backup";

        // Namespace.Classname
        internal const string UMM_TYPENAME = "UnityModManagerNet.UnityModManager";
    }

    internal partial class Exceptions
    {
        internal class AssemblyIsInjectedException : Exception
        {
            internal AssemblyIsInjectedException(string strMessage)
                : base(strMessage) { }
        }

        internal class InvalidArgumentException : Exception
        {
            internal InvalidArgumentException(string argument)
                : base($"Argument: {argument} is invalid") { }
        }

        internal class NotNETAssemblyException : Exception
        {
            internal NotNETAssemblyException(string assembly)
                : base($"{assembly} does not depend on .NET framework") { }
        }
    }

    internal partial class Util
    {
        internal static bool IsValidAssemblyPath(string assemblyPath)
        {
            if (!Path.IsPathRooted(assemblyPath))
                return false;

            if (!File.Exists(assemblyPath))
                return false;

            return true;
        }

        internal static Version GetFrameworkVer(string assemblyFilePath)
        {
            if (!Path.IsPathRooted(assemblyFilePath))
                throw new Exceptions.InvalidArgumentException(assemblyFilePath);

            if (!File.Exists(assemblyFilePath))
                throw new FileNotFoundException(assemblyFilePath);

            string[] extensions = new string[] { ".dll", ".exe" };
            string assemblyExt = Path.GetExtension(assemblyFilePath);

            if (extensions.Where(ext => ext == assemblyExt).SingleOrDefault() == null)
                throw new Exceptions.InvalidArgumentException(assemblyFilePath);

            AssemblyDefinition assDef = AssemblyDefinition.ReadAssembly(assemblyFilePath);
            var corLib = assDef.MainModule.AssemblyReferences
                .Where(assRef => assRef.Name == "mscorlib")
                .SingleOrDefault();

            assDef.Dispose();

            if (corLib == null)
                throw new Exceptions.NotNETAssemblyException(assemblyFilePath);

            return corLib.Version;
        }

        internal static void ReplaceNamespace(ref AssemblyDefinition assDef, string pattern, string replacement)
        {
            if (assDef == null)
            {
                VortexPatcher.Logger.Error("Invalid assembly definition", new NullReferenceException());
                return;
            }

            Regex rgx = new Regex(pattern);
            TypeDefinition modEntryDef = VortexPatcher.InstallerAssembly.MainModule.Types
                .Where(typ => typ.FullName == "VortexHarmonyInstaller.ModTypes.ModEntry")
                .SingleOrDefault();

            if (modEntryDef == null)
            {
                VortexPatcher.Logger.Error("Failed to load Vortex's ModEntry");
                return;
            }

            assDef.MainModule.ImportReference(modEntryDef);
            foreach (var item in assDef.MainModule.Types)
            {
                if (rgx.IsMatch(item.Namespace))
                    item.Namespace = item.Namespace.Replace(pattern, replacement);

                // This is disgusting! TODO: CLEAN THIS SHIT CODE
                TypeDefinition[] types = assDef.MainModule.Types.ToArray();
                foreach (var type in types)
                {
                    if (!type.HasMethods)
                        continue;

                    MethodDefinition[] meths = type.Methods.ToArray();
                    foreach (var meth in meths)
                    {
                        if (!meth.HasParameters)
                            continue;

                        ParameterDefinition[] parameters = meth.Parameters.ToArray();
                        ParameterDefinition[] filtered = parameters
                            .Where(para => para.ParameterType.FullName.Contains(Constants.UMM_TYPENAME))
                            .ToArray();

                        foreach (var para in filtered)
                        {
                            ParameterDefinition parameter = new ParameterDefinition(modEntryDef);
                            parameter.Name = para.Name;
                            meth.Parameters.Insert(para.Index, parameter);
                            meth.Parameters.RemoveAt(para.Index);
                        }
                    }
                }
            }
        }
    }

    class UMMModType : BaseModType, IModType
    {
        private UMMData Data { get { return m_ModData as UMMData; } }

        public bool ConvertAssemblyReferences(string dllPath)
        {
            VortexPatcher.Logger.Info($"Attempting to convert {Path.GetFileName(dllPath)}");
            if (!File.Exists(dllPath))
            {
                VortexPatcher.Logger.Error($"File is missing: { dllPath }");
                return false;
            }

            try
            {
                string tempFile = dllPath + Constants.VORTEX_TEMP_SUFFIX;
                string backUpFile = dllPath + Constants.VORTEX_BACKUP;
                if (!File.Exists(backUpFile))
                    Util.Backup(dllPath);

                File.Copy(dllPath, tempFile, true);
                AssemblyDefinition modAssembly = modAssembly = AssemblyDefinition.ReadAssembly(tempFile);

                var references = modAssembly.MainModule.AssemblyReferences;
                AssemblyNameReference ummRef = references.FirstOrDefault(res => res.Name == Constants.UMM_ASSEMBLY_REF_NAME);
                if (ummRef == null)
                {
                    m_ModAssembly = Assembly.LoadFile(dllPath);
                    throw new Exceptions.AssemblyIsInjectedException(dllPath);
                }

                AssemblyNameReference newRef = AssemblyNameReference.Parse(VortexPatcher.InstallerAssembly.FullName);
                int idx = modAssembly.MainModule.AssemblyReferences.IndexOf(ummRef);
                modAssembly.MainModule.AssemblyReferences.Insert(idx, newRef);
                modAssembly.MainModule.AssemblyReferences.Remove(ummRef);

                modAssembly.Write(dllPath);
                modAssembly.Dispose();

                File.Delete(tempFile);

                m_ModAssembly = Assembly.LoadFile(dllPath);

                return true;
            }
            catch (Exceptions.AssemblyIsInjectedException exc)
            {
                // We already dealt with this mod, no need to hijack its calls again.
                VortexPatcher.Logger.Debug(exc);
                return true;
            }
            catch (Exception exc)
            {
                Util.RestoreBackup(dllPath);
                VortexPatcher.Logger.Error("Assembly conversion failed", exc);
                return false;
            }
        }

        public string GetModName()
        {
            UMMData data = (m_ModData as UMMData);
            if (data == null)
                throw new NullReferenceException("Invalid UMM Data");

            return data.Base_Id;
        }

        public void InjectPatches()
        {
            try
            {
                if (null == m_ModData)
                    throw new NullReferenceException("Mod data is not available");

                UMMData data = (m_ModData as UMMData);
                if (null == data)
                    throw new NullReferenceException("Invalid UMM mod data");

                string strEntryMethod = data.EntryMethod;
                int idx = strEntryMethod.LastIndexOf('.');
                string strMethodName = strEntryMethod.Substring(idx + 1);
                string strClassName = strEntryMethod.Substring(0, idx);

                if (m_ModAssembly == null)
                {
                    VortexPatcher.Logger.Info("Mod assembly is null");
                    string modsFolder = Path.Combine(VortexPatcher.CurrentDataPath, "VortexMods");
                    string modAssemblyPath = Directory.GetFiles(modsFolder, "*.dll", SearchOption.AllDirectories)
                        .Where(file => file.EndsWith(data.AssemblyName))
                        .SingleOrDefault();
                    m_ModAssembly = Assembly.LoadFile(modAssemblyPath);
                }

                Type[] types = m_ModAssembly.GetTypes();
                Type type = types.Where(ty => ty.FullName == strClassName).SingleOrDefault();
                if (type == null)
                    throw new NullReferenceException("Failed to find entry Type in mod assembly");

                BindingFlags bindingFlags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
                MethodInfo methodInfo = type.GetMethod(strMethodName, bindingFlags);
                if (null == methodInfo)
                    throw new NullReferenceException("Failed to find entry Method in mod assembly");

                UnityModManagerNet.UnityModManager.ModEntry modEntry = UnityModManagerNet.UnityModManager.ModEntry.GetModEntry(data, m_ModAssembly.Location);
                object[] param = new object[] { modEntry };
                methodInfo.Invoke(null, param);

                AddExposedMod(modEntry);
            }
            catch (Exception exc)
            {
                VortexPatcher.Logger.Error("Failed to invoke starter method", exc);
                return;
            }
        }

        public bool ParseModData(string strManifestLoc)
        {
            ModDataContainer.RegisterType<IParsedModData, UMMData>();
            string strManifestPath = Path.Combine(strManifestLoc, Constants.UMM_MANIFEST_FILENAME);
            try {
                AssignManifestPath(strManifestPath);
            }
            catch (Exception exc) {
                VortexPatcher.Logger.Error("Failed to assign manifest", exc);
                return false;
            }
            
            ParseData(ModDataContainer.Resolve<UMMData>());
            return m_ModData != null;
        }
    }
}
