var msbuildLib = require('msbuild');
var path = require('path');

async function build() {
  return new Promise((resolve, reject) => {
    var msbuild = new msbuildLib();
    msbuild.sourcePath = path.join(__dirname, 'VortexHarmonyIPC');
    msbuild.version = undefined;
    msbuild.configuration = process.argv[2] || 'Release';
    // msbuild.configuration += ';TargetFrameworkVersion=v4.5.2';
    //msbuild.overrideParams.push('/m'); // parallel build
    msbuild.overrideParams.push('/clp:ErrorsOnly');
    msbuild.on('error', (err, results) => {
      console.error('build failed', err, results);
      reject(err);
    });
    msbuild.on('done', (err, results) => {
      console.log('build done', err, results);
      resolve();
    });

    msbuild.build();
  });
}


async function restore() {
  return new Promise((resolve, reject) => {
    var msbuild = new msbuildLib();
    msbuild.version = undefined;
    msbuild.sourcePath = path.join(__dirname, 'VortexHarmonyIPC');
    msbuild.configuration = process.argv[2] || 'Release';
    // msbuild.configuration += ';TargetFrameworkVersion=v4.5.2';
    msbuild.overrideParams.push('/t:restore');
    msbuild.on('error', (err, results) => {
      console.error('restore failed', err, results);
      reject(new Error('restore failed: ' + err));
    });
    msbuild.on('done', (err, results) => {
      console.log('restore done', err, results);
      resolve();
    });
    msbuild.build();
  });
}

async function main() {
  try {
    await restore();
    await build();
  } catch (err) {
    console.error('failed', err);
    process.exit(1);
  }
}

main();

