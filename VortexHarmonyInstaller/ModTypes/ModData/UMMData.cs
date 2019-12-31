using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Newtonsoft.Json;

using VortexHarmonyInstaller;
using VortexHarmonyInstaller.Delegates;
using VortexHarmonyInstaller.ModTypes;

namespace UnityModManagerNet
{
    public partial class UnityModManager
    {
        public class ModInfo : IEquatable<ModInfo>
        {
            public string Id;
            public string DisplayName;
            public string Author;
            public string Version;
            public string ManagerVersion;
            public string GameVersion;
            public string[] Requirements;
            public string AssemblyName;
            public string EntryMethod;
            public string HomePage;
            public string Repository;

            [NonSerialized]
            public bool IsCheat = true;

            public static implicit operator bool(ModInfo exists)
            {
                return exists != null;
            }

            public bool Equals(ModInfo other)
            {
                return Id.Equals(other.Id);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj))
                {
                    return false;
                }
                return obj is ModInfo modInfo && Equals(modInfo);
            }

            public override int GetHashCode()
            {
                return Id.GetHashCode();
            }

            public ModInfo(UMMData data)
            {
                Id = data.Id;
                DisplayName = data.DisplayName;
                Author = data.Author;
                Version = data.Version;
                ManagerVersion = "1.0.0";
                GameVersion = "1.0.0";
                Requirements = data.Requirements;
                AssemblyName = data.AssemblyName;
                EntryMethod = data.EntryMethod;
                HomePage = "www.nexusmods.com";
                Repository = "www.nexusmods.com";
            }
        }

        // UMM passes a ModEntry object to its mods;
        //  we're going to hijack the object, keeping only
        //  properties and functionality that are relevant to us.
        public class ModEntry : IExposedMod
        {
            public class ModLogger
            {
                private string m_modId;
                public ModLogger(string id)
                {
                    m_modId = $"[{id}] ";
                }

                public void Log(string str)
                {
                    LoggerDelegates.LogInfo(m_modId + str);
                }

                public void Error(string str)
                {
                    LoggerDelegates.LogError(m_modId + str);
                }

                public void Critical(string str)
                {
                    LoggerDelegates.LogError(m_modId + str);
                }

                public void Warning(string str)
                {
                    LoggerDelegates.LogInfo(m_modId + str);
                }

                public void NativeLog(string str)
                {
                    LoggerDelegates.LogInfo(m_modId + str);
                }
                
                public void LogException(string key, Exception e)
                {
                    LoggerDelegates.LogError(m_modId + key, e);
                }

                public void LogException(Exception e)
                {
                    LoggerDelegates.LogError(m_modId, e);
                }
            }

            // Full path to the mod's folder.
            public readonly string Path;
            public readonly ModLogger Logger;
            public readonly ModInfo Info;

            // Reference to the mod's data
            private UMMData m_ModData = null;
            public UMMData ModData
            {
                get { return m_ModData; }
                private set { m_ModData = value; }
            }

            public ModEntry(UMMData data, string strModPath)
            {
                m_ModData = data;
                Path = strModPath;
                Info = new ModInfo(data);
                Logger = new ModLogger(Info.Id);
            }

            public static ModEntry GetModEntry(UMMData data, string strModPath)
            {
                if (data == null)
                    throw new NullReferenceException("Must provide valid data");

                return new ModEntry(data, strModPath);
            }

            public void InvokeOnGUI()
            {
                OnGUI?.Invoke(this);
                OnFixedGUI?.Invoke(this);
            }

            public void InvokeToggleGUI(bool bToggled)
            {
                if ((bool)(OnToggle?.Invoke(this, bToggled)))
                    OnShowGUI?.Invoke(this);
                else
                    OnHideGUI?.Invoke(this);
            }

            public void InvokeOnStart()
            {
                m_ModData.Hooks.Start?.Invoke();
            }

            public void InvokeOnUpdate(float fDelta)
            {
                OnUpdate?.Invoke(this, fDelta);
            }

            public void InvokeOnLateUpdate(float fDelta)
            {
                OnLateUpdate?.Invoke(this, fDelta);
            }

            public void InvokeOnFixedUpdate(float fDelta)
            {
                OnFixedUpdate?.Invoke(this, fDelta);
            }

            public void InvokeCustom(string strName)
            {
                throw new NotImplementedException();
            }

            public string GetModName()
            {
                return Info.Id;
            }

            public Func<ModEntry, bool> OnUnload = null;
            public Func<ModEntry, bool, bool> OnToggle = null;
            public Action<ModEntry> OnGUI = null;
            public Action<ModEntry> OnFixedGUI = null;
            public Action<ModEntry> OnShowGUI = null;
            public Action<ModEntry> OnHideGUI = null;
            public Action<ModEntry> OnSaveGUI = null;
            public Action<ModEntry, float> OnUpdate = null;
            public Action<ModEntry, float> OnLateUpdate = null;
            public Action<ModEntry, float> OnFixedUpdate = null;
        }

        public static ModEntry FindMod(string id)
        {
            IExposedMod mod = BaseModType.ExposedMods
                .Where(entry => ((entry as ModEntry) != null) && (entry.GetModName() == id))
                .SingleOrDefault();

            if (mod == null)
                LoggerDelegates.LogError($"Unable to find mod: {id}");

            return (mod as ModEntry);
        }
    }
}

namespace VortexHarmonyInstaller.ModTypes
{
    public class UMMData : BaseParsedModData, IParsedModData
    {
        [JsonRequired]
        public string Id
        {
            get { return m_strId; }
            set { m_strId = value; }
        }

        [JsonRequired]
        public string EntryMethod
        {
            get { return m_strEntryPoint; }
            set { m_strEntryPoint = value; }
        }

        public string Version
        {
            get { return m_strModVersion; }
            set { m_strModVersion = value; }
        }

        public string[] Requirements
        {
            get { return m_rgDependencies; }
            set { m_rgDependencies = value; }
        }

        public string AssemblyName
        {
            get { return m_strAssemblyName; }
            set { m_strAssemblyName = value; }
        }

        public string Author
        {
            get { return m_strAuthor; }
            set { m_strAuthor = value; }
        }

        public string DisplayName
        {
            get { return m_strName; }
            set { m_strName = value; }
        }

        public bool ParseManifest(string strManifestPath)
        {
            string dirPath = Path.GetDirectoryName(strManifestPath);
            string fileName = Path.GetFileName(strManifestPath);
            try
            {
                if (!File.Exists(strManifestPath))
                {
                    fileName = fileName.ToLower();
                    strManifestPath = Path.Combine(dirPath, fileName);
                }
                string json = File.ReadAllText(strManifestPath);
                UMMData modData = JsonConvert.DeserializeObject<UMMData>(json);
                if (modData.Base_Id != null)
                {
                    AssignBaseData(modData);
                    m_strAssemblyName = modData.AssemblyName;
                    return true;
                }
                else
                    return false;
            }
            catch (Exception exc) {
                bool isParserError = (exc is JsonException) ? true : false;
                if (!isParserError)
                    LoggerDelegates.LogError("Failed to parse mod data", exc);

                return false;
            }
        }

        public bool ParseSettings(string strSettingsPath)
        {
            throw new NotImplementedException();
        }
    }
}
