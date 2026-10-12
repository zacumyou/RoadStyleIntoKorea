export function environment():string {
 const s=window.screen as Screen & {availLeft?:number,availTop?:number};
 return JSON.stringify([window.innerWidth,window.innerHeight,window.devicePixelRatio||1,s?.width,s?.height,s?.availWidth,s?.availHeight,s?.availLeft,s?.availTop,window.getComputedStyle(document.documentElement).fontSize]);
}
export function inside(r:{left:number,top:number,right:number,bottom:number},width:number,height:number):boolean {
 return r.left>=0&&r.top>=0&&r.right<=width+1&&r.bottom<=height+1;
}
export function restorePlacement(raw:string,env:string):{layout:number,position:{x:number,y:number}|null,reset:boolean} {
 const fallback={layout:0,position:null,reset:raw.length>0};
 if(!raw)return fallback;
 try {
  const v=JSON.parse(raw),p=v.position;
  if(v.version!==1||v.environment!==env||!Number.isInteger(v.layout)||v.layout<0||v.layout>3)return fallback;
  if(p!==null&&(!p||!Number.isFinite(p.x)||!Number.isFinite(p.y)||p.x<0||p.y<0))return fallback;
  return {layout:v.layout,position:p,reset:false};
 }catch{return fallback;}
}
