namespace VortexHarmonyInstaller
{
    public interface IModType
    {
        /// <summary>
        /// Function will process and deserialize a mod's manifest
        /// into a data object which can then be used to expose data
        /// to the game itself.
        /// </summary>
        /// <param name="manifestPath"></param>
        /// <returns></returns>
        bool ParseModData(string manifestPath);

        /// <summary>
        /// This function should be primarily used for non-native mods
        /// which have been developed for other mod managers, this is to
        /// bring the mod assembly itself in-line with what Vortex expects.
        /// </summary>
        /// <param name="strDllPath"></param>
        /// <returns></returns>
        bool ConvertAssemblyReferences(string strDllPath);

        /// <summary>
        /// Returns the name/id of this mod entry.
        /// </summary>
        /// <returns></returns>
        string GetModName();

        /// <summary>
        /// Where the magic happens, aka the mod's entry point should be invoked. 
        /// </summary>
        void InjectPatches();

        /// <summary>
        /// Retrieves the mod's data.
        /// </summary>
        /// <returns></returns>
        IParsedModData GetModData();

        /// <summary>
        /// Returns an array of strings containing names/ids of mods that
        /// should be loaded before this mod. These are defined inside the
        /// mod's manifest file
        /// </summary>
        /// <returns></returns>
        string[] GetDependencies();
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

        /// <summary>
        /// This function expects the mod's data to be populated and accessible!
        ///  Will attempt to retrieve the target assembly from the mod manifest;
        ///  this is required when a mod root directory contains more than a single
        ///  assembly file and we need to resolve which one to inject.
        /// </summary>
        /// <returns>
        /// The filename of the mod's target assembly e.g. "CheatingGoose.dll";
        ///  alternatively will return null if the mod author did not provide a
        ///  target assembly for the mod inside the manifest file.
        /// </returns>
        string GetTargetAssemblyFileName();
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
