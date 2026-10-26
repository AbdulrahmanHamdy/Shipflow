import { spawn } from 'child_process';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Look for ShipFlow.API.csproj across likely relative locations
const potentialPaths = [
  path.resolve(__dirname, '../../Shipflow/ShipFlow/src/ShipFlow.API/ShipFlow.API.csproj'),
  path.resolve(__dirname, '../../src/ShipFlow.API/ShipFlow.API.csproj'),
  path.resolve(__dirname, '../src/ShipFlow.API/ShipFlow.API.csproj'),
  path.resolve(process.cwd(), '../Shipflow/ShipFlow/src/ShipFlow.API/ShipFlow.API.csproj'),
  path.resolve(process.cwd(), '../src/ShipFlow.API/ShipFlow.API.csproj'),
  path.resolve(process.cwd(), 'Shipflow/ShipFlow/src/ShipFlow.API/ShipFlow.API.csproj'),
];

const csprojPath = potentialPaths.find((p) => fs.existsSync(p));

if (!csprojPath) {
  console.error('[ShipFlow Backend] ERROR: Could not locate ShipFlow.API.csproj in expected paths:');
  potentialPaths.forEach((p) => console.error('  - ' + p));
  process.exit(1);
}

console.log(`[ShipFlow Backend] Launching ASP.NET Core API from: ${csprojPath}`);

const env = {
  ...process.env,
  ASPNETCORE_ENVIRONMENT: process.env.ASPNETCORE_ENVIRONMENT || 'Development',
  Jwt__SigningKey:
    process.env.Jwt__SigningKey ||
    'ShipFlow_Development_SigningKey_At_Least_32_Bytes_Long_Secret!',
  Seed__DevPassword: process.env.Seed__DevPassword || 'DevPass1234!',
  Cors__AllowedOrigins__0: process.env.Cors__AllowedOrigins__0 || 'http://localhost:3000',
};

const child = spawn(
  'dotnet',
  ['run', '--project', `"${csprojPath}"`, '--launch-profile', 'http'],
  {
    env,
    stdio: 'inherit',
    shell: true,
  }
);

child.on('error', (err) => {
  console.error('[ShipFlow Backend] Failed to start dotnet process:', err);
  process.exit(1);
});

child.on('exit', (code, signal) => {
  if (signal) {
    process.kill(process.pid, signal);
  } else {
    process.exit(code ?? 0);
  }
});

function cleanup() {
  if (child && !child.killed) {
    if (process.platform === 'win32' && child.pid) {
      try {
        spawn('taskkill', ['/pid', child.pid.toString(), '/T', '/F'], {
          stdio: 'ignore',
        });
      } catch {
        child.kill();
      }
    } else {
      child.kill('SIGTERM');
    }
  }
}

process.on('SIGINT', cleanup);
process.on('SIGTERM', cleanup);
process.on('exit', cleanup);
