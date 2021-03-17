const { spawn } = require('child_process');
const path = require('path');
const { app, remote } = require('electron');
const getVersion = require('exe-version');
const Promise = require('bluebird');
const msbuildLib = require('msbuild');
const { fs, log, selectors, util } = require('vortex-api');
const semver = require('semver');

const uniApp = app || remote.app;

const MODULE_PATH = path.join(util.getVortexPath('modules_unpacked'), 'harmony-patcher', 'dist');

const LOAD_ORDER_FILE = 'load_order.txt';

const VIGO_ASSEMBLY = 'VortexUnity.dll';
const VIGO_DIR = path.resolve(MODULE_PATH, '..', 'VortexUnity');
const VIGO_PROJ = path.join(VIGO_DIR, 'VortexUnityManager.csproj');

const NAMESPACE = 'harmony-patcher';

let _BUILD_VER;

// A list of Unity Assemblies required to build
//  VIGO - if we can't find any of these inside the
//  game's dataPath, we assume that VIGO is not required.
const UNITY_ASSEMBLIES = [
  'UnityEngine.dll',
  'UnityEngine.AssetBundleModule.dll',
  'UnityEngine.CoreModule.dll',
  'UnityEngine.IMGUIModule.dll',
  'UnityEngine.TextRenderingModule.dll',
  'UnityEngine.UI.dll',
  'UnityEngine.UIModule.dll',
];

function translateLegacyPatcherCall(extensionPath, dataPath, entryPoint, remove, modsPath, injectVIGO) {
  const dataPathDir = path.extname(dataPath) !== '' ? path.dirname(dataPath) : dataPath;
  const VMLEntryPoint = {
    AssemblyPath: path.join(dataPathDir, 'VortexHarmonyInstaller.dll'),
    TypeName: 'VortexHarmonyInstaller.VortexPatcher',
    MethodName: 'Patch',
    DependencyPath: MODULE_PATH,
    ExpandoObjectData: modsPath,
  };

  const VIGOEntryPoint = {
    AssemblyPath: path.join(dataPathDir, 'VortexUnity.dll'),
    TypeName: 'VortexUnity.VortexUnityManager',
    MethodName: 'RunUnityPatcher',
    DependencyPath: MODULE_PATH,
  }

  const target = entryPoint.split('::');

  const targetAssemblyPath = path.join(dataPathDir, 'Assembly-CSharp.dll');
  const targetEntryPoint = {
    AssemblyPath: targetAssemblyPath,
    TypeName: target[0],
    MethodName: target[1],
    DependencyPath: (path.extname(dataPath) !== '')
      ? path.dirname(dataPath) : dataPath,
  };

  const VMLPatchConfig = {
    Command: (remove) ? 'PurgeVML' : 'DeployVML',
    ExtensionPath: extensionPath,
    SourceEntryPoint: VMLEntryPoint,
    TargetEntryPoints: [ targetEntryPoint ],
  };

  const VIGOPatchConfig = {
    Command: (remove) ? 'PurgeVIGO' : 'DeployVIGO',
    ExtensionPath: extensionPath,
    SourceEntryPoint: VIGOEntryPoint,
    TargetEntryPoints: [ targetEntryPoint ],
  }

  return (injectVIGO)
    ? [VMLPatchConfig, VIGOPatchConfig]
    : [VMLPatchConfig];
}

// Usage:
//  -m "AbsolutePath" -> Managed directory or the game's datapath
//    (Location of the game assembly we want to inject into)
//
//  -e "Namespace.Classname::Methodname" -> the entry point where we want
//    to inject the patcher function.
//
//  -r -> When provided, will inform the injector that we wish to remove
//    the patcher function.
//
//  -g -> The path to the location of the Vortex game extension using the patcher.
//
//  -i -> Path to the libraries which the patcher is using.
//
//  -x -> Location where we're planning to store our mods.
//
//  -v -> Decides whether VIGO should be built or not.
function runPatcher(extensionPath, dataPath, entryPoint, remove, modsPath, context, injectVIGO, unityEngineDir) {
  if (context?.api?.ext?.applyInjectorCommand === undefined) {
    log('error', 'unable to run patcher', { extensionPath });
    if (context === undefined) {
      return Promise.reject(new Error('Deprecated game extension, please include log file when reporting this'));
    } else {
      return Promise.reject(new Error('Harmony Injector API is unavailable'));
    }
  }

  // The assembly path may sometimes differ from the location of the
  //  UnityEngine assemblies. We need these to build VIGO, so in these
  //  circumstances unityEngineDir should be populated with the absolute
  //  path to the directory where the Unity assemblies are located.
  const managedDirPath = (unityEngineDir !== undefined)
    ? unityEngineDir
    : (path.extname(dataPath) === '.dll')
      ? path.dirname(dataPath)
      : dataPath;

  // Runs the VIGO build functionality.
  const buildInGameUI = () => (remove || !injectVIGO)
    ? Promise.resolve()
    : queryNETVersion(path.join(managedDirPath, 'mscorlib.dll'))
        .then(version => buildVIGO(managedDirPath, version));

  // MSBuild debug function - do not remove.
  // return copyAssemblies(managedDirPath)
  //   .then(() => queryNETVersion(dataPath))
  //   .then(version => debugMSBuild(version));

  return buildInGameUI()
    .then(() => cleanAssemblies())
    .catch(err => {
      // VIGO failed the build; by default VIGO is optional
      //  and the harmony patcher should still be able to work
      //  correctly so we log this and continue.
      (!!context)
        ? context.api.showErrorNotification('VIGO failed to build', err)
        : log('error', 'patch injector has reported issues', err);
      return cleanAssemblies();
    })
  .then(() => new Promise((resolve, reject) => {
    modsPath = !!modsPath ? modsPath : path.join(managedDirPath, 'VortexMods');

    // Given the new merging functionality, the assembly we wish to patch
    //  may be located inside the __merged mod directory which will not contain
    //  the mscorlib assembly alongside it; this will block the injector from
    //  re-enabling reflection when needed. The injector _must_ be aware of the
    //  location of the mscorlib assembly which sits alongside the UnityEngine.
    //  We modify the dataPath to include the expected location of the mscorlib assembly;
    //  the injector will decipher this.
    //dataPath = (unityEngineDir !== undefined)  ? `${dataPath}::${unityEngineDir}` : dataPath;
    try {
      const patches = translateLegacyPatcherCall(extensionPath, dataPath, entryPoint, remove, modsPath, injectVIGO);
      const modLoaderPath = path.extname(dataPath) !== '' ? path.dirname(dataPath) : dataPath;
      context.api.ext.applyInjectorCommand(patches[0], modLoaderPath, undefined, (err, result) => {
        if (err !== undefined) {
          return reject(err);
        }

        if (patches[1] !== undefined) {
          context.api.ext.applyInjectorCommand(patches[1], modLoaderPath, undefined, (err, result) => {
            if (err !== undefined) {
              return reject(err);
            }

            return resolve();
          });
        } else {
          return resolve();
        }
      })
    } catch (err) {
      return reject(err);
    }
  }));
}

function addLoadOrderPage(context, gameId, createInfoPanel, gameArtURL, preSort, filter, callback) {
  context.registerLoadOrderPage({
    gameId,
    createInfoPanel,
    gameArtURL,
    preSort: (items) => {
      if (!!preSort && typeof(preSort) === "function") {
        return preSort(items);
      }
      return Promise.resolve(items);
    },
    filter: (mods) => {
      if (!!filter && typeof(filter) === "function") {
        // Forward the new load order to the game extension's callback.
        return filter(mods).filter(mod => mod.type !== 'harmonypatchermod');
      }
      return mods.filter(mod => mod.type !== 'harmonypatchermod');
    },
    callback: (loadOrder) => {
      if (!!callback && typeof(callback) === "function") {
        // Forward the new load order to the game extension's callback.
        callback(loadOrder);
      }
      saveLoadOrder(context, gameId, loadOrder)
    }
  })
}

function getTargetFramework(version) {
  const MSBUILD_VERSIONS = {
    '2.0': '2.0.50727', 
    '3.0':'3.0',
    '3.5': '3.5',
    '4.0': '4.0', 
    '4.5': '4.0',
    '4.6': '4.0',
    '4.7': '4.0',
    '4.8': '4.0',
  };

  const coercedVersion = semver.coerce(version);
  return (semver.valid(coercedVersion))
    ? (coercedVersion.major > 3)
      ? `v${MSBUILD_VERSIONS[coercedVersion.major + '.' + coercedVersion.minor]}`
      : 'v3.5'
    : 'v3.5';
}

function queryNETVersion(corLibAssembly) {
  let version = '3.5.0.0';
  try {
    version = getVersion.default(corLibAssembly);
  } catch (err) {
    version = '3.5.0.0';
  }

  return Promise.resolve(version);
}

function cleanAssemblies() {
  const logAndContinue = (err) => {
    log('error', 'unable to remove assembly', err);
    return Promise.resolve();
  };
  return Promise.each(UNITY_ASSEMBLIES, assembly => {
    const expectedPath = path.join(MODULE_PATH, assembly);
    return fs.removeAsync(expectedPath)
      .then(() => Promise.resolve())
      .catch(err => (err.code !== 'ENOENT')
        ? logAndContinue(err)
        : Promise.resolve());
  })
}

function copyAssemblies(dataPath) {
  return fs.readdirAsync(dataPath).then(entries => {
    const filtered = entries
      .filter(entry => UNITY_ASSEMBLIES.indexOf(entry) !== -1)
      .map(entry => path.join(dataPath, entry));
    return Promise.each(filtered, entry => fs.copyAsync(entry, path.join(MODULE_PATH, path.basename(entry))));
  })
}

function buildVIGO(dataPath, version) {
  _BUILD_VER = getTargetFramework(version);
  const startBuild = () => new Promise((resolve, reject) =>
    copyAssemblies(dataPath)
      .then(() => build(() => resolve()))
      .catch(err => reject(err)));

  return fs.lstatAsync(path.join(dataPath, VIGO_ASSEMBLY))
    .then((stats) => Promise.resolve())
    .catch(err => (err.code === 'ENOENT') ? startBuild() : Promise.reject(err));
}

// The msbuild module we use to build our projects does not
//  provide enough information when a build fails; this function
//  can be used through Vortex to debug any build failures.
function debugMSBuild(version) {
  const netBuildVer = getTargetFramework(version);
  const msbuildLocation = 'C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Community\\MSBuild\\15.0\\bin\\msbuild.exe';
  const params = [
    'D:\\Projects\\kek\\node_modules\\harmony-patcher\\VortexUnity\\VortexUnityManager.csproj',
    `/p:configuration=Release;TargetFrameworkVersion=${netBuildVer}`,
    '/verbosity:quiet',
    '/clp:ErrorsOnly',
  ]

  const debug = spawn(msbuildLocation, params);
  debug.stdout.on('data', data => {
    const formatted = data.toString().split('\n');
    formatted.forEach(line => {
      console.log(line);
    });
  });

  debug.stderr.on('data', data => {
    const formatted = data.toString().split('\n');
    formatted.forEach(line => {
      console.log(line);
    });
  });

  debug.on('error', err => {
    console.log(err);
  });

  debug.on('close', code => {
    console.log(code);
  });
}

function build(cb) {
  var msbuild = new msbuildLib(cb);
  // Use the .NET msbuild executable.
  msbuild.version = _BUILD_VER.substr(1);
  msbuild.sourcePath = VIGO_PROJ;
  msbuild.configuration = `Release;TargetFrameworkVersion=${_BUILD_VER}`;
  msbuild.overrideParams.push('/verbosity:quiet');
  msbuild.overrideParams.push('/clp:ErrorsOnly');
  msbuild.build();
}

function restore(cb) {
  // .NET msbuild does not appear to support restore
  //   the same way that VS 2017 does - that's fine,
  //   we provide the required assemblies anyway.
  var msbuild = new msbuildLib(cb);
  msbuild.version = _BUILD_VER.substr(1);
  msbuild.sourcePath = VIGO_PROJ;
  msbuild.configuration = `Release;TargetFrameworkVersion=${_BUILD_VER}`;
  msbuild.overrideParams.push('/t:restore');
  msbuild.overrideParams.push('/verbosity:quiet');
  msbuild.overrideParams.push('/clp:ErrorsOnly');
  msbuild.build();
}

function raiseConsentDialog(context, gameId, textOverride) {
  const notifId = `${gameId}-harmony-patch-consent`;
  const api = context.api;
  const state = api.store.getState();
  const game = selectors.gameById(state, gameId);
  if (game === undefined) {
    api.showErrorNotification('Unable to find game object', gameId);
    return Promise.resolve();
  }
  const isMerged = (!!game.details && !!game.details.harmonyPatchDetails);
  const isSuppressed = util.getSafe(state, ['settings', 'notifications', 'suppress', notifId], false);
  const t = (text, i18Options) => api.translate(text, i18Options);
  const howToRemovePatchText = (isMerged)
    ? t('clicking the "Purge Mods" button.')
    : t('clicking the "Patcher-Remove" button.');

  if (isSuppressed) {
    return Promise.resolve();
  }

  return new Promise((resolve, reject) => {
    return api.showDialog('question', 'Harmony Patcher', {
      bbcode: (!!textOverride)
        ? t(textOverride)
        : t('Vortex is able to provide and execute a mod loader for "{{gameName}}" by '
          + 'patching the game assembly. In order to do so, Vortex will need to inject code '
          + 'into your game\'s assembly directly.<br /><br />'
          + 'This process can be reversed at any time by {{how}}',
          { replace: { how: howToRemovePatchText, gameName: game.name }, ns: NAMESPACE }),
    },
    [
      { label: 'Cancel', action: () => reject(new util.UserCanceled()) },
      { label: 'Ok', action: () => {
        api.suppressNotification(notifId);
        api.dismissNotification(notifId);
        return resolve();
      }},
    ]);
  });
}

function createError(errorCode, lastError) {
  const createErrorMessage = (error) => {
    return (!!error.RaisedException)
      ? error.Message + '\n'
        + error.RaisedException.ClassName
        + ':' + error.RaisedException.Message
      : error.Message;
  };

  if (lastError !== undefined) {
    let err;
    if (!!lastError.errors) {
      err = lastError.errors
        .filter(errInstance => errInstance.ErrorCode !== 0)
        .map(errInstance => createErrorMessage(errInstance)).join('\n');
    } else {
      err = createErrorMessage(lastError);
    }
    return new Error(err);
  } else {
    const errorKeys = Object.keys(PATCHER_ERRORS);
    return (errorKeys.find(key => errorCode.toString() === key) !== undefined)
      ? new Error(PATCHER_ERRORS[errorCode.toString()])
      : new Error(PATCHER_ERRORS['-13']);
  }
}

function parseErrorData(data) {
  const errorData = (Array.isArray(data))
    ? `{ "errors": [${data.toString()}]}` : data;
  try {
    const error = JSON.parse(errorData);
    return error;
  } catch (err) {
    log('error', 'Failed to parse injector response message', err);
    return undefined;
  }
}

function findAssemblyFile(modFolder) {
  let foundAssembly = false;
  return fs.readdirAsync(modFolder).then(entries => {
    const filtered = entries.filter(entry => path.extname(entry) === '');
    return new Promise((resolve) => {
      return Promise.each(filtered, entry => foundAssembly ? Promise.resolve() : fs.readdirAsync(path.join(modFolder, entry))
        .then(files => {
          const assembly = files.find(file => file.endsWith('.dll'));
          foundAssembly = assembly !== undefined;
          return foundAssembly ? resolve(assembly) : Promise.resolve();
      }))
      // Couldn't find anything.
      .then(() => resolve(undefined));
    });
  });
}

function saveLoadOrder(context, gameId, loadOrder) {
  const state = context.api.store.getState();
  const mods = util.getSafe(state, ['persistent', 'mods', gameId], []);
  const moddingPath = selectors.modPathsForGame(state, gameId)[''];
  const destination = path.join(moddingPath, LOAD_ORDER_FILE);
  const stagingFolder = selectors.installPathForGame(state, gameId);
  let assembliesInOrder = [];
  const keys = Object.keys(loadOrder);
  return Promise.each(keys, key => {
    const entry = loadOrder[key];
    const modFolder = path.join(stagingFolder, mods[key].installationPath);
    // We expect an additional folder inside the installation path which contains all mod files.
    return findAssemblyFile(modFolder)
      .then(assemblyName => {
        if (!!assemblyName) {
          assembliesInOrder.push(assemblyName);
          return Promise.resolve();
        } else {
          log('error', 'Unable to find mod assembly', entry);
          return Promise.resolve();
        }
      });
    })
    .then(() => fs.writeFileAsync(destination, assembliesInOrder.join('\n'),{ encoding: 'utf-8' }))
    .catch(err => context.api.showErrorNotification('Failed to save load order', err));
}

module.exports = {
  addLoadOrderPage,
  runPatcher,
  raiseConsentDialog,
};
