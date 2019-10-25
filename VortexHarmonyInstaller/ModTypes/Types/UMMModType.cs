using Mono.Cecil;

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;

using Unity;

using VortexHarmonyInstaller.Util;
using System.Collections.Generic;

namespace VortexHarmonyInstaller.ModTypes
{
    internal partial class Constants
    {
        internal const string UMM_MANIFEST_FILENAME = "info.json";

        internal const string UMM_ASSEMBLY_REF_NAME = "UnityModManager";

        internal const string VORTEX_TEMP_SUFFIX = "_vortex_temporary";

        internal const string VORTEX_BACKUP = "_vortex_backup";

        // Namespace.Classname
        internal const string UMM_TYPENAME = "UnityModManagerNet.UnityModManager";
    }

    internal partial class Exceptions
    {
        internal class AssemblyIsInjectedException: Exception
        {
            internal AssemblyIsInjectedException(string strMessage)
                : base(strMessage) { }
        }
    }

    internal class UMMModType : BaseModType, IModType
    {
        private UMMData Data { get { return m_ModData as UMMData; } }

        public bool ConvertAssemblyReferences(string strDllPath)
        {
            try
            {
                if (!File.Exists(strDllPath))
                    throw new FileNotFoundException(strDllPath);

                string strBackUpFile = Util.Backup(strDllPath);
                string strTempFile = strDllPath + Constants.VORTEX_TEMP_SUFFIX;
                File.Copy(strDllPath, strTempFile, true);

                AssemblyDefinition modAssembly = AssemblyDefinition.ReadAssembly(strTempFile);

                var references = modAssembly.MainModule.AssemblyReferences;
                AssemblyNameReference ummRef = references.FirstOrDefault(res => res.Name == Constants.UMM_ASSEMBLY_REF_NAME);
                if (ummRef == null)
                {
                    // There's no assembly to convert - we're good to go.
                    return true;
                }

                AssemblyNameReference newRef = new AssemblyNameReference(
                    VortexPatcher.InstallerAssembly.Name.Name,
                    VortexPatcher.InstallerVersion);

                int idx = modAssembly.MainModule.AssemblyReferences.IndexOf(ummRef);
                modAssembly.MainModule.AssemblyReferences.Insert(idx, newRef);
                modAssembly.MainModule.AssemblyReferences.Remove(ummRef);
                modAssembly.Write(strDllPath);
                modAssembly.Dispose();

                // Disassemble and replace unwanted namespace/class calls.
                string strDisassembled = Disassembler.DisassembleFile(strDllPath);
                string strReplaced = Regex.Replace(strDisassembled, "UnityModManagerNet.UnityModManager", "VortexHarmonyInstaller.ModTypes");

                // Prepare for 
                File.Delete(strTempFile);
                File.Delete(strDllPath);

                // Re-assemble the mod file.
                File.WriteAllText(strTempFile, strReplaced);
                Assembler.AssembleFile(strTempFile, strDllPath);

                File.Delete(strTempFile);
                File.Delete(strBackUpFile);

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
                Util.RestoreBackup(strDllPath);
                VortexPatcher.Logger.Error("Assembly conversion failed", exc);
                return false;
            }
            //string strTempFile = strDllPath + Constants.VORTEX_TEMP_SUFFIX;
            //File.Copy(strDllPath, strTempFile, true);

            //// UMM mods reference the UnityModManagerNet.UnityModManager assembly.
            ////  we need to replace this reference with our own assembly.
            //using (AssemblyDefinition modAssembly = AssemblyDefinition.ReadAssembly(strTempFile, new ReaderParameters { ReadWrite = true }))
            //{
            //    var methodDefinitions = modAssembly.Modules.SelectMany(mod => ModuleDefinitionRocks.GetAllTypes(mod))
            //    .SelectMany(t => t.Methods)
            //    .Where(method => null != method.Body);

            //    // Get the type reference we want to inject.
            //    TypeDefinition modEntryTypeDef = Util.GetTypeDef(VortexPatcher.InstallerAssembly, "ModEntry");
            //    MethodDefinition modEntryCtor = Util.GetCtorDef(modEntryTypeDef);
            //    modAssembly.MainModule.ImportReference(modEntryTypeDef);

            //    foreach (MethodBody body in methodDefinitions.Select(m => m.Body))
            //    {
            //        ILProcessor processor = body.GetILProcessor();
            //        List<Instruction> instructions = body.Instructions
            //            .Where(instr => instr.ToString().Contains(Constants.UMM_TYPENAME))
            //            .ToList();

            //        foreach (var instr in instructions)
            //        {
            //            switch (instr.OpCode.Name)
            //            {
            //                case "ldftn":
            //                    // This is a Function/Method definition - replace parameter types.
            //                    MethodDefinition methDef = instr.Operand as MethodDefinition;
            //                    if (null != methDef)
            //                        Util.ConvertParameterTypes(methDef, modEntryTypeDef, "ModEntry");

            //                    break;
            //                case "newobj":
            //                    MethodReference methRef = instr.Operand as MethodReference;
            //                    if (null != methRef)
            //                        Util.ConvertGenericArguments(methRef, modEntryTypeDef);

            //                    break;
            //                case "stfld":
            //                    break;
            //                case "ldfld":
            //                    break;
            //                case "call":
            //                    break;

            //            }

            //            //string 
            //            //string newstring = what.Replace("UnityModManagerNet.UnityModManager", "VortexHarmonyInstaller.ModTypes");
            //            //var newVal = processor.Create(instr.OpCode, VortexPatcher.InstallerAssembly);
            //            //processor.Replace(instr, newVal);
            //            //var stringEndArg = GetStringArgument(instr);
            //            //var writeInstruction = processor.Create(OpCodes.Call, changeTextMethodRef);
            //            //processor.InsertAfter(stringEndArg, writeInstruction);
            //        }
            //    }
            //    if (modAssembly == null)
            //        throw new Exceptions.AssemblyReplacementException(
            //            String.Format("Failed to read assembly: {0}", strDllPath));

            //    var references = modAssembly.MainModule.AssemblyReferences;
            //    AssemblyNameReference ummRef = references.FirstOrDefault(res => res.Name == Constants.UMM_ASSEMBLY_REF_NAME);
            //    if (ummRef == null)
            //    {
            //        // There's no assembly to convert - we're good to go.
            //        return true;
            //    }

            //    AssemblyNameReference newRef = new AssemblyNameReference(
            //        VortexPatcher.InstallerAssembly.Name.Name, 
            //        VortexPatcher.InstallerVersion);

            //    int idx = modAssembly.MainModule.AssemblyReferences.IndexOf(ummRef);
            //    modAssembly.MainModule.AssemblyReferences.Insert(idx, newRef);
            //    modAssembly.MainModule.AssemblyReferences.Remove(ummRef);
            //    modAssembly.Write(strDllPath);

            //    return true;
            //}
        }

        public string GetModName()
        {
            if (Data == null)
                throw new InvalidDataException("Invalid UMM Data");

            return Data.Base_Id;
        }

        public void InjectPatches()
        {
            try
            {
                if (null == m_ModData)
                throw new ArgumentNullException("Mod data is not available");

                UMMData data = (m_ModData as UMMData);
                if (null == data)
                    throw new InvalidDataException("Invalid UMM mod data");

                string strEntryMethod = (m_ModData as UMMData).EntryMethod;
                int idx = strEntryMethod.LastIndexOf('.');
                string strMethodName = strEntryMethod.Substring(idx + 1);
                string strClassName = strEntryMethod.Substring(0, idx);

                Type type = m_ModAssembly.GetType(strClassName);
                if (null == type)
                    throw new NullReferenceException("Failed to find entry Type in mod assembly");

                MethodInfo methodInfo = type.GetMethod(strMethodName);
                if (null == methodInfo)
                    throw new NullReferenceException("Failed to find entry Method in mod assembly");

                ModEntry modEntry = ModEntry.GetModEntry(data, m_ModAssembly.Location);
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
            try { AssignManifestPath(strManifestPath); }
            catch (Exception exc) { return false; }

            ParseData(ModDataContainer.Resolve<UMMData>());
            return m_ModData != null;
        }
    }
}
