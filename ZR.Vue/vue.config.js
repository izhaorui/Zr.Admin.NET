'use strict'
const path = require('path')
const defaultSettings = require('./src/settings.js')
const BundleAnalyzerPlugin = require('webpack-bundle-analyzer').BundleAnalyzerPlugin

// 生产环境，测试和正式
const IS_PROD = ['production', 'prod'].includes(process.env.NODE_ENV)

function resolve(dir) {
  return path.join(__dirname, dir)
}

const name = defaultSettings.title // 标题

//官方vue.config.js 参考文档 https://cli.vuejs.org/zh/config/#css-loaderoptions
// 这里只列一部分，具体配置参考文档
module.exports = {
  css: {
    sourceMap: process.env.NODE_ENV == 'production' ? false : true, //开启css  source maps功能，前端可以看到css具体的行数
    loaderOptions: {
      sass: {
        sassOptions: { outputStyle: "expanded" }
      }
    }
  },
  // 部署生产环境和开发环境下的URL。
  // 默认情况下，Vue CLI 会假设你的应用是被部署在一个域名的根路径上
  // 例如 https://www.baidu.com/。如果应用被部署在一个子路径上，你就需要用这个选项指定这个子路径。例如，如果你的应用被部署在 https://www.baidu.com/admin/，则设置 baseUrl 为 /admin/。
  publicPath: process.env.VUE_APP_ROUTER_PREFIX,
  // 在npm run build 或 yarn build 时 ，生成文件的目录名称（要和baseUrl的生产环境路径一致）（默认dist）
  outputDir: 'dist',
  // 用于放置生成的静态资源 (js、css、img、fonts) 的；（项目打包之后，静态资源会放在这个文件夹下）
  assetsDir: 'static',
  // 是否开启eslint保存检测，有效值：ture | false | 'error'
  lintOnSave: process.env.NODE_ENV === 'development',
  // 如果你不需要生产环境的 source map，可以将其设置为 false 以加速生产环境构建。
  productionSourceMap: false,
  // webpack-dev-server 相关配置
  devServer: {
    host: '0.0.0.0', //项目启动host
    port: 8887, // 项目启动port,
    open: true,
    proxy: {
      // detail: https://cli.vuejs.org/config/#devserver-proxy
      [process.env.VUE_APP_BASE_API]: {
        target: 'http://localhost:8888/', // 后端接口地址
        changeOrigin: true,
        pathRewrite: {
          ['^' + process.env.VUE_APP_BASE_API]: '' //需要rewrite的
        }
      },
			"msgHub": {
				target: 'http://localhost:8888/msgHub',
				ws: true,
				changeOrigin: true,
				pathRewrite: {
          ['^/msgHub']: '' //需要rewrite的
        }
			}
    },
    disableHostCheck: true
  },
  configureWebpack: {
    name: name,
    resolve: {
      //设置路径别名，@代表根目录， @代表 src/文件夹
      alias: {
        '@': resolve('src')
      }
    },
    plugins: []
  },
  chainWebpack(config) {
    config.plugins.delete('preload') // TODO: need test
    config.plugins.delete('prefetch') // TODO: need test

    // set svg-sprite-loader
    config.module
      .rule('svg')
      .exclude.add(resolve('src/assets/icons'))
      .end()
    config.module
      .rule('icons')
      .test(/\.svg$/)
      .include.add(resolve('src/assets/icons'))
      .end()
      .use('svg-sprite-loader')
      .loader('svg-sprite-loader')
      .options({
        symbolId: 'icon-[name]'
      })
      .end()

    /**
     * 打包分析
     */
    if (IS_PROD) {
      config.plugin('webpack-report').use(BundleAnalyzerPlugin, [
        {
          analyzerMode: 'static'
        }
      ])
    }
    config
      .when(process.env.NODE_ENV !== 'development',
        config => {
          config
            .plugin('ScriptExtHtmlWebpackPlugin')
            .after('html')
            .use('script-ext-html-webpack-plugin', [{
              // `runtime` must same as runtimeChunk name. default is `runtime`
              inline: /runtime\..*\.js$/
            }])
            .end()
          config
            .optimization.splitChunks({
              chunks: 'all',
              cacheGroups: {
                libs: {
                  name: 'chunk-libs',
                  test: /[\\/]node_modules[\\/]/,
                  priority: 10,
                  chunks: 'initial' // only package third parties that are initially dependent
                },
                elementUI: {
                  name: 'chunk-elementUI', // split elementUI into a single package
                  priority: 20, // the weight needs to be larger than libs and app or it will be packaged into libs or app
                  test: /[\\/]node_modules[\\/]_?element-ui(.*)/ // in order to adapt to cnpm
                },
                commons: {
                  name: 'chunk-commons',
                  test: resolve('src/components'), // can customize your rules
                  minChunks: 3, //  minimum common number
                  priority: 5,
                  reuseExistingChunk: true
                }
              }
            })
          config.optimization.runtimeChunk('single'),
          {
            from: path.resolve(__dirname, './public/robots.txt'), //防爬虫文件
            to: './' //到根目录下
          }
        }
      )
  }
}