import React from 'react';
import {bindValue,trigger,useValue} from 'cs2/api';
import {environment,restorePlacement,inside} from './menu-placement';
const placement$=bindValue<string|null>('RoadStyleIntoKorea','menuPlacement',null);
const open$=bindValue<boolean>('RoadStyleIntoKorea','open',false);
export function useMenuPlacement(){
 const [layout,setLayout]=React.useState(0),[position,setPosition]=React.useState<{x:number,y:number}|null>(null);
 const saved=useValue(placement$),isOpen=useValue(open$);
 const shell=React.useRef<HTMLDivElement>(null),drag=React.useRef<{x:number,y:number,left:number,top:number}|null>(null);
 const hydrated=React.useRef(false),dirty=React.useRef(false),env=React.useRef(''),lastSaved=React.useRef('');
 const [commit,setCommit]=React.useState(0);
 const reset=()=>{drag.current=null;setLayout(0);setPosition(null);dirty.current=true;setCommit(v=>v+1);};
 React.useEffect(()=>{
  if(hydrated.current||saved==null)return;
  env.current=environment();lastSaved.current=saved;
  const restored=restorePlacement(saved,env.current);setLayout(restored.layout);setPosition(restored.position);
  hydrated.current=true;dirty.current=restored.reset;
 },[saved]);
 React.useEffect(()=>{
  let frame=0;let point:{x:number,y:number}|null=null;
  const apply=()=>{frame=0;if(!drag.current||!shell.current||!point)return;const r=shell.current.getBoundingClientRect();dirty.current=true;setPosition({x:Math.max(0,Math.min(window.innerWidth-r.width,drag.current.left+point.x-drag.current.x)),y:Math.max(0,Math.min(window.innerHeight-r.height,drag.current.top+point.y-drag.current.y))});};
  const move=(e:MouseEvent)=>{if(!drag.current)return;point={x:e.clientX,y:e.clientY};if(!frame)frame=window.requestAnimationFrame(apply);};
  const stop=()=>{if(!drag.current)return;if(frame){window.cancelAnimationFrame(frame);apply();}drag.current=null;setCommit(v=>v+1);};
  const resize=()=>{const next=environment();if(hydrated.current&&next!==env.current){env.current=next;reset();}};
  window.addEventListener('mousemove',move);window.addEventListener('mouseup',stop);window.addEventListener('blur',stop);window.addEventListener('resize',resize);
  return()=>{if(frame)window.cancelAnimationFrame(frame);window.removeEventListener('mousemove',move);window.removeEventListener('mouseup',stop);window.removeEventListener('blur',stop);window.removeEventListener('resize',resize);};
 },[]);
 React.useEffect(()=>{
  if(!hydrated.current)return;
  const next=environment();if(next!==env.current){env.current=next;reset();return;}
  if(isOpen&&position&&shell.current&&!inside(shell.current.getBoundingClientRect(),window.innerWidth,window.innerHeight)){reset();return;}
  if(!dirty.current||drag.current)return;
  const value=JSON.stringify({version:1,environment:env.current,layout,position});
  if(value!==lastSaved.current){trigger('RoadStyleIntoKorea','saveMenuPlacement',value);lastSaved.current=value;}dirty.current=false;
 },[layout,position,commit,isOpen,saved]);
 const changeLayout=(value:number)=>{dirty.current=true;setLayout(value);};
 return {layout,position,shell,drag,setLayout:changeLayout};
}
