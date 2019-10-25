namespace VortexHarmonyInstaller.ModTypes
{
    interface ISettings
    {
        void Save(IExposedMod mod);

        T Load<T, U>(IExposedMod mod) where T : U, new();

        string GetSettingsPath(IExposedMod mod);
    }
}
