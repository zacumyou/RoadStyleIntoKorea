import {localize,LocalText} from "./localization";
import React from "react";
import {useMenuPlacement} from "./use-menu-placement";
import {ModRegistrar} from "cs2/modding";
import {bindValue,trigger,useValue} from "cs2/api";
import {FloatingButton,Button,Tooltip} from "cs2/ui";
import {useLocalization} from "cs2/l10n";
import tactile from "./tactile.svg";import cyclePath from "./cycle-path.svg";
import layoutHorizontal from './layout-horizontal.svg';import layoutVertical from './layout-vertical.svg';import layoutIcons from './layout-icons.svg';import layoutLabels from './layout-labels.svg';
import menu from "./menu.svg";
import edge from "./edge.svg";
import center from "./center.svg";
import crossing from "./crossing.svg";
import paving from "./paving.svg";
import curb from "./curb.svg"; import gutter from "./gutter.svg";
import sideRoad from "./side-road.svg";import hump from "./speed-hump.svg";
import uturn from "./uturn.svg";
import redPaving from "./red-paving.svg";
import colorGuide from "./color-guide.svg";import guide from "./guide.svg";import drawLine from "./draw-line.svg";
import guideAuto from "./guide-auto.svg";
import approach from "./approach.svg";
import manualStop from "./manual-stop.svg";
import manualArrow from "./manual-arrow.svg";
import manualUTurn from "./manual-uturn.svg";
import manualDiamond from "./manual-diamond.svg";
import addAll from "./add-all.svg";
import removeAll from "./remove-all.svg";
import removePaint from "./remove-paint.svg"; import close from "./close.svg";
import styles from "./menu.module.css";
import {RoadSigns} from './road-signs';
import speedIcon from './manual-speed.svg';import textIcon from './manual-text.svg';
import mergeDivider from './merge-divider.svg';import busDivider from './bus-divider.svg';
import {BrushHint} from "./brush-hint";
const query$=bindValue<string>("RoadStyleIntoKorea","menuQuery","");
const open$=bindValue<boolean>("RoadStyleIntoKorea","open",false);
const selected$=bindValue<string>("RoadStyleIntoKorea","selected","");
const guideReady$=bindValue<boolean>("RoadStyleIntoKorea","guideReady",false);
const guideStep$=bindValue<number>("RoadStyleIntoKorea","guideStep",0);
const command=(s:string)=>trigger("RoadStyleIntoKorea","command",s);
const items=[['RedPaving','적색 포장','Red pavement',redPaving],['RoadEdge','가장자리','Edge lines',edge],['Center','중앙선','Center lines',center],['Crosswalk','횡단보도','Crosswalks',crossing],['Paving','보도블록','Sidewalk paving',paving],['Curb','경계석','Curb',curb],['Gutter','배수로','Gutter',gutter],['MergeDivider','합류 구분선','Merge divider',mergeDivider],['BusDivider','버스 구분선','Bus divider',busDivider],['UTurn','유턴 점선','U-turn zone',uturn],['Approach','데칼 자동 생성','Auto decals',approach],['ManualStop','정지선','Stop line',manualStop],['ManualArrow','화살표 표지','Arrow marking',manualArrow],['ManualUTurn','유턴 표지','U-turn marking',manualUTurn],['ManualDiamond','다이아몬드 표지','Diamond marking',manualDiamond],['ManualSpeed','속도 표지','Speed marking',speedIcon],['ManualText','글자 표지','Text marking',textIcon],['Guide','유도선 편집','Guide editor',guide],['GuideAuto','자동 그리기','Automatic guides',guideAuto],['AddAll','모두 추가','Add all',addAll],['RemoveAll','모두 제거','Remove all',removeAll],['RemovePaint','하나씩 표시 제거','Remove marking',removePaint],['SideRoad','샛길 만들기','Side road entrance',sideRoad],['SpeedHump','방지턱','Speed hump',hump],['Tactile','점자블록','Tactile paving',tactile],['CyclePath','보도 자전거도로','Sidewalk cycle strip',cyclePath],['CrossingDistance','횡단보도 거리','Crosswalk distance',crossing],['DrawLine','차선 그리기','Draw lane lines',drawLine],['ColorGuide','컬러 유도선','Color guidance',colorGuide]];
const categories=[["차선","Lane lines",["RoadEdge","Center","MergeDivider","BusDivider","UTurn","DrawLine"]],["횡단보도·유도선","Crossings & guides",["Crosswalk","CrossingDistance","Guide","GuideAuto","ColorGuide"]],["노면 표지","Road markings",["RedPaving","ManualStop","ManualArrow","ManualUTurn","ManualDiamond","ManualSpeed","ManualText","SpeedHump","Approach"]],["보도·도로 시설","Sidewalk & roadside",["Paving","Tactile","CyclePath","Curb","Gutter","SideRoad"]],["관리","Manage",["AddAll","RemovePaint","RemoveAll"]]];
function MenuButton(){const open=useValue(open$);return <Tooltip tooltip="Road Style Into Korea"><FloatingButton src={menu} selected={open} onSelect={()=>command('menu')}/></Tooltip>;}
function Menu(){
 const {layout,position,shell,drag,setLayout}=useMenuPlacement();
 const query=useValue(query$)||"";const matches=(item:string[])=>query.toLowerCase().replace(/\s/g,"").length===0||[...item.slice(0,3),translate('RSIK.UI.Tool.'+item[0],item[2])||item[2]].some(text=>text.toLowerCase().replace(/\s/g,"").includes(query.toLowerCase().replace(/\s/g,"")));const open=useValue(open$),selected=useValue(selected$),ready=useValue(guideReady$),step=useValue(guideStep$);const {translate}=useLocalization();const ko=translate('RSIK.Menu.Language','en')==='ko';const t=localize(translate,ko);if(!open)return null;
return <div ref={shell} className={styles.menuShell+' '+(layout%2?styles.horizontal:'')+' '+(layout>=2?styles.iconsOnly:'')} style={position?{left:position.x,top:position.y,right:'auto'}:undefined}><div className={styles.panel} data-rsik-menu="true" role="region" aria-label="Road Style Into Korea"><div className={styles.header} onMouseDown={e=>{if(e.button!==0||(e.target instanceof Element&&e.target.closest('button')))return;const r=shell.current?.getBoundingClientRect();if(r){e.preventDefault();drag.current={x:e.clientX,y:e.clientY,left:r.left,top:r.top};}}}><span className={styles.title}>ROAD STYLE INTO KOREA</span><div className={styles.headerActions}><Tooltip tooltip={t("RSIK.UI.VerticalHorizontal")}><Button className={styles.layoutButton} onSelect={()=>setLayout(layout^1)} aria-label={t("RSIK.UI.VerticalHorizontal")}><img src={layout%2?layoutHorizontal:layoutVertical} alt=""/></Button></Tooltip><Tooltip tooltip={t("RSIK.UI.IconsOnlyShowLabels")}><Button className={styles.layoutButton} onSelect={()=>setLayout(layout^2)} aria-label={t("RSIK.UI.IconsOnlyShowLabels")}><img src={layout>=2?layoutIcons:layoutLabels} alt=""/></Button></Tooltip><Button className={styles.close} onSelect={()=>command('menu')} aria-label={t("RSIK.UI.Close")}><img src={close} alt=""/></Button></div></div>
<div className={styles.menuSearch}><input value={query} placeholder={t("RSIK.UI.SearchTools")} aria-label={t("RSIK.UI.SearchTools")} onChange={e=>trigger('RoadStyleIntoKorea','menuSearch',e.target.value)} onFocus={()=>trigger('RoadStyleIntoKorea','menuSearchFocus',true)} onBlur={()=>trigger('RoadStyleIntoKorea','menuSearchFocus',false)} onKeyDown={e=>{e.stopPropagation();if(e.key==='Escape'){trigger('RoadStyleIntoKorea','menuSearch','');e.currentTarget.blur();}}}/></div><div className={styles.toolList}>{categories.map(([koreanTitle,englishTitle,keys],index)=>{const group=(keys as string[]).map(key=>items.find(item=>item[0]===key)!).filter(matches);return group.length>0?<div key={index} className={styles.tools+' '+styles.group}><div className={styles.groupTitle}>{t('RSIK.UI.Group.'+index)}</div><div className={styles.toolGrid}>{group.map(([key,korean,english,icon])=><div key={key} className={styles.toolCell}><Tooltip tooltip={t('RSIK.UI.Tool.'+key)}><Button className={styles.tool+' '+(selected==='RSIK'+key+'Tool'?styles.selected:'')} aria-label={t('RSIK.UI.Tool.'+key)} selected={selected==='RSIK'+key+'Tool'} onSelect={()=>command(key)}><img src={icon} alt=""/><span>{t('RSIK.UI.Tool.'+key)}</span></Button></Tooltip></div>)}</div></div>:null;})}{!items.some(matches)&&<div className={styles.hint}>{t("RSIK.UI.NoMatchingTools")}</div>}</div></div>{['RSIKManualSpeedTool','RSIKManualTextTool'].includes(selected)&&<RoadSigns key={selected} speed={selected==='RSIKManualSpeedTool'} ko={ko}/>}</div>;
}
const register:ModRegistrar=registry=>{registry.append('GameTopRight',MenuButton);registry.append('Game',Menu);registry.append('Game',BrushHint);};
export const hasCSS=true;
export default register;










