const { spawn } = require('child_process');
const path = require('path');
const { app, remote } = require('electron');
const Promise = require('bluebird');
const msbuildLib = require('msbuild');
const { fs, log, selectors, util } = require('vortex-api');
const semver = require('semver');

const uniApp = app || remote.app;

const LOG_FILE_PATH = path.join(uniApp.getPath('userData'), 'harmonypatcher.log');
const PATCHER_EXEC = 'VortexHarmoyExec.exe';
const MODULE_PATH = path.join(util.getVortexPath('modules_unpacked'), 'harmony-patcher', 'dist');
const EXEC_PATH = path.join(MODULE_PATH, PATCHER_EXEC);

const LOAD_ORDER_FILE = 'load_order.txt';

const FRAMEWORK_VER_PREFIX = 'FrameworkVersion=';
const ASSEMBLY_REF_QUERY_PREFIX = 'FoundAssembly=';

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

// INVALID_ENTRYPOINT = -1,
// MISSING_FILE = -2,
// INVALID_ARGUMENT = -3,
// FILE_OPERATION_ERROR = -4,
// UNHANDLED_FILE_VERSION = -5,
// FAILED_DOWNLOAD = -6,
// UNKNOWN = -13,
const PATCHER_ERRORS = {
  '-1' : 'Invalid Entry Point',
  '-2' : 'Missing File',
  '-3' : 'Patcher received invalid argument',
  '-4' : 'File operation failed',
  '-5' : 'Encountered unhandled assembly version',
  '-6' : 'Essential download has failed',
  '-7' : 'Assembly reference lookup failed',
  '-13' : `Unknown error - please include your "${LOG_FILE_PATH}" file when reporting this error`,
};

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
  let lastError;

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
    : queryNETVersion(managedDirPath)
        .then(version => buildVIGO(managedDirPath, version));

  // MSBuild debug function - do not remove.
  // return copyAssemblies(managedDirPath)
  //   .then(() => queryNETVersion(dataPath))
  //   .then(version => debugMSBuild(version));

  const isMerged = (unityEngineDir !== undefined) ? true : false;
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
    const wstream = fs.createWriteStream(LOG_FILE_PATH);
    let patcher;
    modsPath = !!modsPath ? modsPath : path.join(managedDirPath, 'VortexMods');

    // Given the new merging functionality, the assembly we wish to patch
    //  may be located inside the __merged mod directory which will not contain
    //  the mscorlib assembly alongside it; this will block the injector from
    //  re-enabling reflection when needed. The injector _must_ be aware of the
    //  location of the mscorlib assembly which sits alongside the UnityEngine.
    //  We modify the dataPath to include the expected location of the mscorlib assembly;
    //  the injector will decipher this.
    dataPath = (unityEngineDir !== undefined)  ? `${dataPath}::${unityEngineDir}` : dataPath;
    try {
      patcher = spawn(EXEC_PATH, ['-g', extensionPath,
                                  '-m', dataPath,
                                  '-e', entryPoint,
                                  '-i', MODULE_PATH,
                                  '-x', modsPath,
                                  !!remove ? '-r' : '',
                                  !!injectVIGO ? '-v' : '']);
    } catch (err) {
      return reject(err);
    }

    patcher.stdout.on('data', data => {
      const formatted = data.toString().split('\n');
      formatted.forEach(line => {
        wstream.write(line + '\n');
      });
    });

    patcher.stderr.on('data', data => {
      const formatted = data.toString().split('\n').filter(line => !!line);
      lastError = parseErrorData(formatted);
      formatted.forEach(line => {
        wstream.write(line + '\n');
      });
    });

    patcher.on('error', err => {
      wstream.close();
      return reject(err);
    });

    patcher.on('close', code => {
      wstream.close();
      return (code !== 0)
        ? reject(createError(code, lastError))
        : resolve();
    });
  }))
  .catch(err => (!!context)
    ? context.api.showErrorNotification('patch injector has reported issues', err)
    : log('error', 'patch injector has reported issues', err));
}

function addLoadOrderPage(context, gameId, loadOrderInfo, gameArtURL, preSort, filter, callback) {
  context.registerLoadOrderPage({
    gameId,
    loadOrderInfo,
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
  const coercedVersion = semver.coerce(version);
  return (semver.valid(coercedVersion))
    ? (coercedVersion.major > 3)
      ? `v${coercedVersion.major}.${coercedVersion.minor}`
      : 'v3.5'
    : 'v3.5';
}

function injectorQuery(dataPath, pattern, assemblyName = undefined) {
  const wstream = fs.createWriteStream(LOG_FILE_PATH);
  let lastError;
  let match;
  const args = (assemblyName === undefined)
    ? ['-q', dataPath]
    : ['-a', dataPath + '::' + assemblyName];
  return new Promise((resolve, reject) => {
    let patcher;
    try {
      patcher = spawn(EXEC_PATH, args);
    } catch (err) {
      return reject(err);
    }

    patcher.stdout.on('data', data => {
      const formatted = data.toString().split('\n');
      formatted.forEach(line => {
        wstream.write(line + '\n');
      });
      match = (match === undefined)
        ? formatted.find(line => line.indexOf(pattern) !== -1)
        : match;
    });

    patcher.stderr.on('data', data => {
      const formatted = data.toString().split('\n').filter(line => !!line);
      lastError = parseErrorData(formatted);
      formatted.forEach(line => {
        wstream.write(line + '\n');
      });
    });

    patcher.on('error', err => {
      wstream.close();
      return reject(err);
    });

    patcher.on('close', code => {
      wstream.close();
      return (code !== 0)
        ? reject(createError(code, lastError))
        : resolve(match);
    });
  }).then(match => (match !== undefined)
    ? match.substring(pattern.length)
    : undefined);
}

function queryAssemblyReference(dataPath, assemblyName) {
  return injectorQuery(dataPath, ASSEMBLY_REF_QUERY_PREFIX, assemblyName);
}

function queryNETVersion(dataPath) {
  return injectorQuery(dataPath, FRAMEWORK_VER_PREFIX)
    .then(version => (version !== undefined) ? version : '3.5.0.0');
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
      .then(() => restore(() => build(() => resolve())))
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
  msbuild.sourcePath = VIGO_PROJ;
  msbuild.configuration = `Release;TargetFrameworkVersion=${_BUILD_VER}`;
  msbuild.overrideParams.push('/verbosity:quiet');
  msbuild.overrideParams.push('/clp:ErrorsOnly');
  msbuild.build();
}

function restore(cb) {
  var msbuild = new msbuildLib(cb);
  msbuild.sourcePath = VIGO_DIR;
  msbuild.configuration = `Release;TargetFrameworkVersion=${_BUILD_VER}`;
  msbuild.overrideParams.push('/t:restore');
  msbuild.overrideParams.push('/verbosity:quiet');
  msbuild.overrideParams.push('/clp:ErrorsOnly');
  msbuild.build();
}

function raiseConsentNotification(context, gameId, isMerged) {
  const notifId = `${gameId}-patch-consent`;
  const api = context.api;
  const t = (text, i18Options) => api.translate(text, i18Options);
  const howToRemovePatchText = (isMerged)
    ? t('clicking the "Purge Mods" button.')
    : t('clicking the "Patcher-Remove" button.');

  api.sendNotification({
    noDismiss: true,
    allowSuppress: true,
    id: notifId,
    type: 'critical',
    message: t('Game assembly patching is required',
      { ns: NAMESPACE }),
      actions: [
        { title: 'More', action: () => consentDialog() },
      ],
  });

  const consentDialog = () => new Promise((resolve, reject) => {
    return api.showDialog('question', 'Harmony Patcher', {
      bbcode: t('{{gameId}} is designed to use Vortex\'s '
            + 'game assembly patching implementation; what this means is - '
            + 'Vortex will inject code into your game\'s assembly which aims to execute '
            + 'our mod loader.<br /><br />'
            + 'This is easily reversible by {{how}}',
            { replace: { how: howToRemovePatchText, gameId }, ns: NAMESPACE }),
    },
    [
      { label: 'Ok', action: () => {
        api.dismissNotification(notifId);
        api.suppressNotification(notifId, true);
        return resolve();
      }},
      { label: 'Cancel', action: () => reject(new util.UserCanceled()) },
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
  raiseConsentNotification,
};
