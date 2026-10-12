const fs=require('fs'),path=require('path'),root=path.resolve(__dirname,'..');
for(let i=0;i<16;i++){
 const color=i>=11&&i<=14?'#54bfee':(i>=1&&i<=3||i>=5&&i<=8)?'#f2cb36':'#fff';
 const double=[2,6,8,9,10,13,14].includes(i),dash=[4,5,6,12,15].includes(i),mixed=[9,10,13,14].includes(i);
 const side=(x,dotted)=>`<path d="M${x} 56V8" fill="none" stroke="${color}" stroke-width="4" ${dotted?'stroke-dasharray="7 5"':''}/>`;
 const body=i===3?`<path d="M36 56 26 48 36 40 26 32 36 24 26 16 36 8" fill="none" stroke="${color}" stroke-width="4"/>`:double?side(26,dash||mixed&&[9,13].includes(i))+side(38,dash||mixed&&[10,14].includes(i)):side(32,dash);
 fs.writeFileSync(path.join(root,'package',`DrawLine${i}.svg`),`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">${body}</svg>`);
}
const icon='<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"><path d="M12 52C48 52 16 12 52 12" fill="none" stroke="white" stroke-width="5"/><g fill="#51d5ff"><circle cx="12" cy="52" r="6"/><circle cx="52" cy="12" r="6"/></g><circle cx="32" cy="32" r="5" fill="#ffc552"/></svg>';
fs.writeFileSync(path.join(root,'package','DrawLine.svg'),icon);
fs.writeFileSync(path.join(root,'ui','src','draw-line.svg'),icon);

for(let i=0;i<8;i++){
 const color=i>=6?'#5ba2be':['#ed75af','#b8dd46','#45b67b'][i%3];
 const body=(i<3||i===6)?'<path d="M22 49 32 39 42 49M22 31 32 21 42 31M22 13 32 3 42 13" fill="none" stroke="'+color+'" stroke-width="8"/>':'<path d="M18 58C18 34 46 34 46 6" fill="none" stroke="'+color+'" stroke-width="9" stroke-dasharray="17 8"/>';
 const svg='<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">'+body+'</svg>';
 fs.writeFileSync(path.join(root,'package','ColorGuide'+i+'.svg'),svg);
 if(i===0){fs.writeFileSync(path.join(root,'package','ColorGuide.svg'),svg);fs.writeFileSync(path.join(root,'ui','src','color-guide.svg'),svg);}
}
