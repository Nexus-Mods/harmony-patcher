using Mono.Cecil;
using Mono.Cecil.Cil;

using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using VortexInjectorIPC.Types;

namespace VortexInjectorIPC.Patches {
    internal partial class Constants {
        // Multilanguage Standard Common Object Runtime Library
        internal const string MSCORLIB = "mscorlib.dll";

        // Github location containing mscorlib replacements.
        internal const string GITHUB_LINK = "https://raw.githubusercontent.com/IDCs/mscorlib-replacements/master/";
    }

    internal partial class PatchHelper {
        /// <summary>
        /// Creates a patch result dictionary in the expected format. This
        ///  is just to avoid typos and stuff like that.
        /// </summary>
        /// <param name="result"></param>
        /// <param name="message"></param>
        /// <returns>Default patch result dictionary</returns>
        internal static Dictionary<string, object> CreatePatchResult(bool result, string message,
            Dictionary<string, object> addendum = null)
        {
            var res = new Dictionary<string, object> () {
                { "Result", result.ToString() },
                { "Message", message }
            };
            return (addendum != null)
                ? res.Concat (addendum.Where(kvp => !res.ContainsKey(kvp.Key)))
                     .ToDictionary (x => x.Key, x => x.Value)
                : res;
        }
    }

    internal class ReflectionPatch: IPatch {
        // Array of mono mscrolib replacements which will re-enable reflection
        //  for games that have them disabled.
        private readonly string [] LIB_REPLACEMENTS = new string []
        {
            "mscorlib.dll.2.0.50727.1433",
            "mscorlib.dll.3.0.40818.0",
            "mscorlib.dll.4.6.57.0",
        };

        private static readonly Lazy<ReflectionPatch> sPatch =
            new Lazy<ReflectionPatch> (() => new ReflectionPatch ());
        public static ReflectionPatch Instance { get { return sPatch.Value; } }
        private ReflectionPatch () { }

        async public Task<Dictionary<string, object>> ApplyPatch (JObject data,
                                                                  ProgressDelegate progress,
                                                                  CoreDelegates coreDelegates)
        {
            PatchConfig config = new PatchConfig ((JObject)data ["patchConfig"]);
            string dataPath = await coreDelegates.context.GetDataPath ();
            string modLoaderPath = await coreDelegates.context.GetModLoaderPath ();

            // We know this is a dictionary, but I'm too lazy to type.
            var result = await EnableReflection (dataPath, modLoaderPath);
            result.Add ("Source", config.SourceEntryPoint.ToString ());
            result.Add ("Targets", config.TargetEntryPoints.ToString ());
            return result;
        }

        public Task<bool> IsApplied (JObject data)
        {
            throw new NotImplementedException ();
        }

        async public Task<Dictionary<string, object>> IsPatchApplicable (JObject data,
                                                                         CoreDelegates coreDelegates)
        {
            string message;
            bool result = false;
            PatchConfig config = new PatchConfig ((JObject)data ["patchConfig"]);
            string dataPath = await coreDelegates.context.GetDataPath ();
            try {
                if (IsReflectionEnabled (dataPath)) {
                    // Reflection already enabled - no need to do anything.
                    message = "Reflection is enabled";
                    result = false;
                } else {
                    message = "Can be applied";
                    result = true;
                }
            } catch (Exception exc) {
                result = false;
                message = exc.Message;
            }
            var addendum = new Dictionary<string, object> () {
                { "Source", config.SourceEntryPoint.ToString() },
                { "Targets", config.TargetEntryPoints.ToString() }
            };
            return PatchHelper.CreatePatchResult (result, message, addendum);
        }

        public Task<Dictionary<string, object>> RemovePatch (JObject data,
                                                             ProgressDelegate progress,
                                                             CoreDelegates coreDelegates)
        {
            throw new NotImplementedException ();
        }

        /// <summary>
        /// Certain games distribute modified assemblies which disable
        ///  reflection and impede modding.
        /// </summary>
        private async Task<Dictionary<string, object>> EnableReflection (string dataPath, string modLoaderPath)
        {
            bool result = false;
            string message = "Unhandled mscorlib version";
            string mscorlib = Path.Combine (dataPath, Constants.MSCORLIB);
            try {
                mscorlib = (Util.IsSymlink (mscorlib))
                    ? Path.Combine (dataPath, Constants.MSCORLIB + VortexInjectorIPC.Constants.VORTEX_BACKUP_TAG)
                    : Path.Combine (dataPath, Constants.MSCORLIB);
            } catch (Exception) {
                mscorlib = Path.Combine (dataPath, Constants.MSCORLIB + VortexInjectorIPC.Constants.VORTEX_BACKUP_TAG);
            }

            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo (mscorlib);
            string version = fvi.FileVersion;
            string strLib = LIB_REPLACEMENTS
                .Where (replacement => replacement.Substring (Constants.MSCORLIB.Length + 1, 1) == version.Substring (0, 1))
                .SingleOrDefault ();

            if (null != strLib) {
                try {
                    WebClient wc = new WebClient ();
                    Uri uri = new Uri (Constants.GITHUB_LINK + strLib);
                    string downloadDestination = (dataPath == modLoaderPath)
                        ? Path.Combine (dataPath, strLib)
                        : Path.Combine (modLoaderPath, Constants.MSCORLIB);
                    byte[] downloaded = await wc.DownloadDataTaskAsync (uri);
                    File.WriteAllBytes (downloadDestination, downloaded);
                    if (dataPath == modLoaderPath) {
                        Util.ReplaceFile (mscorlib, downloadDestination);
                    }
                    result = true;
                    message = "Reflection enabled";
                } catch (Exception exc) {
                    result = false;
                    message = exc.Message;
                }
            }

            return PatchHelper.CreatePatchResult (result, message);
        }

        /// <summary>
        /// Function will test if the game we're trying to inject to
        ///  is distributing a mscorlib.dll file and if so, we're going
        ///  to test whether reflection is enabled.
        /// </summary>
        /// <param name="dataPath"></param>
        private bool IsReflectionEnabled (string dataPath)
        {
            // This method will most definitely have to be enhanced as we encounter new
            //  situations where a game's mscorlib reflection functionality may have been disabled.
            const string ENTRY = "System.Reflection.Emit.AssemblyBuilder::DefineDynamicAssembly";
            string corLib = Path.Combine (dataPath, Constants.MSCORLIB);
            bool reflectionEnabled = true;
            if (!File.Exists (corLib)) {
                // No custom corlib, safe to assume that reflection is enabled.
                return reflectionEnabled;
            }

            string tempFile = string.Empty;
            try {
                tempFile = Util.GetTempFile (Path.Combine (dataPath, Constants.MSCORLIB));
            } catch (Exception) {
                tempFile = Util.GetTempFile (Path.Combine (dataPath, Constants.MSCORLIB + VortexInjectorIPC.Constants.VORTEX_BACKUP_TAG));
            }

            string [] entryPoint = ENTRY.Split (new string [] { "::" }, StringSplitOptions.None);
            try {
                AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly (tempFile);
                if (assembly.Name.Version.Major <= 3) {
                    // There doesn't seem to be a reason to replace the corlib
                    //  for older .NET versions.
                    assembly.Dispose ();
                    return true;
                }
                TypeDefinition type = assembly.MainModule.GetType (entryPoint [0]);
                if (null == type)
                    throw new NullReferenceException ("Failed to find entry Type in mod assembly");

                MethodDefinition meth = type.Methods
                    .Where (method => method.Name.Contains (entryPoint [1]) && method.Parameters.Count == 2)
                    .FirstOrDefault ();

                if (null == meth)
                    throw new NullReferenceException ("Failed to find entry Method in mod assembly");

                Instruction instr = meth.Body.Instructions
                    .Where (instruction => instruction.ToString ().Contains (nameof (PlatformNotSupportedException)))
                    .SingleOrDefault ();

                reflectionEnabled = (instr == null);

                assembly.Dispose ();
            } catch (Exception) {
                reflectionEnabled = false;
            }

            Util.DeleteTemp (tempFile);
            return reflectionEnabled;
        }
    }
}
