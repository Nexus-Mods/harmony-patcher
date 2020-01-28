const { spawn } = require('child_process');
const React = require('react');
const BS = require('react-bootstrap');
const { connect } = require('react-redux');
const path = require('path');
const { app, remote } = require('electron');
const Promise = require('bluebird');
const msbuildLib = require('msbuild');
const { actions, fs, DraggableList, FlexLayout, log, MainPage, selectors, util } = require('vortex-api');

const uniApp = app || remote.app;

const LOG_FILE_PATH = path.join(uniApp.getPath('userData'), 'harmonypatcher.log');
const PATCHER_EXEC = 'VortexHarmoyExec.exe';
const MODULE_PATH = path.join(util.getVortexPath('modules_unpacked'), 'harmony-patcher', 'dist');
const EXEC_PATH = path.join(MODULE_PATH, PATCHER_EXEC);

const LOAD_ORDER_FILE = 'load_order.txt';

const VIGO_ASSEMBLY = 'VortexUnity.dll';
const VIGO_DIR = path.resolve(MODULE_PATH, '..', 'VortexUnity');
const VIGO_PROJ = path.join(VIGO_DIR, 'VortexUnityManager.csproj');

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
function runPatcher(extensionPath, dataPath, entryPoint, remove, modsPath, context) {
  let lastError;
  const managedDirPath = (path.extname(dataPath) !== '')
    ? path.dirname(dataPath)
    : dataPath;
  const buildInGameUI = () => (remove) ? Promise.resolve() : buildVIGO(managedDirPath);
  //return copyAssemblies(managedDirPath).then(() => debugMSBuild());
  return buildInGameUI().then(() => cleanAssemblies())
  .then(() => new Promise((resolve, reject) => {
    const wstream = fs.createWriteStream(LOG_FILE_PATH);
    let patcher;
    modsPath = !!modsPath ? modsPath : path.join(managedDirPath, 'VortexMods');
    try {
      patcher = spawn(EXEC_PATH, ['-g', extensionPath,
                                  '-m', dataPath,
                                  '-e', entryPoint,
                                  '-i', MODULE_PATH,
                                  '-x', modsPath,
                                  !!remove ? '-r' : '']);
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
  //.then(() => );
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

function buildVIGO(dataPath) {
  return new Promise((resolve, reject) =>
    fs.statAsync(path.join(dataPath, VIGO_ASSEMBLY))
      .then(() => reject(new Error('File exists')))
      .catch(err => err.code !== 'ENOENT'
        ? reject(err)
        : copyAssemblies(dataPath))
  .then(() => restore(() => build(() => resolve()))))
  .catch(err => err.message === 'File exists'
    ? Promise.resolve()
    : createError('-13', err));
}

// The msbuild module we use to build our projects does not
//  provide enough information when a build fails; this function
//  can be used through Vortex to debug any build failures.
function debugMSBuild() {
  const msbuildLocation = 'C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Community\\MSBuild\\15.0\\bin\\msbuild.exe';
  const params = [
    'D:\\Projects\\kek\\node_modules\\harmony-patcher\\VortexUnity\\VortexUnityManager.csproj',
    '/p:configuration=Release;TargetFrameworkVersion=v3.5',
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
  msbuild.configuration = 'Release;TargetFrameworkVersion=v3.5';
  msbuild.overrideParams.push('/verbosity:quiet');
  msbuild.overrideParams.push('/clp:ErrorsOnly');
  msbuild.build();
}

function restore(cb) {
  var msbuild = new msbuildLib(cb);
  msbuild.sourcePath = VIGO_DIR;
  msbuild.configuration = 'Release;TargetFrameworkVersion=v3.5';
  msbuild.overrideParams.push('/t:restore');
  msbuild.overrideParams.push('/verbosity:quiet');
  msbuild.overrideParams.push('/clp:ErrorsOnly');
  msbuild.build();
}

// Aimed at creating the default UI settings for a gameId.
//  1. Will register the "Patcher - Add/Remove" buttons.
//  2. Will register the load order page.
function initHarmonyUI(context, extensionPath, dataPath, entryPoint, modsPath, gameId, onErrorCB) {
  // Register the remove action button.
  context.registerAction('mod-icons', 500, 'savegame', {}, 'Patcher - Remove', () => {
    const store = context.api.store;
    const state = store.getState();
    const gameMode = selectors.activeGameId(state);
    if ((gameMode !== gameId) || !canPatch(context)) {
      return false;
    }

    const discoveryPath = getDiscoveryPath(state, gameId);
    const moddingPath = path.join(discoveryPath, modsPath);
    const absDataPath = path.join(discoveryPath, dataPath);
    runPatcher(extensionPath, absDataPath, entryPoint, true, moddingPath)
      .catch(err => onErrorCB(err));
    return true;
  }, () => {
    const state = context.api.store.getState();
    const gameMode = selectors.activeGameId(state);
    return (gameMode === gameId)
  });

  // Register the add action button.
  context.registerAction('mod-icons', 500, 'savegame', {}, 'Patcher - Add', () => {
    const store = context.api.store;
    const state = store.getState();
    const gameMode = selectors.activeGameId(state);
    if ((gameMode !== gameId) || !canPatch(context)) {
      return false;
    }

    const discoveryPath = getDiscoveryPath(state, gameId);
    const moddingPath = path.join(discoveryPath, modsPath);
    const absDataPath = path.join(discoveryPath, dataPath);
    runPatcher(extensionPath, absDataPath, entryPoint, false, moddingPath)
      .catch(err => onErrorCB(err));
    return true;
  }, () => {
    const state = context.api.store.getState();
    const gameMode = selectors.activeGameId(state);
    return (gameMode === gameId)
  });

  context.registerMainPage('sort-none', 'Load Order', LoadOrder, {
    id: 'harmony-load-order',
    hotkey: 'E',
    group: 'per-game',
    visible: () => selectors.activeGameId(context.api.store.getState()) === gameId,
    props: () => ({
      t: context.api.translate,
      extensionPath,
      moddingPath: path.join(getDiscoveryPath(context.api.store.getState(), gameId), modsPath),
    }),
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
      err = lastError.errors.map(errInstance => createErrorMessage(errInstance)).join('\n');
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

// Check list:
//  1. Ensure Vortex is not running any tools.
function canPatch(context) {
  let patch = true;
  const store = context.api.store;
  const state = store.getState();
  const running = util.getSafe(state, ['session', 'base', 'toolsRunning'], {});
  if (Object.keys(running).length > 0) {
    context.api.sendNotification({
      type: 'info',
      message: 'Can\'t run harmony patcher while a tool/game is running',
      displayMS: 5000,
    });
    patch = false;
  }

  return patch;
}

function getDiscoveryPath(state, gameId) {
  const discovery = util.getSafe(state, ['settings', 'gameMode', 'discovered', gameId], undefined);
  if ((discovery === undefined) || (discovery.path === undefined)) {
    log('error', 'Game is not discovered', gameId);
    return undefined;
  }

  return discovery.path;
}

function parseErrorData(data) {
  const errorData = (Array.isArray(data))
    ? `{ "errors": ${data.toString()} }` : data;
  try {
    const error = JSON.parse(errorData);
    return error;
  } catch (err) {
    log('error', 'Failed to parse injector response message', err);
    return undefined;
  }
}

function modIsEnabled(props, mod) {
  return (!!props.modState[mod])
    ? props.modState[mod].enabled
    : false;
}

function LoadOrderBase(props) {
  const loValue = (input) => {
    const idx = props.order.indexOf(input);
    return idx !== -1 ? idx : props.order.length;
  }

  const filtered = Object.keys(props.mods).filter(mod => modIsEnabled(props, mod));
  const sorted = filtered.sort((lhs, rhs) => loValue(lhs) - loValue(rhs));

  class ItemRenderer extends React.Component {
    render() {
      const item = this.props.item;
      return !modIsEnabled(props, item)
        ? null
        : React.createElement(BS.ListGroupItem, {
            style: {
              backgroundColor: 'var(--brand-bg, black)',
              borderBottom: '2px solid var(--border-color, white)'
            },
          },
          React.createElement('div', {
            style: {
              fontSize: '1.1em',
            },
          },
          React.createElement('img', {
            src: props.mods[item].attributes.pictureUrl
                  ? props.mods[item].attributes.pictureUrl
                  : `${props.extensionPath}/gameart.jpg`,
            className: 'mod-picture',
            width:'75px',
            height:'45px',
            style: {
              margin: '5px 10px 5px 5px',
              border: '1px solid var(--brand-secondary,#D78F46)',
            },
          }),
          util.renderModName(props.mods[item])));
    }
  }

  return React.createElement(MainPage, {},
    React.createElement(MainPage.Body, {},
      React.createElement(BS.Panel, { id: 'harmony-loadorder-panel' },
        React.createElement(BS.Panel.Body, {},
          React.createElement(FlexLayout, { type: 'row' },
            React.createElement(FlexLayout.Flex, {},
              React.createElement(DraggableList, {
                id: 'harmony-loadorder',
                itemTypeId: 'harmony-loadorder-item',
                items: sorted,
                itemRenderer: ItemRenderer,
                style: {
                  height: '100%',
                  overflow: 'auto',
                  borderWidth: 'var(--border-width, 1px)',
                  borderStyle: 'solid',
                  borderColor: 'var(--border-color, white)',
                },
                apply: ordered => {
                  props.onSetDeploymentNecessary(props.profile.gameId, true);
                  return props.onSetOrder(props.profile.id, ordered)
                },
              })
            ),
            React.createElement(FlexLayout.Flex, {},
              React.createElement('div', {
                style: {
                  padding: 'var(--half-gutter, 15px)',
                }
              },
                React.createElement('h2', {},
                  props.t('Changing your load order', { ns: props.I18N })),
                React.createElement('p', {},
                  props.t('Drag and drop the mods on the left to reorder them.'
                      + 'Mods placed at the bottom of the load order will have priority over those above them.', { ns: props.I18N })),
                  React.createElement('p', {},
                  props.t('Note: You can only manage mods installed with Vortex. Installing other mods manually may cause unexpected errors.', { ns: props.I18N })),
                  React.createElement('button', {
                    id: 'save',
                    className: 'btn btn-default',
                    onClick: () => saveLoadOrder(props),
                  },
                  props.t('Save changes', { ns: props.I18N }))
              ))
        )))));
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

function saveLoadOrder(props) {
  const destination = path.join(props.moddingPath, LOAD_ORDER_FILE);
  let assembliesInOrder = [];
  return Promise.each(props.order, entry => {
    const modFolder = path.join(props.stagingFolder, props.mods[entry].installationPath);
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
    .catch(err => props.onShowError('Failed to save load order', err, false));
}

function mapStateToProps(state) {
  const profile = selectors.activeProfile(state) || {};
  const profileId = !!profile ? profile.id : '';
  const gameId = !!profile ? profile.gameId : '';
  const stagingFolder = !!gameId ? selectors.installPathForGame(state, gameId) : '';
  return {
    stagingFolder,
    profile,
    modState: util.getSafe(profile, ['modState'], {}),
    mods: util.getSafe(state, ['persistent', 'mods', gameId], []),
    order: util.getSafe(state, ['persistent', 'loadOrder', profileId], []),
    I18N: `game-${gameId}`,
  };
}

function mapDispatchToProps(dispatch) {
  return {
    onSetDeploymentNecessary: (gameId, necessary) => dispatch(actions.setDeploymentNecessary(gameId, necessary)),
    onSetOrder: (profileId, ordered) => dispatch(actions.setLoadOrder(profileId, ordered)),
    onShowError: (message, details, allowReport) => util.showError(dispatch, message, details, { allowReport }),
  };
}

const LoadOrder = connect(mapStateToProps, mapDispatchToProps)(LoadOrderBase);

module.exports = {
  runPatcher,
  initHarmonyUI,
};
