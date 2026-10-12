const assert=require('assert'),path=require('path'),fs=require('fs');
const root=path.resolve(__dirname,'..'),deps=path.resolve(root,'ui/node_modules');
const React=require(path.join(deps,'react')),server=require(path.join(deps,'react-dom/server'));
let state={open:true,selected:'RSIKCenterTool',brush:4,brushNext:2},commands=[],components=[],layout=0,overrides={};
// Direct component inspection uses a tiny hook harness; this is not a browser/game test.
React.useState=initial=>[typeof initial==='number'?layout:initial,()=>{}];React.useRef=()=>({current:null});React.useEffect=()=>{};
const Button=p=>React.createElement('button',{'aria-label':p['aria-label'],className:p.className},p.children);
global.window={React,'cs2/api':{bindValue:(g,k)=>k,useValue:k=>state[k],trigger:(g,k,v)=>commands.push(v)},'cs2/ui':{Button,FloatingButton:Button,Tooltip:p=>p.children},'cs2/l10n':{useLocalization:()=>({translate:(key,fallback)=>Object.prototype.hasOwnProperty.call(overrides,key)?overrides[key]:key==='RSIK.Menu.Language'?'ko':fallback})}};
function walk(e,out=[]){if(!e||typeof e!=='object')return out;if(e.type===Button)out.push(e);React.Children.forEach(e.props?.children,c=>walk(c,out));return out;}
(async()=>{const mod=await import(require('url').pathToFileURL(path.join(root,'package/RoadStyleIntoKorea.mjs')).href);mod.default({append:(slot,c)=>{if(slot==='Game')components.push(c);}});const [Menu,Hint]=components;
for(layout=0;layout<4;layout++){const e=Menu(),buttons=walk(e);assert.equal(buttons.length,32);const tools=buttons.filter(b=>b.props.selected!==undefined);assert.equal(tools.length,29);assert.equal(tools.filter(b=>b.props.selected).length,1);commands=[];tools.forEach(b=>b.props.onSelect());assert.equal(new Set(commands).size,29);const html=server.renderToStaticMarkup(React.createElement(Menu));for(const label of ['차선','횡단보도·유도선','노면 표지','보도·도로 시설','관리'])assert(html.includes(label));assert(e.props.children[0].props.children[0].props.onMouseDown);}
state.menuQuery='점자';assert.equal(walk(Menu()).length,4);state.menuQuery='없는메뉴xyz';assert(server.renderToStaticMarkup(React.createElement(Menu)).includes('검색 결과 없음'));state.menuQuery='';
for(const mode of ['RedPaving','ColorGuide','DrawLine','CrossingDistance','Crosswalk','ManualArrow','SpeedHump']){state.selected='RSIK'+mode+'Tool';state.brush=0;state.brushNext=1;const html=server.renderToStaticMarkup(React.createElement(Hint));assert(!html.includes('undefined'));assert(!html.includes('도로 여건에 따라'));}
// A third-language override must reach labels, translated search and brush instructions.
overrides={'RSIK.Menu.Language':'en','RSIK.UI.Tool.Tactile':'Tastpflaster','RSIK.UI.Group.3':'Gehweg','RSIK.UI.Selected':'Auswahl','RSIK.UI.Apply':'Anwenden'};
state.selected='RSIKTactileTool';state.menuQuery='Tastpflaster';assert.equal(walk(Menu()).length,4);
let translated=server.renderToStaticMarkup(React.createElement(Menu));assert(translated.includes('Tastpflaster')&&translated.includes('Gehweg'));
translated=server.renderToStaticMarkup(React.createElement(Hint));assert(translated.includes('Auswahl')&&translated.includes('Anwenden'));
overrides={'RSIK.Menu.Language':'en'};state.menuQuery='';assert(server.renderToStaticMarkup(React.createElement(Menu)).includes('Sidewalk paving'));
state.open=false;assert.equal(Menu(),null);
console.log('PASS: four menu layouts, 29 unique tool actions, categories, search, draggable header and concise hints (mock UI; not game rendering)');
})().catch(e=>{console.error(e);process.exit(1)});
