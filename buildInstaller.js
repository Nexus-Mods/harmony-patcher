var msbuildLib = require('msbuild');
var path = require('path');

function build() {
  var msbuild = new msbuildLib();
  msbuild.sourcePath = path.join(__dirname, 'VortexHarmonyInstaller', 'VortexHarmonyInstaller.csproj');

  msbuild.configuration = process.argv[2] || 'Release';
  msbuild.configuration += ';TargetFrameworkVersion=v3.5';
  //msbuild.overrideParams.push('/m'); // parallel build
  msbuild.overrideParams.push('/clp:ErrorsOnly');

  msbuild.build();
}


function restore(cb) {
  var msbuild = new msbuildLib(cb);
  msbuild.sourcePath = path.join(__dirname, 'VortexHarmonyInstaller');
  msbuild.configuration = process.argv[2] || 'Release';
  msbuild.configuration += ';TargetFrameworkVersion=v3.5';
  msbuild.overrideParams.push('/t:restore');
  msbuild.build();
}

restore(() => build());

