﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using VortexHarmonyInstaller.Delegates;

namespace VortexHarmonyInstaller.ModTypes
{
    // UMM passes a ModEntry object to its mods;
    //  we're going to hijack the object, keeping only
    //  properties and functionality that are relevant to us.
    public class ModEntry: IExposedMod
    {
        // Reference to the mod's data
        private UMMData m_ModData = null;
        public UMMData Info {
            get { return m_ModData; }
            private set { m_ModData = value; }
        }

        // Full path to the mod's folder.
        private string m_strModPath;
        public string Path {
            get { return m_strModPath; }
            private set { m_strModPath = value; }
        }

        public ModEntry(UMMData data, string strModPath)
        {
            m_ModData = data;
            m_strModPath = strModPath;
        }

        public static ModEntry GetModEntry(UMMData data, string strModPath)
        {
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

        public static class Logger
        {
            public static void NativeLog(string str)
            {
                LoggerDelegates.LogInfo(str);
            }

            public static void NativeLog(string str, string prefix)
            {
                LoggerDelegates.LogInfo(prefix + str);
            }

            public static void Log(string str)
            {
                LoggerDelegates.LogInfo(str);
            }

            public static void Log(string str, string prefix)
            {
                LoggerDelegates.LogInfo(prefix + str);
            }

            public static void Error(string str)
            {
                LoggerDelegates.LogError(str);
            }

            public static void Error(string str, string prefix)
            {
                LoggerDelegates.LogError(prefix + str);
            }

            public static void LogException(Exception e)
            {
                LoggerDelegates.LogError(e);
            }

            public static void LogException(string key, Exception e)
            {
                LoggerDelegates.LogError(e);
            }

            public static void LogException(string key, Exception e, string prefix)
            {
                LoggerDelegates.LogError(e);
            }
        }
    }

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
            try
            {
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
            catch (Exception exc) { return false; }
        }

        public bool ParseSettings(string strSettingsPath)
        {
            throw new NotImplementedException();
        }
    }
}