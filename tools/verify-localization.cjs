const assert=require('assert'),fs=require('fs'),path=require('path');const root=path.resolve(__dirname,'..');
const en=JSON.parse(fs.readFileSync(path.join(root,'lang/en-US.json'),'utf8')),ko=JSON.parse(fs.readFileSync(path.join(root,'lang/ko-KR.json'),'utf8'));
assert.deepEqual(Object.keys(en).sort(),Object.keys(ko).sort());
for(const locale of [en,ko])for(const [key,value]of Object.entries(locale))assert(typeof value==='string'&&value.length>0,key);
const catalog=JSON.parse(fs.readFileSync(path.join(root,'localization/ui.json'),'utf8'));for(const [key,v]of Object.entries(catalog)){assert.equal(en[key],v.en,key);assert.equal(ko[key],v.ko,key);}
for(const file of ['index.tsx','brush-hint.tsx','road-signs.tsx']){const source=fs.readFileSync(path.join(root,'ui/src',file),'utf8');for(const m of source.matchAll(/\bt\(["']([^"']+)["']\)/g))assert(en[m[1]],file+': '+m[1]);}
assert.equal(Object.keys(en).filter(k=>k.startsWith('RSIK.UI.Tool.')).length,29);assert.equal(Object.keys(en).filter(k=>k.startsWith('RSIK.UI.Sign.')).length,11);
assert(en['Options.OPTION[RoadStyleIntoKorea.RoadStyleIntoKorea.Mod.Settings.Crosswalks]']);
for(const [locale,data]of [['en-US',en],['ko-KR',ko]])fs.writeFileSync(path.join(root,'lang',locale+'.json'),JSON.stringify(data,null,2)+'\n');
console.log('PASS: '+Object.keys(en).length+' matching native/exported translation keys, UI references and settings IDs');
