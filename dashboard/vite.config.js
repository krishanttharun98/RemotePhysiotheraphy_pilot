import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { cloudSavePlugin } from './server/ugsCloudSave.js'

export default defineConfig(({ command, mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const plugins = [react(), tailwindcss()]
  if (command === 'serve') plugins.push(cloudSavePlugin(env))
  return {
    base: './',
    plugins,
    server: { port: 5173, open: true },
  }
})
