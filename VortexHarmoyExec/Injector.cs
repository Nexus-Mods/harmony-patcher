using Mono.Cecil;
using Mono.Cecil.Cil;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Harmony;
using VortexHarmonyInstaller;
using VortexHarmoyExec;
using System.Diagnostics;
using System.ComponentModel;
using System.Net.Http;
using System.Net;

namespace VortexHarmonyExec
{
    internal partial class Enums
    {
        internal enum EErrorCode
        {
            INVALID_ENTRYPOINT = -1,
            MISSING_FILE = -2,
            INVALID_ARGUMENT = -3,
            FILE_OPERATION_ERROR = -4,
            UNHANDLED_FILE_VERSION = -5,
            FAILED_DOWNLOAD = -6,
            UNKNOWN = -13,
        }

        internal enum EInjectorState
        {
            NONE,
            RUNNING,
            FINISHED,
        }
    }

    internal partial class Constants
    {
        // The game's assembly file.
        internal const string UNITY_ASSEMBLY_LIB = "Assembly-CSharp.dll";
        //internal const string UNITY_ASSEMBLY_LIB = "TestAssembly.dll";

        // Mods are going to be stored here.
        internal const string MODS_DIRNAME = "VortexMods";

        // Suffix identifying Vortex's backup files.
        internal const string VORTEX_BACKUP_TAG = "_vortex_assembly_backup";

        // The main patcher function we wish to inject.
        internal const string VORTEX_PATCH_METHOD = "VortexHarmonyInstaller.VortexPatcher::Patch";

        // The optional Unity GUI patcher fuction.
        internal const string VORTEX_UNITY_GUI_PATCH = "VortexUnity.VortexUnityManager::RunUnityPatcher";

        // Multilanguage Standard Common Object Runtime Library
        internal const string MSCORLIB = "mscorlib.dll";

        // Github location containing mscorlib replacements.
        internal const string GITHUB_LINK = "https://raw.githubusercontent.com/IDCs/mscorlib-replacements/master/";

        // The name of the bundled asset file.
        internal const string UI_BUNDLE_FILENAME = "vortexui";
    }

    internal partial class Util
    {
        internal static string GetTempFile(string strFilePath) 
        {
            string strDir = Path.GetDirectoryName(strFilePath);
            string strTempFileName = Path.GetFileName(Path.GetTempFileName());
            string strTempFilePath = Path.Combine(strDir, strTempFileName);
            File.Copy(strFilePath, Path.Combine(strDir, strTempFileName), true);
            return Path.Combine(strDir, strTempFileName);
        }

        // Compare 2 files.
        internal static bool AreIdentical(string file1, string file2)
        {
            int file1byte;
            int file2byte;
            FileStream fs1;
            FileStream fs2;

            // Same file ?
            if (file1 == file2)
                return true;
                      
            fs1 = new FileStream(file1, FileMode.Open);
            fs2 = new FileStream(file2, FileMode.Open);
            if (fs1.Length != fs2.Length)
            {
                // Length is different, obvs different files
                fs1.Close();
                fs2.Close();
                return false;
            }

            do 
            {
                file1byte = fs1.ReadByte();
                file2byte = fs2.ReadByte();
            }
            while ((file1byte == file2byte) && (file1byte != -1));
            fs1.Close();
            fs2.Close();

            return ((file1byte - file2byte) == 0);
        }

        /// <summary>
        /// Create a back up file for the provided file.
        /// </summary>
        /// <param name="strFilePath"></param>
        /// <returns></returns>
        internal static string BackupFile(string strFilePath, bool bForce = false)
        {
            string strBackupfile = strFilePath + Constants.VORTEX_BACKUP_TAG;
            if (!File.Exists(strFilePath))
                throw new FileNotFoundException(string.Format("{0} is missing", strFilePath));

            if (bForce) 
            {
                File.Copy(strFilePath, strBackupfile, true);
                return strBackupfile;
            }

            if (File.Exists(strBackupfile))
            {
                if (AreIdentical(strFilePath, strBackupfile))
                {
                    // Identical backup already exists.
                    return strBackupfile;
                }
                else 
                {
                    throw new IOException(string.Format("Backup failed - file \"{0}\" exists.", strBackupfile));
                }
            }

            File.Copy(strFilePath, strBackupfile);

            return strBackupfile;
        }

        /// <summary>
        /// Restore any back up files we may have created for the original
        ///  filename.
        /// </summary>
        /// <param name="strFilePath"></param>
        internal static void RestoreBackup(string strFilePath)
        {
            string strBackupFile = strFilePath + Constants.VORTEX_BACKUP_TAG;
            if (!File.Exists(strBackupFile))
            {
                string strResponse = JSONResponse.CreateSerializedResponse(
                    string.Format("Backup is missing {0}", strBackupFile),
                    Enums.EErrorCode.FILE_OPERATION_ERROR);
                Console.Error.WriteLine(strResponse);
                return;
            }

            try
            {
                File.Copy(strBackupFile, strFilePath, true);
                File.Delete(strBackupFile);
            }
            catch (Exception exc)
            {
                string strResponse = JSONResponse.CreateSerializedResponse(exc.Message, Enums.EErrorCode.FILE_OPERATION_ERROR, exc);
                Console.Error.WriteLine(strResponse);
            }
        }

        internal static void DeleteTemp(string strFilePath)
        {
            try
            {
                File.Delete(strFilePath);
            }
            catch (Exception exc)
            {
                string strMessage = "Failed to delete temporary file";
                string strResponse = JSONResponse.CreateSerializedResponse(strMessage, Enums.EErrorCode.UNKNOWN, exc);
                Console.Error.WriteLine(strResponse);
            }
        }

        internal static void ReplaceFile(string strOld, string strNew)
        {
            try
            {
                BackupFile(strOld, true);
                File.Delete(strOld);
                File.Copy(strNew, strOld);
            }
            catch (Exception exc)
            {
                RestoreBackup(strOld);
                string strMessage = "Failed to replace file";
                string strResponse = JSONResponse.CreateSerializedResponse(strMessage, Enums.EErrorCode.FILE_OPERATION_ERROR, exc);
                Console.Error.WriteLine(strResponse);
            }
        }

        /// <summary>
        /// Function will test if the game we're trying to inject to
        ///  is distributing a mscorlib.dll file and if so, we're going
        ///  to test whether reflection is enabled.
        /// </summary>
        /// <param name="strAssemblyPath"></param>
        /// <param name="strEntryPoint"></param>
        /// <param name="exception"></param>
        internal static bool IsReflectionEnabled(string strDataPath)
        {
            // This method will most definitely have to be enhanced as we encounter new
            //  situations where a game's mscorlib reflection functionality may have been disabled.
            const string ENTRY = "System.Reflection.Emit.AssemblyBuilder::DefineDynamicAssembly";

            bool bReflectionEnabled = true;
            if (!File.Exists(Path.Combine(strDataPath, Constants.MSCORLIB)))
            {
                // No custom corlib, safe to assume that reflection is enabled.
                return bReflectionEnabled;
            }

            string tempFile = GetTempFile(Path.Combine(strDataPath, Constants.MSCORLIB));
            string[] entryPoint = ENTRY.Split(new string[] { "::" }, StringSplitOptions.None);
            try
            {
                AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(tempFile);
                TypeDefinition type = assembly.MainModule.GetType(entryPoint[0]);
                if (null == type)
                    throw new NullReferenceException("Failed to find entry Type in mod assembly");

                MethodDefinition meth = type.Methods
                    .Where(method => method.Name.Contains(entryPoint[1]) && method.Parameters.Count == 2)
                    .FirstOrDefault();

                if (null == meth)
                    throw new NullReferenceException("Failed to find entry Method in mod assembly");

                Instruction instr = meth.Body.Instructions
                    .Where(instruction => instruction.ToString().Contains(nameof(PlatformNotSupportedException)))
                    .SingleOrDefault();

                bReflectionEnabled = (instr == null);

                assembly.Dispose();
            }
            catch (Exception exc)
            {
                bReflectionEnabled = false;
            }

            DeleteTemp(tempFile);
            return bReflectionEnabled;
        }
    }

    internal class MissingAssemblyResolver : BaseAssemblyResolver
    {
        private DefaultAssemblyResolver m_AssemblyResolver;
        private string m_strAssemblyPath;

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
            catch (AssemblyResolutionException ex)
            {
                string[] libraries = Directory.GetFiles(m_strAssemblyPath, "*.dll", SearchOption.AllDirectories);
                string missingLib = libraries.Where(lib => lib.Contains(name.Name)).SingleOrDefault();
                assembly = AssemblyDefinition.ReadAssembly(missingLib);
            }
            return assembly;
        }
    }

    internal class Injector
    {
        private static Enums.EInjectorState m_eInjectorState = Enums.EInjectorState.NONE;
        internal Enums.EInjectorState InjectorState { get { return m_eInjectorState; } }

        private static string m_strBundledAssetsDest;

        private bool m_bInjectGUI;
        private string m_strExtensionPath;
        private string m_strDataPath;
        private string m_strEntryPoint;
        private string m_strGameAssemblyPath;
        private string m_strModsDirectory;
        private MissingAssemblyResolver m_resolver = null;

        // Array of mono mscrolib replacements which will re-enable reflection
        //  for games that have them disabled.
        private readonly string[] LIB_REPLACEMENTS = new string[]
        {
            "mscorlib.dll.2.0.50727.1433",
            "mscorlib.dll.3.0.40818.0",
            "mscorlib.dll.4.6.57.0",
        };

        // Array of files we need to deploy/remove to/from the game's datapath.
        private static string[] _LIB_FILES = new string[] {
            "0Harmony.dll",
            "log4net.config",
            "log4net.dll",
            "Mono.Cecil.dll",
            "Mono.Cecil.Mdb.dll",
            "Mono.Cecil.Pdb.dll",
            "Mono.Cecil.Rocks.dll",
            "Newtonsoft.Json.dll",
            "ObjectDumper.dll",
            "Unity.Abstractions.dll",
            "Unity.Container.dll",
            "VortexHarmonyInstaller.dll",
        };

        public Injector(string strDataPath, string strEntryPoint)
        {
            try
            {
                m_strDataPath = (strDataPath.EndsWith(".dll"))
                    ? Path.GetDirectoryName(strDataPath)
                    : strDataPath;

                m_strBundledAssetsDest = Path.Combine(m_strDataPath, "VortexBundles", "UI");
                m_strExtensionPath = VortexHarmonyManager.ExtensionPath;
                m_strEntryPoint = strEntryPoint;

                m_strGameAssemblyPath = (strDataPath.EndsWith(".dll"))
                    ? strDataPath
                    : Path.Combine(strDataPath, Constants.UNITY_ASSEMBLY_LIB);

                if (!File.Exists(m_strGameAssemblyPath))
                    throw new FileNotFoundException($"{m_strGameAssemblyPath} does not exist");

                m_bInjectGUI = m_strGameAssemblyPath.EndsWith(Constants.UNITY_ASSEMBLY_LIB);
                if (m_bInjectGUI)
                {
                    Array.Resize(ref _LIB_FILES, _LIB_FILES.Length + 1);
                    _LIB_FILES[_LIB_FILES.Length - 1] = "VortexUnity.dll";
                }

                m_strModsDirectory = Path.Combine(m_strDataPath, Constants.MODS_DIRNAME);
                m_resolver = new MissingAssemblyResolver(m_strDataPath);
            }
            catch (Exception exc)
            {
                bool isAssemblyMissing = (exc is FileNotFoundException);
                Enums.EErrorCode errorCode = isAssemblyMissing
                    ? Enums.EErrorCode.MISSING_FILE
                    : Enums.EErrorCode.INVALID_ARGUMENT;
                string strMessage = isAssemblyMissing
                    ? "The game assembly is missing!"
                    : "Injector received invalid argument.";
                string strResponse = JSONResponse.CreateSerializedResponse(strMessage, errorCode, exc);
                Console.Error.WriteLine(strResponse);
                Environment.Exit((int)(errorCode));
            }
        }

        public void Inject()
        {
            m_eInjectorState = Enums.EInjectorState.RUNNING;
            string strTempFile = null;
            AssemblyDefinition unityAssembly = null;
            try
            {
                // Ensure we have reflection enabled - there's no point
                //  in continuing if reflection is disabled.
                if (!Util.IsReflectionEnabled(m_strDataPath))
                {
                    EnableReflection(m_strDataPath);
                }
                else
                    m_eInjectorState = Enums.EInjectorState.FINISHED;
                // Deploy patcher related files.
                DeployFiles();

                // Start the patching process.
                string[] unityPatcher = Constants.VORTEX_UNITY_GUI_PATCH.Split(new string[] { "::" }, StringSplitOptions.None);
                string[] patcherPoints = Constants.VORTEX_PATCH_METHOD.Split(new string[] { "::" }, StringSplitOptions.None);
                string[] entryPoint = m_strEntryPoint.Split(new string[] { "::" }, StringSplitOptions.None);

                strTempFile = Util.GetTempFile(m_strGameAssemblyPath);
                using (unityAssembly = AssemblyDefinition.ReadAssembly(strTempFile,
                    new ReaderParameters { ReadWrite = true, AssemblyResolver = m_resolver }))
                {
                    if (IsInjected(unityAssembly, entryPoint)) 
                    {
                        unityAssembly.Dispose();
                        Util.DeleteTemp(strTempFile);
                        return;
                    }

                    // Back up the game assembly before we do anything.
                    Util.BackupFile(m_strGameAssemblyPath, true);

                    AssemblyDefinition vrtxPatcher = AssemblyDefinition.ReadAssembly(Path.Combine(m_strDataPath, Constants.VORTEX_LIB));
                    MethodDefinition patcherMethod = vrtxPatcher.MainModule.GetType(patcherPoints[0]).Methods.First(x => x.Name == patcherPoints[1]);
                    TypeDefinition type = unityAssembly.MainModule.GetType(entryPoint[0]);
                    if ((type == null) || !type.IsClass)
                    {
                        throw new EntryPointNotFoundException("Invalid entry point");
                    }

                    MethodDefinition methodDefinition = type.Methods.FirstOrDefault(meth => meth.Name == entryPoint[1]);
                    if ((methodDefinition == null) || !methodDefinition.HasBody)
                    {
                        throw new EntryPointNotFoundException("Invalid entry point");
                    }

                    methodDefinition.Body.GetILProcessor().InsertBefore(methodDefinition.Body.Instructions[0], Instruction.Create(OpCodes.Call, methodDefinition.Module.ImportReference(patcherMethod)));
                    if (m_bInjectGUI)
                    {
                        try
                        {
                            AssemblyDefinition guiPatcher = AssemblyDefinition.ReadAssembly(Path.Combine(m_strDataPath, Constants.VORTEX_GUI_LIB));
                            MethodDefinition guiMethod = guiPatcher.MainModule.GetType(unityPatcher[0]).Methods.First(x => x.Name == unityPatcher[1]);
                            methodDefinition.Body.GetILProcessor().InsertBefore(methodDefinition.Body.Instructions[0], Instruction.Create(OpCodes.Call, methodDefinition.Module.ImportReference(guiMethod)));
                        }
                        catch (Exception exc)
                        {
                            throw new EntryPointNotFoundException("Unable to find/insert GUI patcher method definition", exc);
                        }
                    }

                    unityAssembly.Write(m_strGameAssemblyPath);
                    unityAssembly.Dispose();
                    Util.DeleteTemp(strTempFile);
                }
            }
            catch (Exception exc)
            {
                Enums.EErrorCode errorCode = Enums.EErrorCode.UNKNOWN;

                if (unityAssembly != null)
                    unityAssembly.Dispose();

                if (strTempFile != null)
                    Util.DeleteTemp(strTempFile);

                Util.RestoreBackup(m_strGameAssemblyPath);

                if (exc is FileNotFoundException) 
                    errorCode = Enums.EErrorCode.MISSING_FILE;
                else if (exc is EntryPointNotFoundException)
                    errorCode = Enums.EErrorCode.INVALID_ENTRYPOINT;

                string strMessage = "Failed to inject patcher.";
                string strResponse = JSONResponse.CreateSerializedResponse(strMessage, errorCode, exc);
                Console.Error.WriteLine(strResponse);
                Environment.Exit((int)(errorCode));
            }

            while (InjectorState != Enums.EInjectorState.FINISHED)
            {
                // Do nothing.
            }
        }

        /// <summary>
        /// Certain games distribute modified assemblies which disable
        ///  reflection and impede modding.
        /// </summary>
        private void EnableReflection(string strDataPath)
        {
            string strMscorlib = Path.Combine(strDataPath, Constants.MSCORLIB);
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(strMscorlib);
            string version = fvi.FileVersion;
            string strLib = LIB_REPLACEMENTS
                .Where(replacement => replacement.Substring(Constants.MSCORLIB.Length + 1, 1) == version.Substring(0, 1))
                .SingleOrDefault();

            if (null != strLib)
            {
                try
                {
                    WebClient wc = new WebClient();
                    Uri uri = new Uri(Constants.GITHUB_LINK + strLib);
                    wc.DownloadDataAsync(uri, Path.Combine(strDataPath, strLib));
                    wc.DownloadDataCompleted += (sender, e) =>
                    {
                        string strFileName = e.UserState.ToString();
                        File.WriteAllBytes(strFileName, e.Result);
                        Util.ReplaceFile(strMscorlib, strFileName);
                        m_eInjectorState = Enums.EInjectorState.FINISHED;
                    };
                }
                catch (Exception exc)
                {
                    m_eInjectorState = Enums.EInjectorState.FINISHED;
                    string strMessage = "Unhandled mscorlib version.";
                    Enums.EErrorCode err = Enums.EErrorCode.MISSING_FILE;
                    string strResponse = JSONResponse.CreateSerializedResponse(strMessage, err, exc);
                    Console.Error.WriteLine(strResponse);
                    Environment.Exit((int)(err));
                }
            }
            else
            {
                m_eInjectorState = Enums.EInjectorState.FINISHED;
                string strMessage = "Unhandled mscorlib version.";
                Enums.EErrorCode err = Enums.EErrorCode.UNHANDLED_FILE_VERSION;
                string strResponse = JSONResponse.CreateSerializedResponse(strMessage, err);
                Console.Error.WriteLine(strResponse);
                Environment.Exit((int)(err));
            }
        }

        public void RemovePatch()
        {
            try
            {
                PurgeFiles();
                string[] entryPoint = m_strEntryPoint.Split(new string[] { "::" }, StringSplitOptions.None);

                string strTempFile = m_strGameAssemblyPath + Constants.VORTEX_BACKUP_TAG;
                File.Copy(m_strGameAssemblyPath, strTempFile, true);

                using (AssemblyDefinition unityAssembly = AssemblyDefinition.ReadAssembly(strTempFile,
                    new ReaderParameters { ReadWrite = true, AssemblyResolver = m_resolver }))
                {
                    if (!IsInjected(unityAssembly, entryPoint))
                        return;

                    TypeDefinition type = unityAssembly.MainModule.GetType(entryPoint[0]);
                    if ((type == null) || !type.IsClass)
                    {
                        throw new EntryPointNotFoundException("Invalid type");
                    }

                    MethodDefinition methodDefinition = type.Methods.FirstOrDefault(meth => meth.Name == entryPoint[1]);
                    if ((methodDefinition == null) || !methodDefinition.HasBody)
                    {
                        throw new EntryPointNotFoundException("Invalid method");
                    }

                    var instructions = methodDefinition.Body.Instructions
                        .Where(instr => instr.OpCode == OpCodes.Call)
                        .ToArray();
                    Instruction patcherInstr = instructions.FirstOrDefault(instr =>
                        instr.Operand.ToString().Contains(Constants.VORTEX_PATCH_METHOD));

                    methodDefinition.Body.Instructions.Remove(patcherInstr);

                    if (m_bInjectGUI)
                    {
                        Instruction uiPatchInstr = instructions.FirstOrDefault(instr =>
                            instr.Operand.ToString().Contains(Constants.VORTEX_UNITY_GUI_PATCH));

                        if (uiPatchInstr != null)
                            methodDefinition.Body.Instructions.Remove(uiPatchInstr);
                    }

                    unityAssembly.Write(m_strGameAssemblyPath);
                }
            }
            catch (Exception exc)
            {
                Enums.EErrorCode errorCode = Enums.EErrorCode.UNKNOWN;
                Util.RestoreBackup(m_strGameAssemblyPath);
                if (exc is FileNotFoundException) 
                    errorCode = Enums.EErrorCode.MISSING_FILE;
                else if (exc is EntryPointNotFoundException)
                    errorCode = Enums.EErrorCode.INVALID_ENTRYPOINT;

                string strMessage = "Failed to remove patcher.";
                string strResponse = JSONResponse.CreateSerializedResponse(strMessage, errorCode, exc);
                Console.Error.WriteLine(strResponse);
                Environment.Exit((int)(errorCode));
            }
        }

        /// <summary>
        /// Creates the folder structure inside the game's
        ///  folder.
        /// </summary>
        internal void DeployFiles()
        {
            if (!Directory.Exists(m_strDataPath))
                throw new DirectoryNotFoundException(string.Format("Datapath {0} does not exist", m_strDataPath));

            string strLibPath = VortexHarmonyManager.InstallPath;
            Directory.CreateDirectory(m_strModsDirectory);
            string[] files = Directory.GetFiles(strLibPath, "*", SearchOption.TopDirectoryOnly)
                .Where(file => _LIB_FILES.Contains(Path.GetFileName(file)))
                .ToArray();

            foreach (string strFile in files)
                File.Copy(strFile, Path.Combine(m_strDataPath, Path.GetFileName(strFile)), true);

            if (m_bInjectGUI && (m_strExtensionPath != null))
            {
                string[] uiFiles = new string[] {
                    Path.Combine(m_strExtensionPath, Constants.UI_BUNDLE_FILENAME),
                    Path.Combine(m_strExtensionPath, Constants.UI_BUNDLE_FILENAME + ".manifest"),
                };

                try
                {
                    Directory.CreateDirectory(m_strBundledAssetsDest);
                    foreach (string file in uiFiles)
                    {
                        string strDest = Path.Combine(m_strBundledAssetsDest, Path.GetFileName(file));
                        File.Copy(file, strDest, true);
                    }
                }
                catch (Exception e)
                {
                    // This is fine, some extenions might not provide bundled UI assets.
                    //  all this means is that the in-game UI will not look that great.
                    string strMessage = "Extension path did not provide bundled UI assets";
                    string strResponse = JSONResponse.CreateSerializedResponse(strMessage, 0, e);
                    Console.Error.WriteLine(strResponse);
                }
            }
        }

        /// <summary>
        /// Removes Vortex libraries from the Vortex folder.
        /// </summary>
        internal void PurgeFiles()
        {
            string[] files = Directory.GetFiles(m_strDataPath, "*", SearchOption.TopDirectoryOnly)
                .Where(file => _LIB_FILES.Contains(Path.GetFileName(file)))
                .ToArray();

            foreach (string strFile in files) {
                try {
                    // Try to re-instate backups if they exist.
                    Util.RestoreBackup(strFile);
                }
                catch (Exception exc) {
                    if (exc is FileNotFoundException)
                        File.Delete(strFile);
                }
            }

            if (m_bInjectGUI && Directory.Exists(m_strBundledAssetsDest))
                Directory.Delete(m_strBundledAssetsDest, true);
        }

        /// <summary>
        /// Check whether the game assembly has been already patched.
        /// </summary>
        /// <returns>True if injected, false otherwise</returns>
        internal bool IsInjected(AssemblyDefinition unityAssembly, string[] entryPoint)
        {
            TypeDefinition type = unityAssembly.MainModule.GetType(entryPoint[0]);
            if ((type == null) || !type.IsClass)
            {
                throw new EntryPointNotFoundException("Invalid entry point type");
            }

            MethodDefinition methodDefinition = type.Methods.FirstOrDefault(meth => meth.Name == entryPoint[1]);
            if ((methodDefinition == null) || !methodDefinition.HasBody)
            {
                throw new EntryPointNotFoundException("Invalid entry point method");
            }

            Instruction[] instructions = methodDefinition.Body.Instructions.Where(instr => instr.OpCode == OpCodes.Call).ToArray();
            return instructions.FirstOrDefault(instr =>
                instr.Operand.ToString().Contains(Constants.VORTEX_PATCH_METHOD)) != null;
        }
    }
}
