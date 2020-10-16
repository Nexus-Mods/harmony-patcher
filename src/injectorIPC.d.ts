import * as Promise from 'bluebird';

export declare function createIPC(usePipe, id): Promise<ChildProcessWithoutNullStreams>;