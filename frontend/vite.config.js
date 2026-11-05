import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// En desarrollo (npm run dev) el navegador llama a /api y Vite reenvía:
//   /api/ia/*  -> AIService (5100)     /api/*  -> backend (5000)
// En Docker, nginx hace el mismo reenvío (ver nginx.conf).
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api/ia': { target: process.env.VITE_AI_TARGET || 'http://localhost:5100', changeOrigin: true },
      '/api': { target: process.env.VITE_API_TARGET || 'http://localhost:5000', changeOrigin: true },
    },
  },
})
