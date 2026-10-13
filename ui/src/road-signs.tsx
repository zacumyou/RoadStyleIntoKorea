import {useLocalization} from "cs2/l10n";
import {localize,LocalText} from "./localization";
import React,{useState,useEffect,useRef} from 'react';
import {signPlacement} from './sign-placement';
import {bindValue,useValue,trigger} from 'cs2/api';
import {Button} from 'cs2/ui';
import autoIcon from './sign-auto.svg';
import searchIcon from './sign-search.svg';
import placeholderIcon from './sign-placeholder.svg';
import styles from './menu.module.css';
const brush$=bindValue<number>('RoadStyleIntoKorea','brush',0);
export const textSigns=[['버스전용','Bus only'],['어린이','Children'],['도시대교','City / bridge'],['입구','Entrance'],['출구','Exit'],['고속도로','Expressway'],['진입금지','No entry'],['일방통행','One way'],['보호구역','Protection zone'],['천천히','Slow'],['비보호','Unprotected turn']];
export function signName(speed:boolean,value:number,ko:boolean,t:LocalText){const n=speed?13:textSigns.length,index=value%n;return speed?(index===0?(t("RSIK.UI.RoadSpeed")):index*10+' km/h'):t('RSIK.UI.Sign.'+index);}
export function RoadSigns({speed,ko,anchor,placementKey}:{speed:boolean,ko:boolean,anchor:React.RefObject<HTMLDivElement>,placementKey:string}){
 const {translate}=useLocalization();const t=localize(translate,ko);
 const value=useValue(brush$),[query,setQuery]=useState('');const count=speed?13:textSigns.length,index=value%count,size=Math.floor(value/count);
 const choose=(v:number)=>trigger('RoadStyleIntoKorea',speed?'speedBrush':'textBrush',v);
 useEffect(()=>()=>{trigger('RoadStyleIntoKorea','signFocus',false);},[]);
 const panel=useRef<HTMLDivElement>(null),[floating,setFloating]=useState<ReturnType<typeof signPlacement>|null>(null);
 useEffect(()=>{
  let frame=0;
  const place=()=>{frame=0;if(!anchor.current||!panel.current)return;
   const a=anchor.current.getBoundingClientRect(),r=panel.current.getBoundingClientRect();
   const next=signPlacement(a,r.width,Math.min(panel.current.scrollHeight+2,window.innerHeight*.7),window.innerWidth,window.innerHeight);
   setFloating(old=>old&&old.left===next.left&&old.top===next.top&&old.maxHeight===next.maxHeight&&old.maxWidth===next.maxWidth?old:next);
  };
  const schedule=()=>{if(!frame)frame=window.requestAnimationFrame(place);};schedule();
  const observer=typeof ResizeObserver!=='undefined'?new ResizeObserver(schedule):null;
  if(anchor.current)observer?.observe(anchor.current);if(panel.current)observer?.observe(panel.current);
  window.addEventListener('resize',schedule);
  return()=>{if(frame)window.cancelAnimationFrame(frame);observer?.disconnect();window.removeEventListener('resize',schedule);};
 },[anchor,placementKey,speed,query]);
 const q=query.trim().toLowerCase();
 const results=Array.from({length:count},(_,i)=>i).filter(i=>speed||[...textSigns[i],signName(false,i,ko,t)].some(t=>t.toLowerCase().includes(q)));
 return <div ref={panel} className={styles.signPanel} style={floating?{...floating,position:'fixed',right:'auto',marginTop:0}:undefined} data-rsik-menu="true">
  <div className={styles.signTitle}>{(speed?t("RSIK.UI.SpeedMarking"):t("RSIK.UI.TextMarkingManualOnly"))}</div>
  <div className={styles.signSizes}>{[t("RSIK.UI.AutoWidth"),'3 m','4 m'].map((label,i)=><Button key={i} selected={size===i} className={size===i?styles.signSelected:''} onSelect={()=>choose(i*count+index)}>{i===0&&<img className={styles.signAutoIcon} src={autoIcon} alt=""/>}<span>{label}</span></Button>)}</div>
  {!speed&&<div className={styles.signSearchWrap}><input className={styles.signSearch} value={query} placeholder={t("RSIK.UI.SearchTextMarkings")} aria-label={t("RSIK.UI.SearchTextMarkings")} onChange={e=>setQuery(e.target.value)} onFocus={()=>trigger('RoadStyleIntoKorea','signFocus',true)} onBlur={()=>trigger('RoadStyleIntoKorea','signFocus',false)} onKeyDown={e=>{e.stopPropagation();if(e.key==='Escape')e.currentTarget.blur();}}/><img className={styles.signSearchIcon} src={searchIcon} alt=""/></div>}
  <div className={styles.signResults}>{results.map(i=><Button key={i} selected={index===i} className={`${styles.signChoice} ${index===i?styles.signSelected:''}`} onSelect={()=>choose(size*count+i)}><span className={styles.signThumb} aria-hidden="true"><img src={speed&&i===0?autoIcon:placeholderIcon} alt=""/></span><span className={styles.signChoiceLabel}>{signName(speed,i,ko,t)}</span></Button>)}</div>
  {results.length===0&&<div className={styles.hint}>{t("RSIK.UI.NoResults")}</div>}
 </div>;
}
