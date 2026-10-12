const path=require('path');
const deps=path.resolve(__dirname,'node_modules');
const MiniCssExtractPlugin=require(path.join(deps,'mini-css-extract-plugin'));
module.exports={mode:'production',entry:{RoadStyleIntoKorea:'./src/index.tsx'},context:__dirname,
externalsType:'window',externals:{react:'React','cs2/modding':'cs2/modding','cs2/api':'cs2/api','cs2/ui':'cs2/ui','cs2/l10n':'cs2/l10n'},
resolve:{extensions:['.tsx','.ts','.js'],modules:[deps]},resolveLoader:{modules:[deps]},
module:{rules:[{test:/\.tsx?$/,use:{loader:'ts-loader',options:{compiler: path.join(deps,'typescript')}},exclude:/node_modules/},
{test:/\.css$/,use:[MiniCssExtractPlugin.loader,{loader:'css-loader',options:{modules:true}}]},
{test:/\.svg$/,type:'asset/inline'}]},
output:{path:path.resolve(__dirname,'../package'),filename:'[name].mjs',library:{type:'module'},publicPath:'coui://ui-mods/'},
experiments:{outputModule:true},plugins:[new MiniCssExtractPlugin({filename:'[name].css'})]};
