using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using VortexInjectorIPC.Types;

namespace VortexInjectorIPC.Patches {
    class VMLPatch: IPatch {
        private static readonly Lazy<VMLPatch> sPatch =
            new Lazy<VMLPatch> (() => new VMLPatch ());
        public static VMLPatch Instance { get { return sPatch.Value; } }

        // Array of files we need to deploy/remove to/from the game's datapath.
        private static string [] _LIB_FILES = new string [] {
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

        private VMLPatch () { }

        public async Task<Dictionary<string, object>> IsPatchApplicable (JObject data,
                                                                         CoreDelegates core)
        {
            PatchConfig config = new PatchConfig ((JObject)data ["patchConfig"]);
            Dictionary<string, object> res = await ReflectionPatch.Instance.IsPatchApplicable (data, core);
            if (res ["Result"].ToString () == "False" && res["Message"].ToString() != "Reflection is enabled")
                return res;

            return await Injector.Instance.IsPatchApplicable(data, (ProgressDelegate)((int progress) => { }), core);
        }

        public async Task<Dictionary<string, object>> ApplyPatch (JObject data,
                                                                  ProgressDelegate progress,
                                                                  CoreDelegates core)
        {
            Dictionary<string, object> res = await IsPatchApplicable (data, core);
            if (res ["Result"].ToString () == "False")
                return res;

            res = await ReflectionPatch.Instance.ApplyPatch (data, progress, core);
            if (res ["Result"].ToString () == "False")
                return res;

            if (await core.context.IsDeploymentRequired()) {
                res = await DeployFiles (core);
                if (res ["Result"].ToString () == "False")
                    return res;
            }

            return await Injector.Instance.ApplyPatch(data, progress, core);
        }

        public async Task<Dictionary<string, object>> RemovePatch (JObject data,
                                                                   ProgressDelegate progress,
                                                                   CoreDelegates coreDelegates)
        {
            // We're not going to remove the reflection patch. It might still be needed.
            try {
                string dataPath = await coreDelegates.context.GetDataPath ();
                PurgeFiles (dataPath);
            } catch (Exception) {
                // We failed to delete some of the dependency assemblies...
                //  that's arguably fine.
            }
            
            return await Injector.Instance.RemovePatch (data, progress, coreDelegates);
        }

        public async Task<bool> IsApplied (JObject data)
        {
            // Not needed in this case - the injector can remove this patch just fine.
            throw new NotImplementedException ();
        }

        /// <summary>
        /// Removes Vortex libraries from the Vortex folder.
        /// </summary>
        private void PurgeFiles (string dataPath)
        {
            string [] files = Directory.GetFiles (dataPath, "*", SearchOption.TopDirectoryOnly)
                .Where (file => _LIB_FILES.Contains (Path.GetFileName (file)))
                .ToArray ();

            foreach (string strFile in files) {
                try {
                    // Try to re-instate backups if they exist.
                    Util.RestoreBackup (strFile);
                } catch (Exception exc) {
                    if (exc is FileNotFoundException)
                        File.Delete (strFile);
                }
            }
        }

        /// <summary>
        /// Creates the folder structure inside the game's
        ///  folder.
        /// </summary>
        private async Task<Dictionary<string, object>> DeployFiles (CoreDelegates core)
        {
            bool result = true;
            string message = "VML dependencies deployed";
            try {
                string dataPath = await core.context.GetDataPath ();
                if (!Directory.Exists (dataPath))
                    throw new DirectoryNotFoundException (string.Format ("Datapath {0} does not exist", dataPath));

                string libPath = await core.context.GetVMLDepsPath ();
                string modsPath = await core.context.GetModsPath ();
                string modLoaderPath = await core.context.GetModLoaderPath ();

                Directory.CreateDirectory (modsPath);
                string [] files = Directory.GetFiles (libPath, "*", SearchOption.TopDirectoryOnly)
                    .Where (file => _LIB_FILES.Contains (Path.GetFileName (file)))
                    .ToArray ();

                foreach (string file in files) {
                    string dataPathDest = Path.Combine (dataPath, Path.GetFileName (file));
                    string modLoaderPathDest = Path.Combine (modLoaderPath, Path.GetFileName (file));

                    if (!File.Exists (dataPathDest) || !File.Exists (modLoaderPathDest)) {
                        File.Copy (file, modLoaderPathDest);
                    }
                }
            } catch (Exception exc) {
                result = false;
                message = exc.Message;
            }

            return PatchHelper.CreatePatchResult (result, message);
        }
    }
}
