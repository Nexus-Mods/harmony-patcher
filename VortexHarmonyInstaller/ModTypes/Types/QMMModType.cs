using Microsoft.Practices.Unity;

using System;
using System.IO;

namespace VortexHarmonyInstaller.ModTypes
{
    internal partial class Constants
    {
        internal const string QMM_MANIFEST_FILENAME = "mod.json";
    }

    class QMMModType : BaseModType, IModType
    {
        private QMMData Data { get { return m_ModData as QMMData; } }
        public QMMModType()
        {
        }

        public string GetModName()
        {
            if (Data == null)
                throw new NullReferenceException("Invalid QMM Data");

            return Data.Base_Id;
        }

        public bool ConvertAssemblyReferences(string strDllPath)
        {
            // No need to convert anything for QMM mods.
            return true;
        }

        public void InjectPatches()
        {
            throw new NotImplementedException();
        }

        public bool ParseModData(string strManifestLoc)
        {
            ModDataContainer.RegisterType<IParsedModData, QMMData>();
            string strManifestPath = Path.Combine(strManifestLoc, Constants.QMM_MANIFEST_FILENAME);
            try
            {
                AssignManifestPath(strManifestPath);
            }
            catch (Exception)
            {
                return false;
            }

            ParseData(ModDataContainer.Resolve<QMMData>());
            return m_ModData != null;
        }
    }

}
