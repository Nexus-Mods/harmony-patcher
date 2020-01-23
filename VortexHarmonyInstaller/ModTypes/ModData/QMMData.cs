using System;
using System.IO;

using Newtonsoft.Json;

namespace VortexHarmonyInstaller.ModTypes
{
    class QMMData : BaseParsedModData, IParsedModData
    {
        [JsonRequired]
        public string Id {
            get { return m_strId; }
            set { m_strId = value; }
        }

        [JsonRequired]
        public string EntryMethod {
            get { return m_strEntryPoint; }
            set { m_strEntryPoint = value; }
        }

        public string Version {
            get { return m_strModVersion; }
            set { m_strModVersion = value; }
        }

        public string[] Requires {
            get { return m_rgDependencies; }
            set { m_rgDependencies = value; }
        }

        public string AssemblyName {
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

        public string GetTargetAssemblyFileName()
        {
            throw new NotImplementedException();
        }

        public bool ParseManifest(string strManifestPath)
        {
            try
            {
                string json = File.ReadAllText(strManifestPath);
                QMMData modData = JsonConvert.DeserializeObject<QMMData>(json);
                if (modData.Base_Id != null)
                {
                    AssignBaseData(modData);
                    return true;
                }
                else
                    return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool ParseSettings(string strSettingsPath)
        {
            throw new NotImplementedException();
        }
    }
}
