using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VortexHarmonyInstaller
{
    public interface IModType
    {
        bool ParseModData(string strManifestPath);

        bool ConvertAssemblyReferences(string strDllPath);

        string GetModName();

        void InjectPatches();
    }

    public interface IParsedModData
    {
        /// <summary>
        /// Attempts to parse mod information from the mod manifest.
        ///  Different modTypes have different format.
        /// </summary>
        /// <param name="strManifestPath">Path to the mod's manifest</param>
        /// <returns>true if parsed successfully, false otherwise</returns>
        bool ParseManifest(string strManifestPath);

        /// <summary>
        /// Attempts to parse a mod's settings file (if viable)
        /// </summary>
        /// <param name="strSettingsPath">Path to the mod's settings file</param>
        /// <returns>true if parsed and stored successfully</returns>
        bool ParseSettings(string strSettingsPath);
    }

    public interface IExposedMod
    {
        void InvokeOnGUI();
        void InvokeToggleGUI(bool bToggled);
        void InvokeOnStart();
        void InvokeOnUpdate(float fDelta = 0);
        void InvokeOnLateUpdate(float fDelta = 0);
        void InvokeOnFixedUpdate(float fDelta = 0);
        void InvokeCustom(string strName);
        string GetModName();
    }
}
