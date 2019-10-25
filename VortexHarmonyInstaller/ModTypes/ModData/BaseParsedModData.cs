using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using VortexHarmonyInstaller.Delegates;

namespace VortexHarmonyInstaller.ModTypes
{
    internal partial class Exceptions
    {
        internal class SetupException: Exception
        {
            internal SetupException(string strMessage)
                : base(strMessage) { }

            internal SetupException(string strMessage, Exception inner)
                : base(strMessage, inner) { }
        }

        internal class ParserFailedException: Exception
        {
            internal ParserFailedException() { }
        }

        internal class AssemblyReplacementException: Exception
        {
            internal AssemblyReplacementException(string strMessage)
                : base(strMessage) { }
        }
    }

    internal partial class Util
    {
        static public string Backup(string strFilePath)
        {
            if (!File.Exists(strFilePath))
                throw new FileNotFoundException(string.Format("{0}, is missing", strFilePath));

            string strBackupPath = strFilePath + Constants.VORTEX_BACKUP;
            File.Copy(strFilePath, strBackupPath, true);
            return strBackupPath;
        }

        static public void RestoreBackup(string strFilePath)
        {
            string strBackupPath = strFilePath + Constants.VORTEX_BACKUP;

            if (!File.Exists(strBackupPath))
                throw new FileNotFoundException(string.Format("{0}, is missing", strBackupPath));

            File.Copy(strBackupPath, strFilePath, true);
            File.Delete(strBackupPath);
        }
        /// <summary>
        /// Function will attempt to identify and return a specific type from
        ///  then provided assembly.
        /// </summary>
        /// <param name="assembly">The assembly we want to look at.</param>
        /// <param name="strTypeName">A string "pattern" the function uses
        ///  to retrieve the appropriate type definition.
        /// </param>
        /// <returns></returns>
        static public TypeDefinition GetTypeDef(AssemblyDefinition assembly, string strTypeName)
        {
            return assembly.MainModule.GetTypes()
                .Where(reference => reference.FullName.Contains(strTypeName))
                .FirstOrDefault();
        }

        static public MethodDefinition GetMethodDef(AssemblyDefinition assembly, string strMethodFullName)
        {
            string[] entryPoint = strMethodFullName.Split(new string[] { "::" }, StringSplitOptions.None);
            return assembly.MainModule.GetType(entryPoint[0]).Methods.FirstOrDefault(x => x.Name == entryPoint[1]);
        }

        static public MethodDefinition GetMethodDef(TypeReference typeRef, string strMethodName)
        {
            TypeDefinition typeDef = typeRef.Resolve();
            return typeDef.Methods.FirstOrDefault(meth => meth.Name == strMethodName);
        }

        static public MethodDefinition GetCtorDef(TypeReference typeRef)
        {
            return GetMethodDef(typeRef, ".ctor");
        }

        public static IEnumerable<ParameterDefinition> FilterMethodDefParams(Instruction instr, string strPattern)
        {
            MethodDefinition methDef = instr.Operand as MethodDefinition;
            if (methDef == null)
            {
                VortexPatcher.Logger.DebugFormat(
                    "{0} cannot be resolved to {1}",
                    instr.Operand.ToString(),
                    nameof(MethodDefinition));
            }
            var parameters = methDef.Parameters;
            return parameters.Where(par => par.ParameterType.FullName.Contains(strPattern));
        }

        /// <summary>
        /// Will insert instructions to create a new object and call a specific method for
        ///  that object before the referenced instruction which will be removed as well.
        /// </summary>
        /// <param name="processor">The IL Processor</param>
        /// <param name="typeRef">Type reference of the object we want to instantiate</param>
        /// <param name="instruction">The instruction we want to replace</param>
        /// <param name="strMethName">The name of the method we want to call</param>
        public static void ReplaceInstruction(ILProcessor processor, TypeReference typeRef, Instruction instruction, string strMethName)
        {
            TypeDefinition typeDefinition = typeRef.Resolve();
            MethodDefinition ctorDef = GetCtorDef(typeRef);
            MethodDefinition instanceDef = GetMethodDef(typeRef, strMethName);
            processor.InsertBefore(instruction, processor.Create(OpCodes.Newobj, ctorDef));
            processor.InsertBefore(instruction, processor.Create(OpCodes.Call, instanceDef));
            processor.Remove(instruction);
        }

        public static void ConvertParameterTypes(MethodDefinition methodDef, TypeReference newType, string strPattern)
        {
            if (!methodDef.HasParameters)
            {
                VortexPatcher.Logger.InfoFormat("{0} does not have any parameters", methodDef.FullName);
                return;
            }

            TypeDefinition typeDef = newType.Resolve();

            var parameters = methodDef.Parameters.ToList();
            foreach (ParameterDefinition parameter in parameters)
            {
                if (parameter.ParameterType.FullName.Contains(strPattern))
                {
                    if (methodDef.Module.Types.Where(type =>
                        type.Name == newType.Name).SingleOrDefault() != null)
                    {
                        methodDef.Module.Types.Add(typeDef);
                    }

                    ParameterDefinition newParam = new ParameterDefinition(parameter.Name, parameter.Attributes, methodDef.Module.ImportReference(newType));
                    int idx = methodDef.Parameters.IndexOf(parameter);
                    methodDef.Parameters.Insert(idx, newParam);
                    methodDef.Parameters.Remove(parameter);
                }
            }
        }

        public static void ConvertGenericArguments(MethodReference methodRef, TypeReference newType)
        {
            throw new NotImplementedException();
            //TypeDefinition TypeDef = methodRef.DeclaringType.Resolve();
            //if (!TypeDef.HasGenericParameters)
            //    return;

            //MethodDefinition methodDef = TypeDef.

            //List<TypeReference> arguments = methodDef.GenericArguments
            //    .Where(arg => arg.Name == newType.Name)
            //    .ToList();

            //foreach (TypeReference argument in arguments)
            //{
            //    int idx = methodDef.GenericArguments.IndexOf(argument);
            //    methodDef.GenericArguments.Insert(idx, newType);
            //    methodDef.GenericArguments.ElementAt(idx).Module.ImportReference(newType);
            //    methodDef.GenericArguments.Remove(argument);
            //}
        }

        public static bool IsInjected(AssemblyDefinition modAssembly)
        {
            try
            {
                return modAssembly.MainModule.AssemblyReferences
                    .Where(reference => reference.Name.Contains(VortexHarmonyInstaller.Constants.INSTALLER_ASSEMBLY_NAME))
                    .SingleOrDefault() != null;
            }
            catch (Exception err)
            {
                VortexPatcher.Logger.Error("Unable to read assembly", err);
                return false;
            }
        }
    }

    public class BaseParsedModData : IEquatable<BaseParsedModData>
    {
        // Highlight mod as cheat, useful to inform users of potential
        //  issues when playing online.
        [JsonIgnore] protected bool m_bIsCheat;
        [JsonIgnore] public bool Base_IsCheat { get { return m_bIsCheat; } }

        // Mod id
        [JsonIgnore] protected string m_strId;
        [JsonIgnore] public string Base_Id { get { return m_strId; } }

        // The mod name we display within the game.
        [JsonIgnore] protected string m_strName;
        [JsonIgnore] public string Base_Name { get { return m_strName; } }

        // The mod's version.
        [JsonIgnore] protected string m_strModVersion;
        [JsonIgnore] public string Base_ModVersion { get { return m_strModVersion; } }

        // Minimum allowed game version
        [JsonIgnore] protected string m_strMinGameVersion;
        [JsonIgnore] public string Base_MinGameVersion { get { return m_strMinGameVersion; } }

        // Mod author
        [JsonIgnore] protected string m_strAuthor;
        [JsonIgnore] public string Base_Author { get { return m_strAuthor; } }

        // Entry points format: NameSpace.ClassName::MethodName
        [JsonIgnore] protected string m_strEntryPoint;
        [JsonIgnore] public string Base_EntryPoint { get { return m_strEntryPoint; } }

        // Nexus game Id
        [JsonIgnore] protected string m_strGameId;
        [JsonIgnore] public string Base_GameId { get { return m_strGameId; } }

        // A list of mod names we wish to load *Before* this mod
        [JsonIgnore] protected string[] m_rgDependencies;
        [JsonIgnore] public string[] Base_Dependencies { get { return m_rgDependencies; } }

        // The name of the assembly file.
        [JsonIgnore] protected string m_strAssemblyName;
        [JsonIgnore] public string Base_AssemblyName { get { return m_strAssemblyName; } }

        protected MonoBehaviourHooks m_MonoHooks = null;
        public MonoBehaviourHooks Hooks { get { return m_MonoHooks; } }

        public virtual void AssignBaseData(BaseParsedModData other)
        {
            m_strId = other.Base_Id;
            m_strName = other.Base_Name;
            m_strGameId = other.Base_GameId;
            m_strAuthor = other.Base_Author;
            m_rgDependencies = other.Base_Dependencies;
            m_strEntryPoint = other.Base_EntryPoint;
            m_bIsCheat = other.Base_IsCheat;
            m_strMinGameVersion = other.Base_MinGameVersion;
            m_strModVersion = other.Base_ModVersion;
            m_strAssemblyName = other.Base_AssemblyName;
            m_MonoHooks = new MonoBehaviourHooks();
        }

        public virtual void AssignAssemblyName(string strManifestPath)
        {
            if (m_strAssemblyName == null)
            {
                string strDir = Path.GetDirectoryName(strManifestPath);
                string strAssemblyName = Directory.GetFiles(strDir, "*.dll", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (strAssemblyName != null)
                {
                    m_strAssemblyName = strAssemblyName;
                }
            }
        }

        public bool Equals(BaseParsedModData other)
        {
            return m_strId == other.Base_Id;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            return ((obj is BaseParsedModData modData) 
                && (Equals(modData)));
        }

        public override int GetHashCode()
        {
            return Base_Id.GetHashCode();
        }
    }

    public class BaseModType
    {
        // Manifest filePath (must be absolute path)
        protected string m_strManifestPath;
        internal string ManifestPath { get { return m_strManifestPath; } }

        // Reference to this mod's assembly
        protected Assembly m_ModAssembly = null;
        public Assembly ModAssembly { get { return m_ModAssembly; } }

        // Will hold parsed mod information from JSON files.
        protected IParsedModData m_ModData = null;
        internal IParsedModData ModData { get { return m_ModData; } }

        protected static Unity.UnityContainer m_ModDataContainer = new Unity.UnityContainer();
        internal static Unity.UnityContainer ModDataContainer { get { return m_ModDataContainer; } }

        protected static List<IExposedMod> m_ExposedMods = new List<IExposedMod>();
        public static List<IExposedMod> ExposedMods { get { return m_ExposedMods; } }

        public BaseModType()
        {
        }

        protected void AddExposedMod(IExposedMod mod)
        {
            if (!m_ExposedMods.Contains(mod))
                m_ExposedMods.Add(mod);
        }

        protected void AssignManifestPath(string strManifestPath)
        {
            if (!Path.IsPathRooted(strManifestPath))
                throw new Exceptions.SetupException("Must provide absolute path to manifest file");

            if (!File.Exists(strManifestPath))
                throw new FileNotFoundException(String.Format("{0} does not exist", strManifestPath));

            m_strManifestPath = strManifestPath;
        }

        protected void ParseData(IParsedModData parsedData)
        {
            m_ModData = parsedData;
            ParseManifest();
        }

        protected void ParseManifest()
        {
            if (!m_ModData.ParseManifest(ManifestPath))
            {
                m_ModData = null;
            }
        }
    }
}
