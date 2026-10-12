const assert=require('assert'),fs=require('fs'),path=require('path'),vm=require('vm');
const ts=require('../ui/node_modules/typescript');
const source=fs.readFileSync(path.join(__dirname,'src/menu-placement.ts'),'utf8');
const exportsObject={};
vm.runInNewContext(ts.transpileModule(source,{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2019}}).outputText,{exports:exportsObject});
const {restorePlacement,inside}=exportsObject;
for(let layout=0;layout<4;layout++){
 const result=restorePlacement(JSON.stringify({version:1,environment:'display',layout,position:{x:125,y:60}}),'display');
 assert.equal(result.layout,layout);assert.equal(result.position.x,125);assert.equal(result.position.y,60);assert.equal(result.reset,false);
}
for(const raw of ['bad JSON',JSON.stringify({version:1,environment:'old display',layout:3,position:null}),JSON.stringify({version:1,environment:'display',layout:4,position:null}),JSON.stringify({version:1,environment:'display',layout:2,position:{x:-1,y:10}})]){
 const result=restorePlacement(raw,'display');assert.equal(result.layout,0);assert.equal(result.position,null);assert.equal(result.reset,true);
}
assert.equal(restorePlacement('','display').reset,false);
assert(inside({left:10,top:10,right:500,bottom:700},1920,1080));
assert(!inside({left:1800,top:10,right:2100,bottom:700},1920,1080));
assert(!inside({left:10,top:-20,right:500,bottom:700},1920,1080));
console.log('PASS: saved placement, four layouts, changed display, malformed data and screen bounds');
