using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VortexInjectorIPC {
    using SelectCB = Action<int, int, int []>;
    using ContinueCB = Action<bool, int>;
    using CancelCB = Action;

    partial struct Defaults {
        public static int TIMEOUT_MS = 30000;
    }

    internal partial class Util {
        public static async Task<object> Timeout (Task<object> task, int timeout)
        {
            var res = await Task.WhenAny (task, Task.Delay (timeout));
            if (res == task) {
                return await task;
            } else {
                throw new TimeoutException ("task timeout");
            }
        }
    }

    #region Context

    public class ContextDelegates {
        // Vortex's 
        private Func<object, Task<object>> mGetAppVersion;

        private Func<object, Task<object>> mGetCurrentGameVersion;

        // Refers to the location of the assembly we're trying to inject to.
        private Func<object, Task<object>> mGetDatapath;

        // Refers to the location of mods destination patj.
        private Func<object, Task<object>> mGetModspath;

        // Refers to the location of the VML assembly - this
        //  can at times be different from the DataPath, particularly
        //  when using the harmony-patcher-modtype which creates
        //  a Vortex generated mod which is used to store the modified
        //  assembly alongside all depenedncy libraries
        private Func<object, Task<object>> mGetModLoaderPath;

        // Refers to the location of the mod loader's dependency assemblies
        private Func<object, Task<object>> mGetVMLDepsPath;

        // Path to the game extension that's running the injector.
        private Func<object, Task<object>> mGetExtensionPath;

        private Func<object, Task<object>> mGetDeploymentRequired;

        public ContextDelegates (dynamic source)
        {
            mGetAppVersion = source.getAppVersion;
            mGetCurrentGameVersion = source.getCurrentGameVersion;
            mGetDatapath = source.getDatapath;
            mGetModspath = source.getModsPath;
            mGetModLoaderPath = source.getModLoaderPath;
            mGetVMLDepsPath = source.getVMLDepsPath;
            mGetExtensionPath = source.getExtensionPath;
            mGetDeploymentRequired = source.getDeploymentRequired;
        }

        public async Task<string> GetAppVersion ()
        {
            object res = await Util.Timeout (mGetAppVersion (null), Defaults.TIMEOUT_MS);
            return (string)res;
        }

        public async Task<string> GetCurrentGameVersion ()
        {
            object res = await Util.Timeout (mGetCurrentGameVersion (null), Defaults.TIMEOUT_MS);
            return (string)res;
        }

        public async Task<string> GetDataPath ()
        {
            object res = await Util.Timeout (mGetDatapath (null), Defaults.TIMEOUT_MS);
            return (string)res;
        }

        public async Task<string> GetModsPath ()
        {
            object res = await Util.Timeout (mGetModspath (null), Defaults.TIMEOUT_MS);
            return (string)res;
        }

        public async Task<string> GetModLoaderPath ()
        {
            object res = await Util.Timeout (mGetModLoaderPath (null), Defaults.TIMEOUT_MS);
            return (string)res;
        }

        public async Task<string> GetVMLDepsPath ()
        {
            object res = await Util.Timeout (mGetVMLDepsPath (null), Defaults.TIMEOUT_MS);
            return (string)res;
        }

        public async Task<string> GetExtensionPath ()
        {
            object res = await Util.Timeout (mGetExtensionPath (null), Defaults.TIMEOUT_MS);
            return (string)res;
        }

        public async Task<bool> IsDeploymentRequired ()
        {
            object res = await Util.Timeout (mGetDeploymentRequired (null), Defaults.TIMEOUT_MS);
            return ((string)res == "True") ? true : false;
        }
    }

    #endregion

    public class CoreDelegates {
        private ContextDelegates mContextDelegates;
        //private ui.Delegates mUIDelegates;

        public CoreDelegates (dynamic source)
        {
            mContextDelegates = new ContextDelegates (source.context);
            //mUIDelegates = new ui.Delegates (source.ui);
        }

        public ContextDelegates context {
            get {
                return mContextDelegates;
            }
        }

        //public ui.Delegates ui {
        //    get {
        //        return mUIDelegates;
        //    }
        //}
    }
}
