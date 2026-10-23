
import { defineConfig } from 'vite';

const apiUrl = 'http://127.0.0.1:5057';

export default defineConfig({
  server: {
    proxy: {
      '/api': {
        target: apiUrl,
        changeOrigin: true,
      },
      '/hubs': {
        target: apiUrl,
        changeOrigin: true,
        ws: true,
      },
    },
  },
});
