const fs=require('fs'),path=require('path');
const root=path.resolve(__dirname,'..'),pkg=path.join(root,'package');
const road='<path d="M15 6H49L58 58H6Z" fill="white" opacity=".28"/>';
const svg=body=>'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">'+road+body+'</svg>';
const write=(name,body)=>fs.writeFileSync(path.join(pkg,name+'.svg'),svg(body));
// Shared stems and symmetric arrowheads keep compound directions legible at 22px.
const stroke=d=>`<path d="${d}" fill="none" stroke="white" stroke-width="7" stroke-linecap="butt" stroke-linejoin="round"/>`;
const head=d=>`<path d="${d}" fill="white"/>`;
function arrows(directions){
 let body=stroke('M32 56V32');
 if(directions.includes('S'))body+=stroke('M32 34V15')+head('M32 5 21 18H43Z');
 if(directions.includes('L'))body+=stroke('M32 34V32H15')+head('M5 32 18 21V43Z');
 if(directions.includes('R'))body+=stroke('M32 34V32H49')+head('M59 32 46 21V43Z');
 return body;
}
for(const [name,directions] of Object.entries({Straight:'S',Left:'L',Right:'R',StraightLeft:'SL',StraightRight:'SR',LeftRight:'LR',StraightLeftRight:'SLR'}))write('HintArrow'+name,arrows(directions));
const diagonal=stroke('M47 54 18 18')+head('M9 9 30 12 12 30Z');
write('HintArrowDiagonalLeft',diagonal);
write('HintArrowDiagonalRight','<g transform="translate(64 0) scale(-1 1)">'+diagonal+'</g>');
const uturn=stroke('M44 56V23C44 7 18 7 18 23V35')+head('M18 47 7 33H29Z');
write('HintArrowUTurn',uturn);
const leftUTurn=stroke('M45 56V22C45 6 20 6 20 22V28')+head('M20 40 10 27H30Z')+stroke('M45 46H16')+head('M5 46 18 36V56Z');
write('HintArrowLeftUTurn',leftUTurn);
fs.writeFileSync(path.join(root,'ui/src/manual-arrow.svg'),svg(arrows('S')));
fs.writeFileSync(path.join(root,'ui/src/manual-uturn.svg'),svg(uturn));
write('ManualArrow',arrows('S'));write('ManualUTurn',uturn);
for(const name of ['HintYellow','HintDouble','HintZigzag','BrushYellow']){
 const p=path.join(pkg,name+'.svg');fs.writeFileSync(p,fs.readFileSync(p,'utf8').replaceAll('fill="white" opacity="1"','fill="#bf962f" opacity="1"').replaceAll('stroke="white"','stroke="#bf962f"'));
}
for(let i=0;i<7;i++){
 const color=i>=3?'#43a6ff':'white';let body='';
 if(i===0)body='<path d="M29 9H35V55H29Z" fill="white"/><path d="M48 3 52 14 63 18 52 22 48 33 44 22 33 18 44 14Z" fill="#70cbff"/>';
 else if(i===3||i===4)body=`<path d="${i===3?'M29 7H35V57H29Z':'M29 7H35V19H29ZM29 26H35V38H29ZM29 45H35V57H29Z'}" fill="${color}"/>`;
 else body=`<g${i===2||i===6?' transform="translate(64 0) scale(-1 1)"':''}><path d="M25 7H31V57H25Z M33 12 47 20 44 25 30 17Z M33 29 47 37 44 42 30 34Z M33 46 47 54 44 59 30 51Z" fill="${color}"/></g>`;
 write('HintDivider'+i,body);
}
// Width choices must show the actual single/double-row footprint.
for(const size of [2,3,4,13]){
 const rows=size===13?[26]:[17,35],height=size===13?12:size===2?8:size===3?11:14;
 write('HintCrosswalk'+size,rows.map(y=>[10,20,30,40,50].map(x=>`<rect x="${x}" y="${y}" width="6" height="${height}" fill="white"/>`).join('')).join(''));
}
console.log('Updated road style tooltip icons');

