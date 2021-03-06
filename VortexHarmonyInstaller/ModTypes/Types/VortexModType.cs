﻿using Microsoft.Practices.Unity;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace VortexHarmonyInstaller.ModTypes
{
    internal partial class Constants
    {
        internal const string VORTEX_MANIFEST_FILENAME = "manifest.json";
    }

    class VortexModType : BaseModType, IModType
    {
        private VortexModData Data { get { return m_ModData as VortexModData; } }

        public VortexModType() {}

        public string GetModName()
        {
            if (Data == null)
                throw new NullReferenceException("Invalid Vortex mod data");

            return Data.Base_Id;
        }

        public bool ParseModData(string strManifestLocation)
        {
            ModDataContainer.RegisterType<IParsedModData, VortexModData>();
            string strManifestPath = Path.Combine(strManifestLocation, Constants.VORTEX_MANIFEST_FILENAME);
            try
            {
                AssignManifestPath(strManifestPath);
            }
            catch (Exception)
            {
                return false;
            }

            ParseData(ModDataContainer.Resolve<VortexModData>());
            return m_ModData != null;
        }

        public void InjectPatches()
        {
            try
            {
                if (null == m_ModData)
                    throw new ArgumentNullException("Mod data is not available");

                VortexModData data = (m_ModData as VortexModData);
                if (null == data)
                    throw new NullReferenceException("Invalid Vortex mod data");

                string[] entryPoint = data.EntryPoint.Split(new string[] { "::" }, StringSplitOptions.None);
                if (entryPoint.Length != 2)
                    throw new NullReferenceException(string.Format("Invalid EntryPoint", entryPoint.Length));

                Type type = m_ModAssembly.GetType(entryPoint[0]);
                if (null == type)
                    throw new NullReferenceException("Failed to find entry Type in mod assembly");

                MethodInfo methodInfo = type.GetMethod(entryPoint[1]);
                if (null == methodInfo)
                    throw new NullReferenceException("Failed to find entry Method in mod assembly");

                bool hasVortexParam = methodInfo.GetParameters().SingleOrDefault() != null;
                if (hasVortexParam)
                {
                    VortexMod mod = VortexMod.GetModEntry(data, VortexPatcher.CurrentDataPath);
                    object[] param = new object[] { mod };

                    methodInfo.Invoke(null, param);
                    AddExposedMod(mod);
                } 
                else
                {
                    methodInfo.Invoke(null, null);
                }
            }
            catch (Exception exc)
            {
                VortexPatcher.Logger.Error("Failed to invoke starter method", exc);
                return;
            }
        }

        public bool ConvertAssemblyReferences(string strDllPath)
        {
            m_ModAssembly = Assembly.LoadFile(strDllPath);

            // No need to convert anything - this is a Vortex mod.
            return true;
        }

        public string[] GetDependencies()
        {
            List<string> dependencies = new List<string>();
            if ((Data != null) && (Data.Base_Dependencies != null))
                dependencies.Concat(Data.Base_Dependencies);

            return dependencies.ToArray();
        }
    }
}
