using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace VortexHarmonyInstaller.ModTypes
{
    // UMM passes a ModEntry object to its mods;
    //  we're going to hijack the object, keeping only
    //  properties and functionality that are relevant to us.
    public class ModEntry
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
