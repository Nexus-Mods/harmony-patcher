import * as Promise from 'bluebird';
import { types } from 'vortex-api';

export declare function runPatcher(
  extensionPath: string,
  dataPath: string,
  entryPoint: string,
  remove: boolean,
  modsPath: string,
  context?: types.IExtensionContext,
  injectVIGO?: boolean,
  unityEngineDir?: string): Promise<void>;

export declare function addLoadOrderPage(
  context: types.IExtensionContext,
  gameId: string,
  loadOrderInfo: string,
  gameArtURL: string,
  preSort?: (items: any[]) => Promise<any[]>,
  filter?: (mods: types.IMod[]) => types.IMod[],
  callback?: (loadOrder: any) => void): void;

export declare function raiseConsentDialog(
  context: types.IExtensionContext,
  gameId: string,
): Promise<void>;
