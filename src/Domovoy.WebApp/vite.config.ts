import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Мини-приложение раздаётся с подпути GitHub Pages:
// https://vyacheslavbelg.github.io/Project-Pennsylvania-/
// Без base все ассеты будут запрошены от корня домена и отдадут 404.
export default defineConfig({
  base: '/Project-Pennsylvania-/',
  plugins: [react()],
  build: {
    outDir: 'dist',
    sourcemap: false,
  },
})
