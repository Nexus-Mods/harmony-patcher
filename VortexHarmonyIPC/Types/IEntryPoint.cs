using Mono.Cecil;

using System;
using System.Collections.Generic;

namespace VortexInjectorIPC.Types {
    public interface IEntryPoint {
        string AssemblyPath { get; }
        string DependencyPath { get; }
        string TypeName { get; }
        string MethodName { get; }

        // Serialized JSON which can be used to pass data around.
        //  e.g. when injecting a method call into target points (Serialized argument)
        string ExpandoObjectData { get; }
    }

    public interface IEntryPointDefinitions: IDisposable {
        bool IsEntryPointValid { get; }
        AssemblyDefinition AssemblyDef { get; }
        TypeDefinition TypeDef { get; }
        MethodDefinition MethodDef { get; }
    }
}
