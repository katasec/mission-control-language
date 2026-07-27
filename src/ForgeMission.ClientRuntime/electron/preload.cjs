const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('forgeDesktop', {
  pickFolder: () => ipcRenderer.invoke('forge-client-runtime:pick-folder'),
});
