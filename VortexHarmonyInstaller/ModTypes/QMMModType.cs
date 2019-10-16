using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Unity;

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
                throw new InvalidDataException("Invalid QMM Data");

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
            catch (Exception exc)
            {
                return false;
            }

            ParseData(ModDataContainer.Resolve<QMMData>());
            return m_ModData != null;
        }
    }

}
