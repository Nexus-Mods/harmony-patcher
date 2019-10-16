import * as Promise from 'bluebird';
declare function runPatcher(dataPath: string, entryPoint: string, remove: boolean): Promise<void>;
export default runPatcher;
