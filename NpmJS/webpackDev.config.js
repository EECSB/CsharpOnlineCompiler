const path = require("path");
const webpack = require("webpack");
const TerserPlugin = require("terser-webpack-plugin");
const MonacoWebpackPlugin = require('monaco-editor-webpack-plugin');

module.exports = {
    mode: "production", //mode: "development",
    entry: ["./src/MonacoInterop.js", "./src/GunInterop.js"],
    output: {
        path: path.resolve(__dirname, "../wwwroot/js/webpack-bundle"),
        filename: "index.bundle.js",
        //Short, clean names for lazily-loaded chunks. The default names embed the full
        //local path + "node_modules" (e.g. C_Online_Compiler_Post_..._node_modules_monaco-editor_...),
        //which (1) some servers/WAFs block because the URL contains "node_modules", and
        //(2) can exceed the Windows MAX_PATH limit during publish, silently dropping files.
        //Either way the C# language chunk 404s on the server and syntax highlighting breaks.
        chunkFilename: "[contenthash].chunk.js",
        //Remove stale chunks from previous builds so the folder never serves orphaned files.
        clean: true
    },
    module: {
        rules: [
            {
                test: /\.css$/,
                use: ['style-loader', 'css-loader']
            },
            {
                //Monaco's codicon font. This has to be an asset module rather than file-loader: under
                //webpack 5 file-loader hands back a JS module, which webpack then emits as an asset in its
                //own right — so the .ttf the CSS ends up pointing at contains that JS snippet instead of the
                //font, and the browser drops it ("downloadable font: rejected by sanitizer") leaving the
                //editor's icons as blank boxes. asset/resource emits the real binary and links to it.
                test: /\.ttf$/,
                type: 'asset/resource'
            }
        ]
    },
    plugins: [
        //The editor only ever uses C# (see Editor.razor -> InitializeMonaco(..., "csharp")).
        //Shipping only that language drops ~150 unused language definitions and the
        //css/html/json/ts web workers, shrinking the output dramatically.
        new MonacoWebpackPlugin({ languages: ['csharp'] }),
        //Fold every lazily-loaded chunk back into index.bundle.js so there are no separate
        //chunk files to 404 on the server. Monaco normally loads each language/feature as its
        //own async chunk; if even one fails to deploy (missing upload, blocked path, MAX_PATH
        //truncation) that feature silently breaks - which is why syntax highlighting died on the
        //server but worked locally. With a single bundle, if the editor loads at all, C#
        //highlighting works. (editor.worker.js stays a separate file - it is a Web Worker entry,
        //not an async chunk, and the editor degrades gracefully without it.)
        new webpack.optimize.LimitChunkCountPlugin({ maxChunks: 1 })
    ],
    optimization: {
        //minimize: false, //We don't want to minimize our code(while developing).
        minimize: true, //Minify the bundle for a smaller, faster download when deployed.
        minimizer: [
            new TerserPlugin({
                parallel: true,
                terserOptions: {
                    // https://github.com/webpack-contrib/terser-webpack-plugin#terseroptions
                }
            })
        ]
    }
}