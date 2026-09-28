const { spawn } = require('node:child_process');
const net = require('node:net');

const [codeExecutable, extensionPath, examplesPath, portText] = process.argv.slice(2);
const port = Number(portText);

if (!codeExecutable || !extensionPath || !examplesPath || !Number.isInteger(port) || port < 1 || port > 65535) {
  console.error('Expected: launchExtensionHost.js <VS Code executable> <extension path> <examples path> <port>');
  process.exit(1);
}

const probe = net.createServer();
probe.once('error', error => {
  console.error(`Cannot use extension debug port ${port}: ${error.message}`);
  process.exitCode = 1;
});
probe.listen(port, '127.0.0.1', () => {
  probe.close(() => {
    const child = spawn(codeExecutable, [
      '--new-window',
      '--disable-extensions',
      `--inspect-extensions=${port}`,
      `--extensionDevelopmentPath=${extensionPath}`,
      examplesPath
    ], { detached: true, stdio: 'ignore', windowsHide: false });

    child.once('error', error => {
      console.error(`Could not start VS Code: ${error.message}`);
      process.exitCode = 1;
    });
    child.once('spawn', () => {
      child.unref();
      console.log(`Started Extension Development Host on 127.0.0.1:${port}`);
    });
  });
});
