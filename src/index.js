"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
const patcher_1 = require('./patcher');
const injectorIPC_1 = require('./injectorIPC');
exports.runPatcher = patcher_1.runPatcher;
exports.addLoadOrderPage = patcher_1.addLoadOrderPage;
exports.raiseConsentDialog = patcher_1.raiseConsentDialog;
exports.createIPC = injectorIPC_1.createIPC;
