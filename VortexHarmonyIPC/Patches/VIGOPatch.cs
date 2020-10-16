using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using VortexInjectorIPC.Types;

namespace VortexInjectorIPC.Patches {
    internal partial class Constants {
        internal const string VIGO_ASSEMBLY = "VortexUnity.dll";

        // The name of the bundled asset file.
        internal const string UI_BUNDLE_FILENAME = "vortexui";
    }
    class VIGOPatch: IPatch {
        private static readonly Lazy<VIGOPatch> sPatch =
            new Lazy<VIGOPatch> (() => new VIGOPatch ());
        public static VIGOPatch Instance { get { return sPatch.Value; } }
        private VIGOPatch () { }

        public async Task<Dictionary<string, object>> IsPatchApplicable (JObject data,
                                                                   CoreDelegates core)
        {
            PatchConfig config = new PatchConfig ((JObject)data ["patchConfig"]);
            Dictionary<string, object> res = await ReflectionPatch.Instance.IsPatchApplicable (data, core);
            if (res ["Result"].ToString () == "false")
                return res;

            return await Injector.Instance.IsPatchApplicable (data, (ProgressDelegate)((int progress) => { }), core);
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
                res = await DeployVIGO (core);
                if (res ["Result"].ToString () == "False")
                    return res;
            }

            return await Injector.Instance.ApplyPatch (data, progress, core);
        }

        public async Task<Dictionary<string, object>> RemovePatch (JObject data,
                                                                   ProgressDelegate progress,
                                                                   CoreDelegates coreDelegates)
        {
            try {
                string modLoaderPath = await coreDelegates.context.GetModLoaderPath ();
                string bundledAssetsDest = Path.Combine (modLoaderPath, "VortexBundles");
                PurgeVIGO (bundledAssetsDest);
            } catch (Exception) {
                // Failed to purge VIGO, not a big deal
            }

            return await Injector.Instance.RemovePatch (data, progress, coreDelegates);
        }

        public Task<bool> IsApplied (JObject data)
        {
            throw new NotImplementedException ();
        }

        private void PurgeVIGO(string vigoAssetsPath)
        {
            if (vigoAssetsPath != null && Directory.Exists (vigoAssetsPath))
                Directory.Delete (vigoAssetsPath, true);
        }

        private async Task<Dictionary<string, object>> DeployVIGO (CoreDelegates core)
        {
            bool deployedBundle = false;
            string message = "Failed to deploy VIGO";
            try {
                string extensionPath = await core.context.GetExtensionPath ();
                string modLoaderPath = await core.context.GetModLoaderPath ();
                string [] uiFiles = new string [] {
                Path.Combine(extensionPath, Constants.UI_BUNDLE_FILENAME),
                Path.Combine(extensionPath, Constants.UI_BUNDLE_FILENAME + ".manifest"),
            };
                try {
                    string bundledAssetsDest = Path.Combine (modLoaderPath, "VortexBundles", "UI");
                    Directory.CreateDirectory (bundledAssetsDest);
                    foreach (string file in uiFiles) {
                        string strDest = Path.Combine (bundledAssetsDest, Path.GetFileName (file));
                        File.Copy (file, strDest, true);
                        deployedBundle = true;
                        message = "VIGO deployed successfully";
                    }
                } catch (Exception e) {
                    // This is fine, some extensions might not provide bundled UI assets.
                    //  all this means is that the in-game UI will not look that great.
                    message = e.Message;
                }
            } catch (Exception e) {
                message = e.Message;
            }
            
            return PatchHelper.CreatePatchResult (deployedBundle, message);
        }
    }
}
