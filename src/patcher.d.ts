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

export declare function initHarmonyUI(
  context: types.IExtensionContext,
  extensionPath: string,
  dataPath: string,
  entryPoint: string,
  modsPath: string,
  gameId: string,
  onErrorCB: () => void): void;

export declare function raiseConsentNotification(
  context: types.IExtensionContext,
  gameId: string,
  isMerged: boolean,
): Promise<void>;
