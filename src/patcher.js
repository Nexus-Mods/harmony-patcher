const { spawn } = require('child_process');
const path = require('path');
const { app, remote } = require('electron');
const { fs, log, util } = require('vortex-api');

const uniApp = app || remote.app;

const LOG_FILE_PATH = path.join(uniApp.getPath('userData'), 'harmonypatcher.log');
const PATCHER_EXEC = 'VortexHarmoyExec.exe';
const MODULE_PATH = path.join(util.getVortexPath('modules_unpacked'), 'harmony-patcher', 'dist');
const EXEC_PATH = path.join(MODULE_PATH, PATCHER_EXEC);

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
function runPatcher(extensionPath, dataPath, entryPoint, remove, modsPath) {
  let lastError;
  const wstream = fs.createWriteStream(LOG_FILE_PATH);
  //const wstream = fs.createWriteStream('log.log');
  // -m "D:\Games\UntitledGooseGame\Untitled_Data\Managed" -e "GameManager::Awake" -r
  return new Promise((resolve, reject) => {
    let patcher;
    modsPath = !!modsPath ? modsPath : path.join(dataPath, 'VortexMods');
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
      lastError = parseErrorData(data.toString());
      const formatted = data.toString().split('\n');
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
      const errorKeys = Object.keys(PATCHER_ERRORS);
      const createError = () => {
        if (lastError !== undefined) {
          const errorMessage = (!!lastError.RaisedException)
            ? lastError.Message + '\n'
              + lastError.RaisedException.ClassName
              + ':' + lastError.RaisedException.Message
            : lastError.Message;
          return new Error(errorMessage);
        } else {
          return (errorKeys.find(key => code.toString() === key) !== undefined)
            ? new Error(PATCHER_ERRORS[code.toString()])
            : new Error(PATCHER_ERRORS['-13']);
        }
      }
      return (code !== 0)
        ? reject(createError())
        : resolve();
    });
  });
}

function parseErrorData(data) {
  try {
    const error = JSON.parse(data);
    return error;
  } catch (err) {
    log('error', 'Failed to parse injector response message', err);
    return undefined;
  }
}

exports.default = runPatcher;
