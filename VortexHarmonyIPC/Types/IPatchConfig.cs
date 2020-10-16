using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VortexInjectorIPC.Types {
    public interface IPatchConfig {
        // The command property defines what sort of operation
        //  we expect the injector to run.
        string Command { get; }

        // Path to the calling extension
        string ExtensionPath { get; }

        // The method call we want to inject.
        IEntryPoint SourceEntryPoint { get; }

        // The target method calls we want to inject.
        //  The source method 
        IEntryPoint [] TargetEntryPoints { get; }
    }
}
