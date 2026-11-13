import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// When started from the Aspire AppHost, the API address is injected through service discovery variables.
const apiTarget =
  process.env.API_HTTPS ??
  process.env.API_HTTP ??
  process.env.services__api__https__0 ??
  process.env.services__api__http__0 ??
  'http://localhost:5180'

export default defineConfig({
  plugins: [react()],
  server: {
    port: process.env.PORT ? Number(process.env.PORT) : 5173,
    proxy: {
      '/api': { target: apiTarget, changeOrigin: true, secure: false },
    },
  },
  build: {
    // The API serves the built client from wwwroot, so the client and API share one origin.
    outDir: '../Api/wwwroot',
    emptyOutDir: true,
    chunkSizeWarningLimit: 1500,
  },
})
