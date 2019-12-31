using Microsoft.Practices.Unity;

using Mono.Cecil;

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;

using static UnityModManagerNet.UnityModManager;
using System.Collections.Generic;

namespace VortexHarmonyInstaller.ModTypes
{
    internal partial class Constants
    {
        internal const string IL_EXT = ".il";

        internal const string UMM_MANIFEST_FILENAME = "Info.json";

        internal const string VIGO_FILENAME = "VortexUnity.dll";

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
            internal AssemblyIsInjectedException(string message)
                : base(message) { }
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

            string tempFile = dllPath + Constants.VORTEX_TEMP_SUFFIX;
            string instructionsFile = null;
            try
            {
                string backUpFile = dllPath + Constants.VORTEX_BACKUP;
                if (!File.Exists(backUpFile))
                    Util.Backup(dllPath);

                // Create a temporary file for us to manipulate.
                File.Copy(dllPath, tempFile, true);

                // UMM mods need to have an assembly reference to the VortexUnity.dll file
                //  containing UI specific functionality.
                AssemblyDefinition vigoDef = AssemblyDefinition.ReadAssembly(Path.Combine(VortexPatcher.CurrentDataPath, Constants.VIGO_FILENAME));
                AssemblyNameReference vigoRef = AssemblyNameReference.Parse(vigoDef.FullName);
                string vigoName = vigoDef.Name.Name;
                AssemblyDefinition modAssembly = AssemblyDefinition.ReadAssembly(tempFile);

                // Add the reference to VIGO.
                modAssembly.MainModule.ImportReference(vigoRef.GetType());
                modAssembly.MainModule.AssemblyReferences.Add(vigoRef);
                var references = modAssembly.MainModule.AssemblyReferences;

                // Find the UMM reference
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
                vigoDef.Dispose();

                File.Copy(dllPath, tempFile, true);

                // We're going to re-assemble the mod file; to do this we need to find out which
                //  .NET assembler to use (version is important)
                Version assemblerVersion = Util.GetFrameworkVer(dllPath);

                // Disassemble and extract any embedded resource files we can find.
                string disassembled = VortexHarmonyInstaller.Util.Disassembler.DisassembleFile(tempFile, true);

                // Find the reference id for VIGO within the assembly, we're
                //  going to use this value to replace the existing UMM refId.
                string pattern = @"(.assembly extern )(\/.*\/)( VortexUnity)";
                string refId = Regex.Match(disassembled, pattern).Groups[2].Value;

                // UMM distributes 2 nearly identical Harmony assemblies
                //  one which seems to reference GAC assemblies directly (Harmony12),
                //  and the normally distributed assembly which will look for assemlies
                //  locally before resorting to GAC (An optimization?)
                //  Either way, we're going to use the assembly we distribute instead and
                //  remove Harmony12 if we find it.
                disassembled = disassembled.Replace("Harmony12", "Harmony");
                
                // This is one UGLY pattern but will have to do in the meantime.
                //  We use regex to find all UI calls which will now point to VortexHarmonyInstaller
                //  as we replaced the UMM reference, and re-point the instruction to VortexUnity
                //  TODO: There must be a better way of doing this - more research is needed.
                pattern = @"(VortexHarmonyInstaller)(\/\*.*?\*\/)(\]UnityModManagerNet\.UnityModManager\/\*.*?\*\/\/UI)";
                disassembled = Regex.Replace(disassembled, pattern, m => vigoName + refId + m.Groups[3].Value);
                instructionsFile = tempFile + Constants.IL_EXT;

                // Write the data to an IL file and delete the current dll file
                //  as we want to replace it. We backed up this file earlier so we should be fine.
                File.WriteAllText(instructionsFile, disassembled);
                File.Delete(dllPath);

                // Generate the new Assembly!
                VortexHarmonyInstaller.Util.Assembler.AssembleFile(instructionsFile, dllPath, assemblerVersion);

                // Ensure that the mod is aware of its assembly.
                m_ModAssembly = Assembly.LoadFile(dllPath);
                VortexPatcher.Logger.Info($"Assembly {Path.GetFileName(dllPath)} converted successfully.");
                return true;
            }
            catch (Exceptions.AssemblyIsInjectedException)
            {
                // We already dealt with this mod, no need to hijack its calls again.
                VortexPatcher.Logger.Info($"Assembly {Path.GetFileName(dllPath)} is already patched.");
                return true;
            }
            catch (Exception exc)
            {
                Util.RestoreBackup(dllPath);
                VortexPatcher.Logger.Error("Assembly conversion failed", exc);
                return false;
            }
            finally
            {
                // Cleanup
                AssemblyDefinition modAssembly = AssemblyDefinition.ReadAssembly(tempFile);
                if (modAssembly.MainModule.HasResources)
                {
                    Resource[] resources = modAssembly.MainModule.Resources.Where(res => res.ResourceType == ResourceType.Embedded).ToArray();
                    foreach(Resource res in resources)
                    {
                        string possiblePath = Path.Combine(Path.GetDirectoryName(dllPath), res.Name);
                        if (File.Exists(possiblePath))
                            File.Delete(possiblePath);
                    }
                }

                modAssembly.Dispose();
                modAssembly = null;

                // We have no guarantee that the file reference has been released yet
                //  but we can try to delete anyway.
                Util.TryDelete(tempFile);

                if ((instructionsFile != null) && (File.Exists(instructionsFile)))
                    Util.TryDelete(instructionsFile);
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
                    string modAssemblyPath = Directory.GetFiles(VortexPatcher.CurrentModsPath, "*.dll", SearchOption.AllDirectories)
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

                ModEntry modEntry = ModEntry.GetModEntry(data, m_ModAssembly.Location);
                object[] param = new object[] { modEntry };
                methodInfo.Invoke(null, param);

                AddExposedMod(modEntry);
            } catch (ReflectionTypeLoadException ex) {
                foreach(Exception inner in ex.LoaderExceptions) {
                    VortexPatcher.Logger.Error($"Loader exception: {inner.Message}", inner);
                }
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

        public string[] GetDependencies()
        {
            List<string> dependencies = new List<string>();
            if ((Data != null) && (Data.Base_Dependencies != null))
                dependencies.Concat(Data.Requirements);

            return dependencies.ToArray();
        }
    }
}
