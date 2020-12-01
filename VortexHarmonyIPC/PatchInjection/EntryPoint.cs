using Mono.Cecil;

using System;
using System.Linq;

using Newtonsoft.Json.Linq;

using VortexInjectorIPC.Types;
using System.IO;

namespace VortexInjectorIPC {
    public class EntryPointDefinitions: IEntryPointDefinitions, IDisposable {
        private bool m_entryPointValid = false;
        public bool IsEntryPointValid => m_entryPointValid;

        private AssemblyDefinition m_assemblyDef = null;
        public AssemblyDefinition AssemblyDef => m_assemblyDef;

        private TypeDefinition m_typeDef = null;
        public TypeDefinition TypeDef => m_typeDef;

        private MethodDefinition m_methodDef = null;
        public MethodDefinition MethodDef => m_methodDef;

        private bool disposedValue = false;

        public EntryPointDefinitions (IEntryPoint entryPoint)
        {
            try {
                m_assemblyDef = AssemblyDefinition.ReadAssembly (entryPoint.AssemblyPath);
                m_typeDef = m_assemblyDef.MainModule.GetType (entryPoint.TypeName);
                m_methodDef = m_typeDef.Methods.First (x => x.Name == entryPoint.MethodName);
                m_entryPointValid = true;
            } catch (Exception) {
                if (m_assemblyDef != null) {
                    Dispose (true);
                }
                m_entryPointValid = false;
            }
        }

        public EntryPointDefinitions (ref AssemblyDefinition assDef, IEntryPoint entryPoint)
        {
            try {
                m_assemblyDef = assDef;
                m_typeDef = m_assemblyDef.MainModule.GetType (entryPoint.TypeName);
                m_methodDef = m_typeDef.Methods.First (x => x.Name == entryPoint.MethodName);
                m_entryPointValid = true;
            } catch (Exception) {
                if (m_assemblyDef != null) {
                    Dispose (true);
                }
                m_entryPointValid = false;
            }
        }

        private void Dispose (bool disposing)
        {
            if (!disposedValue) {
                if (disposing) {
                    m_assemblyDef.Dispose ();
                }

                m_assemblyDef = null;
                m_typeDef = null;
                m_methodDef = null;
                disposedValue = true;
            }
        }

        public void Dispose ()
        {
            Dispose (true);
        }
    }

    public class EntryPoint: IEntryPoint
    {
        private string m_assemblyPath;
        public string AssemblyPath { get => m_assemblyPath; }

        private string m_dependencyPath;
        public string DependencyPath { get => m_dependencyPath; }

        private string m_typeName;
        public string TypeName => m_typeName;

        private string m_methodName;
        public string MethodName => m_methodName;

        private string m_expandoObjectData;
        public string ExpandoObjectData => m_expandoObjectData;

        public EntryPoint(JToken token)
        {
            m_assemblyPath = token ["AssemblyPath"].ToString ();
            m_typeName = token["TypeName"].ToString ();
            m_methodName = token["MethodName"].ToString();

            m_dependencyPath = token ["DependencyPath"] != null
                ? token ["DependencyPath"].ToString()
                : Path.GetDirectoryName (m_assemblyPath);

            m_expandoObjectData = token ["ExpandoObjectData"] != null
                ? token ["ExpandoObjectData"].ToString ()
                : string.Empty;
        }

        public EntryPoint(string assemblyPath,
                          string typeName,
                          string methodName,
                          string dependencyPath = null,
                          string expandoData = null)
        {
            m_assemblyPath = assemblyPath;
            m_typeName = typeName;
            m_methodName = methodName;
            m_dependencyPath = dependencyPath != null
                ? dependencyPath : Path.GetDirectoryName(assemblyPath);

            m_expandoObjectData = expandoData != null
                ? expandoData : string.Empty;
        }

        public IEntryPointDefinitions GetDefinitions ()
        {
            return new EntryPointDefinitions (this);
        }

        public override string ToString ()
        {
            return m_typeName + "::" + m_methodName;
        }
    }
}
