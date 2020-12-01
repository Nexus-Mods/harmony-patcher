using System.Collections.Generic;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace VortexInjectorIPC.Types {
    public delegate void ProgressDelegate (int percent);
    public interface IPatch {
        /// <summary>
        /// Tests whether this patch can be applied. This is the correct
        ///  location to verify whether dependencies are present and whether
        ///  the patch had already been applied.
        /// </summary>
        /// <param name="config"></param>
        /// <returns>Information whether the patch is applicable</returns>
        Task<Dictionary<string, object>> IsPatchApplicable (JObject data,
                                                            CoreDelegates core);

        /// <summary>
        /// Attempts to apply the patch to the target assembly
        /// </summary>
        /// <param name="config"></param>
        /// <param name="progress"></param>
        /// <param name="coreDelegates"></param>
        /// <returns>Success/Failure information</returns>
        Task<Dictionary<string, object>> ApplyPatch (JObject data,
                                                     ProgressDelegate progress,
                                                     CoreDelegates coreDelegates);

        /// <summary>
        /// Attempts to remove the patch from the target assembly
        /// </summary>
        /// <param name="config"></param>
        /// <param name="progress"></param>
        /// <param name="coreDelegates"></param>
        /// <returns>Success/Failure information</returns>
        Task<Dictionary<string, object>> RemovePatch (JObject data,
                                                      ProgressDelegate progress,
                                                      CoreDelegates coreDelegates);

        /// <summary>
        /// Quick check if the patch has been applied.
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        Task<bool> IsApplied (JObject data);
    }
}
