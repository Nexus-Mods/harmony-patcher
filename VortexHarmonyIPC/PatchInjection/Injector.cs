using Mono.Cecil;
using Mono.Cecil.Cil;

using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using VortexInjectorIPC.Types;
using VortexInjectorIPC.Patches;

namespace VortexInjectorIPC {
    public partial class Constants {
        // The game's assembly file.
        public const string UNITY_ASSEMBLY_LIB = "Assembly-CSharp.dll";

        // Suffix identifying Vortex's backup files.
        public const string VORTEX_BACKUP_TAG = ".vortex_backup";

        // The main patcher function we wish to inject.
        public const string VORTEX_PATCH_METHOD = "VortexHarmonyInstaller.VortexPatcher::Patch";

        // The optional Unity GUI patcher fuction.
        public const string VORTEX_UNITY_GUI_PATCH = "VortexUnity.VortexUnityManager::RunUnityPatcher";
    }

    internal partial class Util {
        internal static void Dispose<T>(ref T t)
        {
            if (EqualityComparer<T>.Default.Equals (t, default (T))) {
                return;
            }

            IDisposable disposable = t as IDisposable;
            if (disposable != null) {
                disposable.Dispose ();
                t = default(T);
            }
        }

        internal static bool IsSymlink (string filePath)
        {
            FileInfo pathInfo = new FileInfo (filePath);
            return pathInfo.Attributes.HasFlag (FileAttributes.ReparsePoint);
        }

        internal static string GetTempFile (string strFilePath)
        {
            string strDir = Path.GetDirectoryName (strFilePath);
            string strTempFileName = Path.GetFileName (Path.GetTempFileName ());
            string strTempFilePath = Path.Combine (strDir, strTempFileName);
            File.Copy (strFilePath, Path.Combine (strDir, strTempFileName), true);
            return Path.Combine (strDir, strTempFileName);
        }

        // Compare 2 files.
        internal static bool AreIdentical (string file1, string file2)
        {
            int file1byte;
            int file2byte;
            FileStream fs1;
            FileStream fs2;

            // Same file ?
            if (file1 == file2)
                return true;

            fs1 = new FileStream (file1, FileMode.Open);
            fs2 = new FileStream (file2, FileMode.Open);
            if (fs1.Length != fs2.Length) {
                // Length is different, obvs different files
                fs1.Close ();
                fs2.Close ();
                return false;
            }

            do {
                file1byte = fs1.ReadByte ();
                file2byte = fs2.ReadByte ();
            }
            while ((file1byte == file2byte) && (file1byte != -1));
            fs1.Close ();
            fs2.Close ();

            return ((file1byte - file2byte) == 0);
        }

        /// <summary>
        /// Create a back up file for the provided file.
        /// </summary>
        /// <param name="strFilePath"></param>
        /// <returns></returns>
        internal static string BackupFile (string strFilePath, bool bForce = false)
        {
            string strBackupfile = strFilePath + Constants.VORTEX_BACKUP_TAG;
            if (!File.Exists (strFilePath))
                throw new FileNotFoundException (string.Format ("{0} is missing", strFilePath));

            if (bForce) {
                File.Copy (strFilePath, strBackupfile, true);
                return strBackupfile;
            }

            if (File.Exists (strBackupfile)) {
                if (AreIdentical (strFilePath, strBackupfile)) {
                    // Identical backup already exists.
                    return strBackupfile;
                } else {
                    throw new IOException (string.Format ("Backup failed - file \"{0}\" exists.", strBackupfile));
                }
            }

            File.Copy (strFilePath, strBackupfile);

            return strBackupfile;
        }

        /// <summary>
        /// Restore any back up files we may have created for the original
        ///  filename.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="reportError"></param>
        internal static void RestoreBackup (string filePath, bool reportError = true)
        {
            string backupFile = filePath + Constants.VORTEX_BACKUP_TAG;
            if (!File.Exists (backupFile)) {
                if (!reportError)
                    return;
                throw new FileNotFoundException ("Backup is missing", backupFile);
            }

            try {
                File.Copy (backupFile, filePath, true);
                File.Delete (backupFile);
            } catch (Exception exc) {
                if (!reportError)
                    return;

                throw exc;
            }
        }

        internal static void DeleteTemp (string filePath)
        {
            try {
                File.Delete (filePath);
            } catch (Exception exc) {
                throw new IOException ("Failed to delete temporary file", exc);
            }
        }

        internal static void ReplaceFile (string strOld, string strNew)
        {
            try {
                BackupFile (strOld, true);
                File.Delete (strOld);
                File.Copy (strNew, strOld);
            } catch (Exception exc) {
                RestoreBackup (strOld);
                throw new IOException ("Failed to replace file", exc);
            }
        }
    }

    internal class EntryPointInjectedException: Exception {
        public EntryPointInjectedException (string message)
        : base (message)
        {
        }
    }

    internal class MissingAssemblyResolver: BaseAssemblyResolver {
        private DefaultAssemblyResolver m_assemblyResolver;
        private readonly string [] m_assemblyPaths;

        public MissingAssemblyResolver (string [] assemblyPaths)
        {
            m_assemblyResolver = new DefaultAssemblyResolver ();

            foreach (string assPath in assemblyPaths)
                m_assemblyResolver.AddSearchDirectory (assPath);

            m_assemblyPaths = assemblyPaths;
        }

        public override AssemblyDefinition Resolve (AssemblyNameReference name)
        {
            Console.WriteLine (name.ToString ());
            AssemblyDefinition assembly = null;
            try { assembly = m_assemblyResolver.Resolve (name); } catch (AssemblyResolutionException) {
                foreach (string assPath in m_assemblyPaths) {
                    string [] libraries = Directory.GetFiles (assPath, "*.dll", SearchOption.AllDirectories);
                    string missingLib = libraries.Where (lib => lib.Contains (name.Name)).SingleOrDefault ();
                    Console.WriteLine (missingLib != null ? "found" : "not found");
                    if (missingLib != null)
                        return AssemblyDefinition.ReadAssembly (missingLib);
                }
            }

            return assembly;
        }
    }

    internal class PatchConfig: IPatchConfig {
        private string mCommand;
        public string Command => mCommand;

        private string mExtensionPath;
        public string ExtensionPath => mExtensionPath;

        private EntryPoint mSource = null;
        public IEntryPoint SourceEntryPoint => mSource;

        private EntryPoint [] mTargets = null;
        public IEntryPoint [] TargetEntryPoints => mTargets;

        public PatchConfig (JObject data)
        {
            mCommand = data ["Command"].ToString ();
            mExtensionPath = data ["ExtensionPath"].ToString ();
            mSource = new EntryPoint (data ["SourceEntryPoint"]);
            mTargets = data ["TargetEntryPoints"].Select (entry => new EntryPoint (entry))
                                                 .ToArray ();
        }
    }

    public sealed class Injector {
        private static readonly Lazy<Injector> sInjector =
            new Lazy<Injector> (() => new Injector ());

        public static Injector Instance { get { return sInjector.Value; } }

        // Will attempt to resolve missing assemblies, this needs
        //  to point to the game directory containing all game assemblies -
        //  most importantly mscorlib.dll
        private MissingAssemblyResolver m_resolver = null;

        private Injector () { }

        async internal Task<Dictionary<string, object>> IsPatchApplicable (JObject data, ProgressDelegate progressDelegate, CoreDelegates coreDelegates)
        {
            AssemblyDefinition assembly = null;
            EntryPointDefinitions sourceDefs = null;
            EntryPointDefinitions targetDefs = null;
            string tempFile = string.Empty;
            try {
                PatchConfig config = new PatchConfig ((JObject)data["patchConfig"]);
                EntryPoint sourcePoint = config.SourceEntryPoint as EntryPoint;
                if (!File.Exists (sourcePoint.AssemblyPath))
                    throw new FileNotFoundException ($"{sourcePoint.AssemblyPath} does not exist");

                sourceDefs = sourcePoint.GetDefinitions () as EntryPointDefinitions;
                if (!sourceDefs.IsEntryPointValid)
                    throw new EntryPointNotFoundException ($"Unable to find {sourcePoint.AssemblyPath}_{sourcePoint.ToString ()} target entry point. Expected format is Namespace.className::methodName");

                string dataPath = await coreDelegates.context.GetDataPath ();
                string VMLPath = await coreDelegates.context.GetModLoaderPath ();

                foreach (EntryPoint point in config.TargetEntryPoints) {
                    if (!File.Exists (point.AssemblyPath))
                        throw new FileNotFoundException ($"{point.AssemblyPath} does not exist");

                    // TODO: Might have to change this to only use the entry point's dependency path.
                    //  More testing is needed.
                    string [] assemblyResolverPaths = (dataPath != VMLPath)
                        ? new string [] { point.DependencyPath, dataPath, VMLPath }
                        : new string [] { point.DependencyPath, dataPath };

                    targetDefs = point.GetDefinitions () as EntryPointDefinitions;
                    if (!targetDefs.IsEntryPointValid)
                        throw new EntryPointNotFoundException ($"Unable to find {point.AssemblyPath}{point.ToString ()} target entry point. Expected format is Namespace.className::methodName");

                    m_resolver = new MissingAssemblyResolver (assemblyResolverPaths);

                    tempFile = Util.GetTempFile (point.AssemblyPath);
                    using (assembly = AssemblyDefinition.ReadAssembly (tempFile,
                        new ReaderParameters { AssemblyResolver = m_resolver })) {
                        if (IsInjected (assembly, config.SourceEntryPoint, point))
                            throw new EntryPointInjectedException ($"{point.AssemblyPath}_{point.ToString()} is already injected");
                    }

                    Util.Dispose (ref assembly);
                    Util.Dispose (ref targetDefs);
                    Util.DeleteTemp (tempFile);
                    tempFile = string.Empty;
                }
            } catch (Exception exc) {
                Util.Dispose (ref assembly);
                Util.Dispose (ref sourceDefs);
                Util.Dispose (ref targetDefs);

                if (tempFile != string.Empty) {
                    Util.DeleteTemp (tempFile);
                    tempFile = string.Empty;
                }

                return PatchHelper.CreatePatchResult (false, exc.Message);
            }

            return PatchHelper.CreatePatchResult (true, "Patch is applicable");
        }

        async internal Task<Dictionary<string, object>> ApplyPatch (JObject data, ProgressDelegate progressDelegate, CoreDelegates coreDelegates)
        {
            AssemblyDefinition assembly = null;
            EntryPointDefinitions sourceDefs = null;
            string tempFile = string.Empty;
            try {
                PatchConfig config = new PatchConfig ((JObject)data["patchConfig"]);
                EntryPoint sourcePoint = config.SourceEntryPoint as EntryPoint;
                if (!File.Exists (sourcePoint.AssemblyPath))
                    throw new FileNotFoundException ($"{sourcePoint.AssemblyPath} does not exist");

                sourceDefs = sourcePoint.GetDefinitions () as EntryPointDefinitions;
                if (!sourceDefs.IsEntryPointValid)
                    throw new EntryPointNotFoundException ($"Unable to find {sourcePoint.AssemblyPath}_{sourcePoint.ToString ()} source entry point. Expected format is Namespace.className::methodName");

                string dataPath = await coreDelegates.context.GetDataPath ();
                string VMLPath = await coreDelegates.context.GetModLoaderPath ();

                foreach (EntryPoint point in config.TargetEntryPoints) {
                    if (!File.Exists(point.AssemblyPath))
                        throw new FileNotFoundException ($"{point.AssemblyPath} does not exist");
                    // TODO: Might have to change this to only use the entry point's dependency path.
                    //  More testing is needed.
                    string [] assemblyResolverPaths = (dataPath != VMLPath)
                        ? new string [] { point.DependencyPath, dataPath, VMLPath }
                        : new string [] { point.DependencyPath, dataPath };

                    m_resolver = new MissingAssemblyResolver (assemblyResolverPaths);

                    tempFile = Util.GetTempFile (point.AssemblyPath);
                    using (assembly = AssemblyDefinition.ReadAssembly (tempFile,
                        new ReaderParameters { AssemblyResolver = m_resolver })) {
                        if (IsInjected (assembly, config.SourceEntryPoint, point))
                            throw new EntryPointInjectedException ($"{point.AssemblyPath}_{point.ToString ()} is already injected");

                        TypeDefinition typeDef = assembly.MainModule.GetType (point.TypeName);
                        MethodDefinition methDef = typeDef.Methods.First (x => x.Name == point.MethodName);

                        ILProcessor ilProcessor = methDef.Body.GetILProcessor ();
                        if (sourcePoint.ExpandoObjectData != string.Empty) {
                            ilProcessor.InsertBefore (methDef.Body.Instructions [0], Instruction.Create (OpCodes.Ldstr, sourcePoint.ExpandoObjectData));
                            ilProcessor.InsertBefore (methDef.Body.Instructions [1], Instruction.Create (OpCodes.Call, methDef.Module.ImportReference (sourceDefs.MethodDef)));
                        } else {
                            ilProcessor.InsertBefore (methDef.Body.Instructions [0], Instruction.Create (OpCodes.Call, methDef.Module.ImportReference (sourceDefs.MethodDef)));
                        }

                        assembly.Write (point.AssemblyPath);
                    }

                    Util.Dispose (ref assembly);
                    Util.DeleteTemp (tempFile);
                    tempFile = string.Empty;
                }

                Util.Dispose (ref sourceDefs);
            } catch (Exception exc) {
                Util.Dispose (ref assembly);
                Util.Dispose (ref sourceDefs);

                if (tempFile != string.Empty) {
                    Util.DeleteTemp (tempFile);
                    tempFile = string.Empty;
                }

                return PatchHelper.CreatePatchResult (false, exc.Message);
            }

            return PatchHelper.CreatePatchResult (true, "Patch Applied");
        }

        async internal Task<Dictionary<string, object>> RemovePatch (JObject data, ProgressDelegate progressDelegate, CoreDelegates coreDelegates)
        {
            AssemblyDefinition assembly = null;
            EntryPointDefinitions sourceDefs = null;
            string tempFile = string.Empty;

            // This should rarely be used when deploying VML as Vortex itself would remove the
            //  the assemblies when the user decides to purge.
            try {
                PatchConfig config = new PatchConfig ((JObject)data ["patchConfig"]);
                EntryPoint sourcePoint = config.SourceEntryPoint as EntryPoint;
                if (!File.Exists (sourcePoint.AssemblyPath))
                    throw new FileNotFoundException ($"{sourcePoint.AssemblyPath} does not exist");

                sourceDefs = sourcePoint.GetDefinitions () as EntryPointDefinitions;
                if (!sourceDefs.IsEntryPointValid)
                    throw new EntryPointNotFoundException ($"Unable to find {sourcePoint.AssemblyPath}_{sourcePoint.ToString ()} source entry point. Expected format is Namespace.className::methodName");

                string dataPath = await coreDelegates.context.GetDataPath ();
                string VMLPath = await coreDelegates.context.GetModLoaderPath ();

                foreach (EntryPoint point in config.TargetEntryPoints) {
                    if (!File.Exists(point.AssemblyPath))
                        throw new FileNotFoundException ($"{point.AssemblyPath} does not exist");

                    // TODO: Might have to change this to only use the entry point's dependency path.
                    //  More testing is needed.
                    string [] assemblyResolverPaths = (dataPath != VMLPath)
                        ? new string [] { point.DependencyPath, dataPath, VMLPath }
                        : new string [] { point.DependencyPath, dataPath };

                    m_resolver = new MissingAssemblyResolver (assemblyResolverPaths);

                    tempFile = Util.GetTempFile (point.AssemblyPath);
                    using (assembly = AssemblyDefinition.ReadAssembly (tempFile,
                    new ReaderParameters { ReadWrite = true, AssemblyResolver = m_resolver })) {
                        // If the assembly is not injected - there's nothing to remove.
                        //  This shouldn't be treated as an error although we do break
                        //  out using an exception.
                        if (!IsInjected (assembly, sourcePoint, point))
                            throw new EntryPointInjectedException ($"{point.AssemblyPath}_{point.ToString()} is not injected.");

                        TypeDefinition typeDef = assembly.MainModule.GetType (point.TypeName);
                        MethodDefinition methDef = typeDef.Methods.First (x => x.Name == point.MethodName);

                        var instructions = methDef.Body.Instructions
                            .Where (instr => instr.OpCode == OpCodes.Call)
                            .ToArray ();
                        Instruction patchInstr = instructions.FirstOrDefault (instr =>
                             instr.Operand.ToString ().Contains (point.ToString ()));

                        methDef.Body.Instructions.Remove (patchInstr);

                        assembly.Write (point.AssemblyPath);
                    }

                    Util.Dispose (ref assembly);
                    Util.DeleteTemp (tempFile);
                    tempFile = string.Empty;
                }
                Util.Dispose (ref sourceDefs);
            } catch (Exception exc) {
                Util.Dispose (ref assembly);
                Util.Dispose (ref sourceDefs);

                if (tempFile != string.Empty) {
                    Util.DeleteTemp (tempFile);
                    tempFile = string.Empty;
                }

                return PatchHelper.CreatePatchResult (false, exc.Message);
            }

            return PatchHelper.CreatePatchResult (true, "Patch removed successfully");
        }

        /// <summary>
        /// Check whether the game assembly has been already patched.
        /// </summary>
        /// <returns>True if injected, false otherwise</returns>
        internal bool IsInjected (AssemblyDefinition assembly, IEntryPoint sourcePoint, IEntryPoint targetPoint)
        {
            TypeDefinition type = assembly.MainModule.GetType (targetPoint.TypeName);
            if ((type == null) || !type.IsClass) {
                throw new EntryPointNotFoundException ("Invalid entry point type");
            }

            MethodDefinition methodDefinition = type.Methods.FirstOrDefault (meth => meth.Name == targetPoint.MethodName);
            if ((methodDefinition == null) || !methodDefinition.HasBody) {
                throw new EntryPointNotFoundException ("Invalid entry point method");
            }

            Instruction [] instructions = methodDefinition.Body.Instructions.Where (instr => instr.OpCode == OpCodes.Call).ToArray ();
            return instructions.FirstOrDefault (instr =>
                 instr.Operand.ToString ().Contains (sourcePoint.ToString())) != null;
        }
    }
}
