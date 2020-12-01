const cp = require('child_process');
const path = require('path');

async function createIPC(usePipe, id) {
  // it does actually get named .exe on linux as well
  const exeName = 'VortexInjectorIPC.exe';

  return new Promise((resolve, reject) => {
    const args = [id];
    if (usePipe) {
      args.push('--pipe');
    }

    const proc = cp.spawn(path.join(path.resolve(__dirname, '..'), 'dist', exeName), args);
    proc.on('error', err => {
      reject(err);
    });
    proc.on('exit', (code, signal) => {
      if (code === 0x80131700) {
        reject(new Error('No compatible .Net Framework, you need .Net framework 4.6 or newer'));
      } else if (code !== null) {
        reject(new Error(`Failed to run Vortex Injector. Errorcode ${code.toString(16)}`));
      } else {
        reject(new Error(`The Vortex Injector was terminated. Signal: ${signal}`));
      }
    });
    resolve(proc);
  });
}

module.exports = {
  createIPC,
};