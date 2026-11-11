import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // The dev server forwards any request starting with /api to the C# API.
    // The browser thinks everything comes from one origin, so CORS never comes up,
    // and the code can call fetch('/api/...') with no hard-coded host.
    proxy: {
      '/api': 'http://localhost:5025',
    },
  },
})
