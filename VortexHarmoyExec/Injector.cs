using Mono.Cecil;
using Mono.Cecil.Cil;

using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Net;

namespace VortexHarmonyExec
{
    internal partial class Enums
    {
        internal enum EErrorCode
        {
            SAFE_TO_IGNORE = 0,
            INVALID_ENTRYPOINT = -1,
            MISSING_FILE = -2,
            INVALID_ARGUMENT = -3,
            FILE_OPERATION_ERROR = -4,
            UNHANDLED_FILE_VERSION = -5,
            FAILED_DOWNLOAD = -6,
            MISSING_ASSEMBLY_REF = -7,
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

        // Suffix identifying Vortex's backup files.
        internal const string VORTEX_BACKUP_TAG = ".vortex_backup";

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
        internal static bool IsSymlink(string filePath)
        {
            FileInfo pathInfo = new FileInfo(filePath);
            return pathInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);
        }

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
        /// <param name="filePath"></param>
        /// <param name="reportError"></param>
        internal static void RestoreBackup(string filePath, bool reportError = true)
        {
            string backupFile = filePath + Constants.VORTEX_BACKUP_TAG;
            if (!File.Exists(backupFile))
            {
                if (!reportError)
                    return;

                string response = JSONResponse.CreateSerializedResponse(
                    string.Format("Backup is missing {0}", backupFile),
                    Enums.EErrorCode.FILE_OPERATION_ERROR);
                Console.Error.WriteLine(response);
                return;
            }

            try
            {
                File.Copy(backupFile, filePath, true);
                File.Delete(backupFile);
            }
            catch (Exception exc)
            {
                if (!reportError)
                    return;

                string response = JSONResponse.CreateSerializedResponse(exc.Message, Enums.EErrorCode.FILE_OPERATION_ERROR, exc);
                Console.Error.WriteLine(response);
            }
        }

        internal static void DeleteTemp(string filePath)
        {
            try
            {
                File.Delete(filePath);
            }
            catch (Exception exc)
            {
                string message = "Failed to delete temporary file";
                string response = JSONResponse.CreateSerializedResponse(message, Enums.EErrorCode.UNKNOWN, exc);
                Console.Error.WriteLine(response);
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
        /// <param name="dataPath"></param>
        internal static bool IsReflectionEnabled(string dataPath)
        {
            // This method will most definitely have to be enhanced as we encounter new
            //  situations where a game's mscorlib reflection functionality may have been disabled.
            const string ENTRY = "System.Reflection.Emit.AssemblyBuilder::DefineDynamicAssembly";
            string corLib = Path.Combine(dataPath, Constants.MSCORLIB);
            bool reflectionEnabled = true;
            if (!File.Exists(corLib))
            {
                // No custom corlib, safe to assume that reflection is enabled.
                return reflectionEnabled;
            }

            string tempFile = string.Empty;
            try
            {
                tempFile = GetTempFile(Path.Combine(dataPath, Constants.MSCORLIB));
            }
            catch (Exception exc)
            {
                // Can't find the corlib - might be down to it being a symlink, try to get the backup.
                string message = JSONResponse.CreateSerializedResponse("", Enums.EErrorCode.FILE_OPERATION_ERROR, exc);
                Console.WriteLine(message);
                tempFile = GetTempFile(Path.Combine(dataPath, Constants.MSCORLIB + Constants.VORTEX_BACKUP_TAG));
            }

            string[] entryPoint = ENTRY.Split(new string[] { "::" }, StringSplitOptions.None);
            try
            {
                AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(tempFile);
                if (assembly.Name.Version.Major <= 3)
                {
                    // There doesn't seem to be a reason to replace the corlib
                    //  for older .NET versions.
                    assembly.Dispose();
                    return true;
                }
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

                reflectionEnabled = (instr == null);

                assembly.Dispose();
            }
            catch (Exception)
            {
                reflectionEnabled = false;
            }

            DeleteTemp(tempFile);
            return reflectionEnabled;
        }
    }

    internal class MissingAssemblyResolver : BaseAssemblyResolver
    {
        private DefaultAssemblyResolver m_assemblyResolver;
        private readonly string[] m_assemblyPaths;

        public MissingAssemblyResolver(string[] assemblyPaths)
        {
            m_assemblyResolver = new DefaultAssemblyResolver();

            foreach (string assPath in assemblyPaths)
                m_assemblyResolver.AddSearchDirectory(assPath);

            m_assemblyPaths = assemblyPaths;
        }

        public override AssemblyDefinition Resolve(AssemblyNameReference name)
        {
            Console.WriteLine(name.ToString());
            AssemblyDefinition assembly = null;
            try { assembly = m_assemblyResolver.Resolve(name); }
            catch (AssemblyResolutionException)
            {
                foreach (string assPath in m_assemblyPaths)
                {
                    string[] libraries = Directory.GetFiles(assPath, "*.dll", SearchOption.AllDirectories);
                    string missingLib = libraries.Where(lib => lib.Contains(name.Name)).SingleOrDefault();
                    Console.WriteLine(missingLib != null ? "found" : "not found");
                    if (missingLib != null)
                        return AssemblyDefinition.ReadAssembly(missingLib);
                }
            }

            return assembly;
        }
    }

    internal class Injector
    {
        private static Enums.EInjectorState m_injectorState = Enums.EInjectorState.NONE;
        internal Enums.EInjectorState InjectorState { get { return m_injectorState; } }

        // The destination for VIGO's assets.
        private static string m_bundledAssetsDest;

        // Dictates whether the VIGO patching method will get injected or not.
        private readonly bool m_injectGUI;

        // The location of the game extension.
        private readonly string m_extensionPath;

        // Pertains to the location of all required game assemblies
        private readonly string m_dataPath;

        // Generally the mod loader is located alongside all other game assemblies.
        //  BUT - in case it isn't (__merged), this field will point towards the
        //  directory containing VIGO and the mod loader.
        private readonly string m_modLoaderDirPath;

        // The entry point where we want to inject the patcher functions
        private string m_entryPoint;

        // Pertains to the location of the game assembly. When a game
        //  extension uses the merging functionality this location may differ
        //  from where the game stores the rest of its assemblies (e.g. __merged folder)
        private string m_gameAssemblyPath;

        // Will attempt to resolve missing assemblies, this needs
        //  to point to the game directory containing all game assemblies -
        //  most importantly mscorlib.dll
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
            "Microsoft.Practices.ServiceLocation.dll",
            "Microsoft.Practices.Unity.Configuration.dll",
            "Microsoft.Practices.Unity.dll",
            "Microsoft.Practices.Unity.Interception.Configuration.dll",
            "Microsoft.Practices.Unity.Interception.dll",
            "Mono.Cecil.dll",
            "Mono.Cecil.Mdb.dll",
            "Mono.Cecil.Pdb.dll",
            "Mono.Cecil.Rocks.dll",
            "Newtonsoft.Json.dll",
            //"System.Data.dll",
            "System.Runtime.Serialization.dll",
            "ObjectDumper.dll",
            "VortexHarmonyInstaller.dll",
        };

        public Injector(string dataPath, string entryPoint)
        {
            try
            {
                // Check if the dataPath we received contains two paths, this will happen if/when
                //  the patcher has been called to inject the patches into an assembly that is located
                //  away from the game's assemblies (e.g. __merged)
                string[] dataPaths = dataPath.Split(new string[] { "::" }, StringSplitOptions.None);
                m_modLoaderDirPath = (dataPaths[0].EndsWith(".dll"))
                    ? Path.GetDirectoryName(dataPaths[0])
                    : dataPaths[0];

                // Datapath MUST point towards the directory containing all the assemblies used by
                //  the game assembly we're trying to patch, most importantly - mscorlib (if applicable).
                m_dataPath = (dataPaths.Length == 2) ? dataPaths[1] : m_modLoaderDirPath;

                // .NET 3.5 System.IO.Path.Combine doesn't support more than two arguments....
                string uiBundlePath = Path.Combine("VortexBundles", "UI");
                m_bundledAssetsDest = Path.Combine(m_modLoaderDirPath, uiBundlePath);
                m_extensionPath = VortexHarmonyManager.ExtensionPath;
                m_entryPoint = entryPoint;

                // Absolute path to the game assembly itself.
                m_gameAssemblyPath = (m_dataPath.EndsWith(".dll"))
                    ? Path.Combine(m_modLoaderDirPath, Path.GetFileName(m_dataPath))
                    : Path.Combine(m_modLoaderDirPath, Constants.UNITY_ASSEMBLY_LIB);

                if (!File.Exists(m_gameAssemblyPath))
                    throw new FileNotFoundException($"{m_gameAssemblyPath} does not exist");

                m_injectGUI = VortexHarmonyManager.InjectVIGO;
                if (m_injectGUI)
                {
                    Array.Resize(ref _LIB_FILES, _LIB_FILES.Length + 1);
                    _LIB_FILES[_LIB_FILES.Length - 1] = "VortexUnity.dll";
                }

                string[] assemblyResolverPaths = (m_dataPath != m_modLoaderDirPath)
                    ? new string[] { m_dataPath, m_modLoaderDirPath }
                    : new string[] { m_dataPath };

                m_resolver = new MissingAssemblyResolver(assemblyResolverPaths);
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
            m_injectorState = Enums.EInjectorState.RUNNING;
            string strTempFile = null;
            AssemblyDefinition unityAssembly = null;
            try
            {
                // Ensure we have reflection enabled - there's no point
                //  in continuing if reflection is disabled.
                if (!Util.IsReflectionEnabled(m_dataPath))
                {
                    EnableReflection(m_dataPath, m_modLoaderDirPath);
                }
                else
                    m_injectorState = Enums.EInjectorState.FINISHED;

                // Deploy patcher related files.
                DeployFiles();

                // Start the patching process.
                string[] unityPatcher = Constants.VORTEX_UNITY_GUI_PATCH.Split(new string[] { "::" }, StringSplitOptions.None);
                string[] patcherPoints = Constants.VORTEX_PATCH_METHOD.Split(new string[] { "::" }, StringSplitOptions.None);
                string[] entryPoint = m_entryPoint.Split(new string[] { "::" }, StringSplitOptions.None);

                strTempFile = Util.GetTempFile(m_gameAssemblyPath);
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
                    if (m_modLoaderDirPath == m_dataPath)
                        Util.BackupFile(m_gameAssemblyPath, true);

                    AssemblyDefinition vrtxPatcher = AssemblyDefinition.ReadAssembly(Path.Combine(m_modLoaderDirPath, Constants.VORTEX_LIB));
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

                    ILProcessor ilProcessor = methodDefinition.Body.GetILProcessor();
                    ilProcessor.InsertBefore(methodDefinition.Body.Instructions[0], Instruction.Create(OpCodes.Ldstr, VortexHarmonyManager.ModsFolder));
                    ilProcessor.InsertBefore(methodDefinition.Body.Instructions[1], Instruction.Create(OpCodes.Call, methodDefinition.Module.ImportReference(patcherMethod)));
                    if (m_injectGUI)
                    {
                        try
                        {
                            AssemblyDefinition guiPatcher = AssemblyDefinition.ReadAssembly(Path.Combine(m_modLoaderDirPath, Constants.VORTEX_GUI_LIB));
                            MethodDefinition guiMethod = guiPatcher.MainModule.GetType(unityPatcher[0]).Methods.First(x => x.Name == unityPatcher[1]);
                            ilProcessor.InsertBefore(methodDefinition.Body.Instructions[0], Instruction.Create(OpCodes.Call, methodDefinition.Module.ImportReference(guiMethod)));
                        }
                        catch (Exception exc)
                        {
                            throw new EntryPointNotFoundException("Unable to find/insert GUI patcher method definition", exc);
                        }
                    }

                    unityAssembly.Write(m_gameAssemblyPath);
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

                if (m_modLoaderDirPath == m_dataPath)
                    Util.RestoreBackup(m_gameAssemblyPath);

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
        private void EnableReflection(string dataPath, string modLoaderPath)
        {
            string mscorlib = Path.Combine(dataPath, Constants.MSCORLIB);
            try
            {
                mscorlib = (Util.IsSymlink(mscorlib))
                    ? Path.Combine(dataPath, Constants.MSCORLIB + Constants.VORTEX_BACKUP_TAG)
                    : Path.Combine(dataPath, Constants.MSCORLIB);
            }
            catch (Exception exc)
            {
                string message = JSONResponse.CreateSerializedResponse($"{mscorlib} appears to be missing", Enums.EErrorCode.MISSING_FILE, exc);
                Console.WriteLine(message);
                mscorlib = Path.Combine(dataPath, Constants.MSCORLIB + Constants.VORTEX_BACKUP_TAG);
            }

            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(mscorlib);
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
                    string downloadDestination = (dataPath == modLoaderPath)
                        ? Path.Combine(dataPath, strLib)
                        : Path.Combine(modLoaderPath, Constants.MSCORLIB);
                    wc.DownloadDataAsync(uri, downloadDestination);
                    wc.DownloadDataCompleted += (sender, e) =>
                    {
                        string strFileName = e.UserState.ToString();
                        File.WriteAllBytes(strFileName, e.Result);
                        if (dataPath == modLoaderPath)
                        {
                            Util.ReplaceFile(mscorlib, strFileName);
                        }

                        m_injectorState = Enums.EInjectorState.FINISHED;
                    };
                }
                catch (Exception exc)
                {
                    m_injectorState = Enums.EInjectorState.FINISHED;
                    string strMessage = "Unhandled mscorlib version.";
                    Enums.EErrorCode err = Enums.EErrorCode.MISSING_FILE;
                    string strResponse = JSONResponse.CreateSerializedResponse(strMessage, err, exc);
                    Console.Error.WriteLine(strResponse);
                    Environment.Exit((int)(err));
                }
            }
            else
            {
                m_injectorState = Enums.EInjectorState.FINISHED;
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
                string[] entryPoint = m_entryPoint.Split(new string[] { "::" }, StringSplitOptions.None);

                string strTempFile = m_gameAssemblyPath + Constants.VORTEX_BACKUP_TAG;
                File.Copy(m_gameAssemblyPath, strTempFile, true);

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

                    if (m_injectGUI)
                    {
                        Instruction uiPatchInstr = instructions.FirstOrDefault(instr =>
                            instr.Operand.ToString().Contains(Constants.VORTEX_UNITY_GUI_PATCH));

                        if (uiPatchInstr != null)
                            methodDefinition.Body.Instructions.Remove(uiPatchInstr);
                    }

                    unityAssembly.Write(m_gameAssemblyPath);
                }
            }
            catch (Exception exc)
            {
                Enums.EErrorCode errorCode = Enums.EErrorCode.UNKNOWN;
                Util.RestoreBackup(m_gameAssemblyPath);
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
            if (!Directory.Exists(m_dataPath))
                throw new DirectoryNotFoundException(string.Format("Datapath {0} does not exist", m_dataPath));

            string libPath = VortexHarmonyManager.InstallPath;
            Directory.CreateDirectory(VortexHarmonyManager.ModsFolder);
            string[] files = Directory.GetFiles(libPath, "*", SearchOption.TopDirectoryOnly)
                .Where(file => _LIB_FILES.Contains(Path.GetFileName(file)))
                .ToArray();

            foreach (string file in files)
            {
                string dataPathDest = Path.Combine(m_dataPath, Path.GetFileName(file));
                string modLoaderPathDest = Path.Combine(m_modLoaderDirPath, Path.GetFileName(file));

                if (File.Exists(dataPathDest) || File.Exists(modLoaderPathDest))
                {
                    //FileVersionInfo ourFile = FileVersionInfo.GetVersionInfo(file);
                    //FileVersionInfo theirFile = FileVersionInfo.GetVersionInfo(dataPathDest);

                    //if (ourFile.FileMajorPart == theirFile.FileMajorPart)
                    //    File.Copy(file, modLoaderPathDest, true);
                    //else
                    //{
                        string strResponse = JSONResponse.CreateSerializedResponse($"{Path.GetFileName(dataPathDest)} exists and will not be replaced", Enums.EErrorCode.SAFE_TO_IGNORE);
                        Console.Error.WriteLine(strResponse);
                    //}
                }
                else
                {
                    File.Copy(file, modLoaderPathDest);
                }
            }

            if (m_injectGUI && (m_extensionPath != null))
            {
                string[] uiFiles = new string[] {
                    Path.Combine(m_extensionPath, Constants.UI_BUNDLE_FILENAME),
                    Path.Combine(m_extensionPath, Constants.UI_BUNDLE_FILENAME + ".manifest"),
                };

                try
                {
                    Directory.CreateDirectory(m_bundledAssetsDest);
                    foreach (string file in uiFiles)
                    {
                        string strDest = Path.Combine(m_bundledAssetsDest, Path.GetFileName(file));
                        File.Copy(file, strDest, true);
                    }
                }
                catch (Exception e)
                {
                    // This is fine, some extensions might not provide bundled UI assets.
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
            string[] files = Directory.GetFiles(m_dataPath, "*", SearchOption.TopDirectoryOnly)
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

            if (m_injectGUI && Directory.Exists(m_bundledAssetsDest))
                Directory.Delete(m_bundledAssetsDest, true);
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
