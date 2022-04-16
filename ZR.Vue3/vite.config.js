import { defineConfig, loadEnv } from 'vite'
import path from 'path'
import createVitePlugins from './vite/plugins'

// https://vitejs.dev/config/
export default defineConfig(({ mode, command }) => {
  const env = loadEnv(mode, process.cwd())
  return {
    plugins: createVitePlugins(env, command === 'build'),
    resolve: {
      // https://cn.vitejs.dev/config/#resolve-alias
      alias: {
        // 设置路径
        '~': path.resolve(__dirname, './'),
        // 设置别名
        '@': path.resolve(__dirname, './src')
      },
      // https://cn.vitejs.dev/config/#resolve-extensions
      extensions: ['.mjs', '.js', '.ts', '.jsx', '.tsx', '.json', '.vue'],
    },
    base: env.VITE_APP_ROUTER_PREFIX,
    // 打包配置
    build: {
      sourcemap: command === 'build' ? false : true,
      target: 'modules',
      outDir: 'dist', //指定输出目录
      assetsDir: 'assets', //指定静态资源存储目录
      minify: 'terser', //混淆器，terser构建后文件体积更小
    },
    // vite 相关配置
    server: {
      port: 8887,
      host: true,
      open: true,
      proxy: {
        // https://cn.vitejs.dev/config/#server-proxy
        '/dev-api': {
          target: 'http://localhost:8888',
          changeOrigin: true,
          rewrite: (path) => path.replace(/^\/dev-api/, '')
        }
      },
    },
  }
})