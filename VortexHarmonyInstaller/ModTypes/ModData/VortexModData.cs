using System;
using System.IO;

using Newtonsoft.Json;
using VortexHarmonyInstaller.Util;

namespace VortexHarmonyInstaller.ModTypes
{
    // Any data we want to expose to the mod should be included in
    //  this class.
    public class VortexMod : ILoggableMod, IExposedMod
    {
        public Action<VortexMod> OnGUI = null;
        public Action<VortexMod, bool> OnGUIToggle = null;

        private VortexModData m_ModData = null;
        public VortexModData VortexData {
            get { return m_ModData; }
            private set { m_ModData = value; }
        }

        private string m_strGameDataPath;
        public string DataPath {
            get { return m_strGameDataPath; }
            private set { m_strGameDataPath = value; }
        }

        public VortexMod(VortexModData data, string strDataPath)
        {
            m_ModData = data;
            m_strGameDataPath = strDataPath;
        }

        public static VortexMod GetModEntry(VortexModData data, string strDataPath)
        {
            return new VortexMod(data, strDataPath);
        }

        public void LogInfo(string strMessage, Exception exc)
        {
            if (exc != null)
                VortexPatcher.Logger.Info(PrependModId(strMessage), exc);
            else
                VortexPatcher.Logger.Info(PrependModId(strMessage));
        }

        public void LogInfo(object obj, string strType)
        {
            VortexPatcher.Logger.Info(obj);
        }

        public void LogDebug(string strMessage, Exception exc)
        {
            if (exc != null)
                VortexPatcher.Logger.Debug(PrependModId(strMessage), exc);
            else
                VortexPatcher.Logger.Debug(PrependModId(strMessage));
        }

        public void LogDebug(object obj, string strType)
        {
            VortexPatcher.Logger.Debug(obj);
        }

        public void LogError(string strMessage, Exception exc)
        {
            if (exc != null)
                VortexPatcher.Logger.Error(PrependModId(strMessage), exc);
            else
                VortexPatcher.Logger.Error(PrependModId(strMessage));
        }

        public void LogError(object obj, string strType)
        {
            VortexPatcher.Logger.Error(obj);
        }

        private string PrependModId(string strMessage)
        {
            return string.Format("[{0}] - {1}", m_ModData.Base_Id, strMessage);
        }

        public void InvokeOnGUI()
        {
            OnGUI?.Invoke(this);
        }

        public void InvokeToggleGUI(bool bToggled)
        {
            OnGUIToggle?.Invoke(this, bToggled);
        }

        public void InvokeOnStart()
        {
            m_ModData.Hooks.Start?.Invoke();
        }

        public void InvokeOnUpdate(float fDelta)
        {
            m_ModData.Hooks.Update?.Invoke();
        }

        public void InvokeOnLateUpdate(float fDelta)
        {
            m_ModData.Hooks.LateUpdate?.Invoke();
        }

        public void InvokeOnFixedUpdate(float fDelta)
        {
            m_ModData.Hooks.FixedUpdate?.Invoke();
        }

        public void InvokeCustom(string strName)
        {
            throw new NotImplementedException();
        }

        public string GetModName()
        {
            return m_ModData.DisplayName;
        }
    }

    public class VortexModData : BaseParsedModData, IParsedModData
    {
        [JsonRequired]
        public bool IsCheat {
            get { return m_bIsCheat; }
            set { m_bIsCheat = value; }
        }

        [JsonRequired]
        public string EntryPoint
        {
            get { return m_strEntryPoint; }
            set { m_strEntryPoint = value; }
        }

        [JsonRequired]
        public string Id
        {
            get { return m_strId; }
            set { m_strId = value; }
        }

        [JsonRequired]
        public string DisplayName {
            get { return m_strName; }
            set { m_strName = value; }
        }

        public string Author {
            get { return m_strAuthor; }
            set { m_strAuthor = value; }
        }

        public string GameId {
            get { return m_strGameId; }
            set { m_strGameId = value; }
        }

        [JsonIgnore]
        public string ModPath {
            get { return Path.GetDirectoryName(m_strManifestPath); }
        }

        public bool ParseManifest(string strManifestPath)
        {
            try
            {
                string json = File.ReadAllText(strManifestPath);
                VortexModData modData = JsonConvert.DeserializeObject<VortexModData>(json, 
                    new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error, });
                if (modData.Base_Id != null)
                {
                    AssignBaseData(modData);
                    AssignAssemblyName(strManifestPath);
                    return true;
                }
                else
                    return false;
            }
            catch (Exception exc)
            {
                VortexPatcher.Logger.Error("Failed to parse mod manifest", exc);
                return false;
            }
        }

        public bool ParseSettings(string strSettingsPath)
        {
            throw new NotImplementedException();
        }
    }
}
