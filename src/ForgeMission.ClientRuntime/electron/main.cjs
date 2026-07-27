const { app, BrowserWindow, dialog, ipcMain } = require('electron');
const { spawn } = require('node:child_process');
const http = require('node:http');
const path = require('node:path');

let hostProcess;

function startHost() {
  const projectPath = path.join(__dirname, '..', 'ForgeMission.ClientRuntime.csproj');
  hostProcess = spawn('dotnet', ['run', '--project', projectPath, '--no-launch-profile'], {
    stdio: ['ignore', 'pipe', 'pipe'],
  });

  return waitForHostAddress(hostProcess);
}

function waitForHostAddress(host) {
  return new Promise((resolve, reject) => {
    let output = '';
    const timeout = setTimeout(() => reject(new Error('Forge Client Runtime did not report a ready address.')), 30000);

    host.stdout.setEncoding('utf8');
    host.stderr.setEncoding('utf8');
    host.stdout.on('data', (chunk) => {
      process.stdout.write(chunk);
      output += chunk;
      const match = output.match(/^FORGE_CLIENT_RUNTIME_URL=(.+)$/m);
      if (match) {
        clearTimeout(timeout);
        resolve(match[1].trim());
      }
    });
    host.stderr.on('data', (chunk) => process.stderr.write(chunk));
    host.on('error', (error) => {
      clearTimeout(timeout);
      reject(error);
    });
    host.on('exit', (code) => {
      clearTimeout(timeout);
      reject(new Error(`Forge Client Runtime exited before readiness (code ${code}).`));
    });
  });
}

function confirmReady(url) {
  return new Promise((resolve, reject) => {
    http.get(`${url}/ready`, (response) => {
      let body = '';
      response.setEncoding('utf8');
      response.on('data', (chunk) => body += chunk);
      response.on('end', () => {
        if (response.statusCode !== 200) {
          reject(new Error(`Forge Client Runtime readiness failed (${response.statusCode}).`));
          return;
        }

        const ready = JSON.parse(body);
        resolve(ready.url);
      });
    }).on('error', reject);
  });
}

function createWindow(url) {
  const window = new BrowserWindow({
    width: 1100,
    height: 760,
    webPreferences: {
      contextIsolation: true,
      nodeIntegration: false,
      preload: path.join(__dirname, 'preload.cjs'),
      sandbox: true,
    },
  });

  return window.loadURL(url);
}

ipcMain.handle('forge-client-runtime:pick-folder', async () => {
  const result = await dialog.showOpenDialog({
    properties: ['openDirectory', 'createDirectory'],
  });

  return result.canceled ? null : result.filePaths[0];
});

app.whenReady().then(async () => {
  const announcedUrl = await startHost();
  const readyUrl = await confirmReady(announcedUrl);
  console.log(`Forge Client Runtime ready at ${readyUrl}`);
  await createWindow(readyUrl);
}).catch((error) => {
  console.error(error);
  app.quit();
});

app.on('window-all-closed', () => app.quit());
app.on('before-quit', () => hostProcess?.kill());
