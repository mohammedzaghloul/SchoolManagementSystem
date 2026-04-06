const fs = require('fs');
const path = require('path');
const { spawn } = require('child_process');

const distDir = path.join(__dirname, 'dist', 'school-management');
const forceDevMode = process.argv.includes('--dev');
const shouldServeProductionBuild =
  !forceDevMode &&
  (
    Boolean(process.env.RAILWAY_ENVIRONMENT)
    || process.env.NODE_ENV === 'production'
    || (Boolean(process.env.PORT) && fs.existsSync(distDir))
  );

if (shouldServeProductionBuild) {
  require('./server');
  return;
}

const ngCli = require.resolve('@angular/cli/bin/ng.js');
const child = spawn(
  process.execPath,
  [ngCli, 'serve', '--host', '0.0.0.0', '--port', process.env.DEV_PORT || '4200'],
  {
    cwd: __dirname,
    stdio: 'inherit'
  }
);

child.on('exit', (code) => {
  process.exit(code ?? 0);
});
