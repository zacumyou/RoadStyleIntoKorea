import {localize,LocalText} from "./localization";
import React,{useEffect,useRef} from "react";
import {bindValue,useValue,trigger} from "cs2/api";
import {Button} from "cs2/ui";
import {useLocalization} from "cs2/l10n";
import {signName} from './road-signs';
import styles from "./brush-hint.module.css";
const selected$=bindValue<string>('RoadStyleIntoKorea','selected','');
const brush$=bindValue<number>('RoadStyleIntoKorea','brush',2);
const next$=bindValue<number>('RoadStyleIntoKorea','brushNext',3);
const signHover$=bindValue<string>('RoadStyleIntoKorea','signHover','');
const step$=bindValue<number>('RoadStyleIntoKorea','guideStep',0);
const open$=bindValue<boolean>('RoadStyleIntoKorea','open',false);
function label(crossing:boolean,value:number,ko:boolean,t:LocalText){
 if(crossing)return `${value===13?(t("RSIK.UI.SingleRow")):(t("RSIK.UI.DoubleRow"))} ${value===13?3:value} m`;
 return ([t("RSIK.UI.Original"),t("RSIK.UI.NoMarkings"),t("RSIK.UI.WhiteSolid"),t("RSIK.UI.YellowSolid"),t("RSIK.UI.DoubleYellow"),t("RSIK.UI.YellowZigzag")])[value]||'';
}
function thumbnail(crossing:boolean,value:number){
 const name=crossing?`Crosswalk${value}`:(['Original','None','White','Yellow','Double','Zigzag'][value]||'None');
 return `coui://rsik/Hint${name}.svg`;
}
export function BrushHint({docked=false}:{docked?:boolean}={}){
 const open=useValue(open$);
 const signHover=useValue(signHover$),selected=useValue(selected$),value=useValue(brush$),next=useValue(next$);
 const {translate}=useLocalization();const ko=translate('RSIK.Menu.Language','en')==='ko';const t=localize(translate,ko);
 const mode=selected.replace('RSIK','').replace('Tool',''),step=useValue(step$);
 const active=['RedPaving','ColorGuide','DrawLine','CrossingDistance','Tactile','CyclePath','SpeedHump','MergeDivider','BusDivider','RemovePaint','Gutter','Curb','SideRoad','Crosswalk','RoadEdge','Center','Paving','UTurn','Guide','GuideAuto','Approach','ManualStop','ManualArrow','ManualUTurn','ManualDiamond','ManualSpeed','ManualText','AddAll','RemoveAll'].includes(mode),crossing=mode==='Crosswalk';
 const cycling=['RedPaving','ColorGuide','DrawLine','SpeedHump','SideRoad','MergeDivider','BusDivider','Gutter','Crosswalk','RoadEdge','Center','Paving','ManualArrow','ManualSpeed','ManualText'].includes(mode);
 const symbols=['Straight','Left','Right','StraightLeft','StraightRight','LeftRight','StraightLeftRight','DiagonalLeft','DiagonalRight','LeftUTurn'];
 const manualName:Record<string,string>={ManualStop:t("RSIK.UI.StopLine"),ManualUTurn:t("RSIK.UI.UTurnMarking"),ManualDiamond:t("RSIK.UI.DiamondMarking")};
 const lineNames=[t("RSIK.UI.WhiteSolid"),t("RSIK.UI.YellowSolid"),t("RSIK.UI.DoubleYellow"),t("RSIK.UI.YellowZigzag"),t("RSIK.UI.WhiteDotted"),t("RSIK.UI.YellowDotted"),t("RSIK.UI.DoubleYellowDotted"),t("RSIK.UI.CenterYellow"),t("RSIK.UI.CenterDoubleYellow"),t("RSIK.UI.WhiteRL"),t("RSIK.UI.WhiteLR"),t("RSIK.UI.BusBlueSolid"),t("RSIK.UI.BusBlueDashed"),t("RSIK.UI.BusBlueRL"),t("RSIK.UI.BusBlueLR"),t("RSIK.UI.UTurnDotted")];
 const colorNames=[t("RSIK.UI.PinkChevron"),t("RSIK.UI.LightGreenChevron"),t("RSIK.UI.GreenChevron"),t("RSIK.UI.PinkJunction"),t("RSIK.UI.LightGreenJunction"),t("RSIK.UI.GreenJunction"),t("RSIK.UI.BlueChevron"),t("RSIK.UI.BlueJunction")];
 const name=(v:number):string=>mode==='RedPaving'?(translate('RSIK.RedPaving.'+v,v===0?(t("RSIK.UI.Continuous")):(t("RSIK.UI.Stripes3mGap6m")))||''):mode==='ColorGuide'?colorNames[[0,1,2,6,3,4,5,7][v]]:mode==='DrawLine'?lineNames[v]:mode==='CrossingDistance'?`${t("RSIK.UI.CrosswalkTargetDistance")} ${v} m`:mode==='Tactile'?(t("RSIK.UI.TactilePaving030M")):mode==='CyclePath'?(t("RSIK.UI.SidewalkCycleStrip180M")):mode==='SpeedHump'?(([t("RSIK.UI.SpeedHumpClean"),t("RSIK.UI.SpeedHumpWorn")])[v]):['MergeDivider','BusDivider'].includes(mode)?([t("RSIK.UI.Automatic"),t("RSIK.UI.WhiteRL"),t("RSIK.UI.WhiteLR"),t("RSIK.UI.BlueSolid"),t("RSIK.UI.BlueDashed"),t("RSIK.UI.BlueRL"),t("RSIK.UI.BlueLR")])[v]:['ManualSpeed','ManualText'].includes(mode)?signName(mode==='ManualSpeed',v,ko,t):mode==='SideRoad'?`${t("RSIK.UI.SideRoadEntrance")} ${v} m`:mode==='RemovePaint'?(t("RSIK.UI.RemoveMarking")):mode==='Gutter'?((v===1?t("RSIK.UI.GutterAGrating"):t("RSIK.UI.GutterBSlots"))):mode==='Curb'?(t("RSIK.UI.Curb")):mode==='ManualArrow'?([t("RSIK.UI.Straight"),t("RSIK.UI.Left"),t("RSIK.UI.Right"),t("RSIK.UI.StraightLeft"),t("RSIK.UI.StraightRight"),t("RSIK.UI.LeftRight"),t("RSIK.UI.StraightLeftRight"),t("RSIK.UI.DiagonalLeft"),t("RSIK.UI.DiagonalRight"),t("RSIK.UI.LeftUTurn")])[v]:manualName[mode]?manualName[mode]:mode==='Center'?([t("RSIK.UI.Original"),t("RSIK.UI.None"),t("RSIK.UI.WhiteSolid"),t("RSIK.UI.YellowSolid"),t("RSIK.UI.DoubleYellow")])[v]:mode==='Paving'?`${t("RSIK.UI.Paving")} ${String(v).padStart(2,'0')}`:mode==='UTurn'?`${t("RSIK.UI.UTurnZone")} ${v} m`:mode==='AddAll'?(t("RSIK.UI.AddAll")):mode==='RemoveAll'?(t("RSIK.UI.RemoveAll")):mode==='Approach'?(t("RSIK.UI.AutoDecals")):mode==='Guide'?(t("RSIK.UI.GuideEditor")):mode==='GuideAuto'?(t("RSIK.UI.AutomaticLeftTurnGuides")):label(crossing,v,ko,t);
 const icon=(v:number):string=>mode==='RedPaving'?`coui://rsik/RedPaving${v}.svg`:mode==='ColorGuide'?`coui://rsik/ColorGuide${[0,1,2,6,3,4,5,7][v]}.svg`:mode==='DrawLine'?`coui://rsik/DrawLine${v}.svg`:mode==='CrossingDistance'?'coui://rsik/Crosswalk.svg':mode==='Tactile'?'coui://rsik/Tactile.svg':mode==='CyclePath'?'coui://rsik/CyclePath.svg':mode==='SpeedHump'?'coui://rsik/SpeedHump.svg':['MergeDivider','BusDivider'].includes(mode)?`coui://rsik/HintDivider${v}.svg`:['ManualSpeed','ManualText'].includes(mode)?`coui://rsik/${mode}.svg`:mode==='SideRoad'?'coui://rsik/SideRoad.svg':mode==='RemovePaint'?'coui://rsik/RemovePaint.svg':mode==='Gutter'?`coui://rsik/Gutter${v===1?'A':'B'}.png`:mode==='Curb'?'coui://rsik/HintCurb.svg':mode==='ManualArrow'?`coui://rsik/HintArrow${symbols[v]}.svg`:mode==='ManualUTurn'?'coui://rsik/HintArrowUTurn.svg':mode==='ManualDiamond'?'coui://rsik/HintDiamond.svg':mode==='ManualStop'?'coui://rsik/HintStop.svg':mode==='Center'?`coui://rsik/Hint${['Original','None','White','Yellow','Double'][v]}.svg`:mode==='Paving'?`coui://rsik/RSIKThumb_CSKRBrickPed0${v}W8m.png`:mode==='UTurn'?'coui://rsik/HintZone.svg':['Guide','GuideAuto','Approach','AddAll','RemoveAll'].includes(mode)?`coui://rsik/${mode}.svg`:thumbnail(crossing,v);
 const left=mode==='RedPaving'?translate('RSIK.RedPaving.Left',t("RSIK.UI.StartEnd")):mode==='ColorGuide'?(t("RSIK.UI.StartEnd")):mode==='DrawLine'?(t("RSIK.UI.ConnectDragMidpoint")):mode==='CrossingDistance'?(t("RSIK.UI.Increase1m")):['AddAll','RemoveAll','Approach'].includes(mode)?(t("RSIK.UI.DragAndRelease")):mode==='RemovePaint'?(t("RSIK.UI.RemoveSelected")):mode==='AddAll'?(t("RSIK.UI.AddAll")):mode==='RemoveAll'?(t("RSIK.UI.RemoveAll")):mode==='Guide'?((step===2?t("RSIK.UI.ConnectEnd"):t("RSIK.UI.SelectStart"))):(mode==='GuideAuto'||mode==='Approach')?(t("RSIK.UI.Generate")):(t("RSIK.UI.Apply"));
 const right=mode==='RedPaving'?translate('RSIK.RedPaving.Right',t("RSIK.UI.DeleteGroupCancel")):mode==='ColorGuide'?(t("RSIK.UI.DeleteGuideCancel")):mode==='DrawLine'?(t("RSIK.UI.DeleteCancel")):mode==='CrossingDistance'?(t("RSIK.UI.ExitTool")):mode==='RemovePaint'?(t("RSIK.UI.RestoreRemoved")):mode.startsWith('Manual')?(t("RSIK.UI.DeleteMarking")):['AddAll','RemoveAll'].includes(mode)?(t("RSIK.UI.ExitTool")):mode==='GuideAuto'?(t("RSIK.UI.ClearAll")):mode==='Guide'?(t("RSIK.UI.CancelDelete")):(t("RSIK.UI.Reset"));
 const card=useRef<HTMLDivElement>(null);
 useEffect(()=>{
  if(!active||docked)return;
  let x=-1,y=-1,overUI=false,frame=0;
  const place=()=>{frame=0;const el=card.current;if(!el)return;
   if(x<0||overUI){el.style.visibility='hidden';return;}
   const box=el.getBoundingClientRect(),gap=18,pad=10;
   let left=x+gap,top=y+gap;
   if(left+box.width>window.innerWidth-pad)left=x-box.width-gap;
   if(top+box.height>window.innerHeight-pad)top=y-box.height-gap;
   el.style.left=Math.max(pad,Math.min(left,window.innerWidth-box.width-pad))+'px';
   el.style.top=Math.max(pad,Math.min(top,window.innerHeight-box.height-pad))+'px';el.style.visibility='visible';
  };
  const schedule=()=>{if(!frame)frame=window.requestAnimationFrame(place);};
  const move=(e:MouseEvent)=>{x=e.clientX;y=e.clientY;overUI=e.target instanceof Element&&!!e.target.closest('button,input,[data-rsik-menu]');schedule();};
  const hide=()=>{if(card.current)card.current.style.visibility='hidden';};
  window.addEventListener('mousemove',move);window.addEventListener('resize',schedule);window.addEventListener('blur',hide);document.addEventListener('mouseleave',hide);
  return()=>{if(frame)window.cancelAnimationFrame(frame);window.removeEventListener('mousemove',move);window.removeEventListener('resize',schedule);window.removeEventListener('blur',hide);document.removeEventListener('mouseleave',hide);};
 },[active,docked,open]);
 if(!active)return null;
 const detailMessages:Record<string,string>={
  'line.missing':t("RSIK.UI.SelectedNetLaneAssetIsUnavailable"),
  'detail.position':t("RSIK.UI.PositionOnSidewalk"),
  'detail.snapped':t("RSIK.UI.SnappedToAdjacentStrip"),
  'detail.narrow':t("RSIK.UI.SidewalkIsTooNarrow"),
  'detail.missing':t("RSIK.UI.RequiredNetLaneAssetIsMissing")
 };
 const hoverText=mode==='RedPaving'?translate(signHover,signHover):detailMessages[signHover]||(signHover||'').replace('에셋 없음 / Unavailable',t("RSIK.UI.Unavailable"));
 const send=(command:string)=>trigger('RoadStyleIntoKorea','command',command);
 return <div ref={card} className={docked?styles.docked:styles.card} data-rsik-hint="true" role={docked?'region':'tooltip'} aria-label={t("RSIK.UI.SelectedToolInstructions")}>
  <div className={styles.current}><img className={styles.thumb} src={icon(value)} alt=""/><div><span className={styles.caption}>{t("RSIK.UI.Selected")}</span><strong>{name(value)}</strong></div></div>
  {['RedPaving','ColorGuide','DrawLine','ManualSpeed','Tactile','CyclePath'].includes(mode)&&signHover&&<div className={styles.caption}>{hoverText}</div>}
  {cycling&&(docked?<Button className={styles.change} onSelect={()=>send('cycle')}><img className={styles.thumb} src={icon(next)} alt=""/><span>{t("RSIK.UI.ChangeToNext")}<strong>{name(next)}</strong></span><span>›</span></Button>:<div className={styles.next}><img className={styles.thumb} src={icon(next)} alt=""/><div><span className={styles.caption}>{t("RSIK.UI.Next")}</span><span className={styles.name}>{name(next)}</span></div></div>)}
  {mode==='RedPaving'&&<div className={styles.instruction}>{translate('RSIK.RedPaving.Help',t("RSIK.UI.SelectStartAndEndOnTheSameLane"))}</div>}
  {docked&&mode!=='RedPaving'&&<div className={styles.instruction}>{(['Tactile','CyclePath'].includes(mode)?t("RSIK.UI.PositionAcrossTheSidewalkClickApplyToSegmentRightClickRemoveThisStrip"):(mode==='SideRoad'?t("RSIK.UI.MoveThePointerNearEitherRoadEdge"):(mode==='SpeedHump'?t("RSIK.UI.HoverALaneToPreviewTheFullSpanInThatDirection"):(['AddAll','RemoveAll','Approach'].includes(mode)?t("RSIK.UI.DragOverRoadsToSelectTheAffectedArea"):(mode==='Guide'?(step===2?t("RSIK.UI.ClickAYellowEndpointToConnect"):t("RSIK.UI.SelectTheStartingLaneEndpoint")):t("RSIK.UI.HoverOverARoadToPreviewThenClickToApply"))))))}</div>}
  <div className={styles.actions}>
   <span><img src="coui://rsik/RSIKMouseLeft.png" alt=""/>{left}</span>
   <span><img src="coui://rsik/RSIKMouseRight.png" alt=""/>{right}</span>
  </div>
  {cycling&&<div className={styles.keys}><kbd>Shift</kbd><span>+</span><kbd>R</kbd><span className={styles.keyLabel}>{t("RSIK.UI.Style")}</span></div>}
  {mode==='CrossingDistance'&&<div className={styles.keys}><kbd>Shift</kbd><span>{t("RSIK.UI.Click1m020m")}</span></div>}
 {crossing&&<div className={styles.keys}><kbd>Ctrl</kbd><span className={styles.keyLabel}>{t("RSIK.UI.ClickAllCrossings")}</span></div>}
  {crossing&&<div className={styles.keys}><kbd>Shift</kbd><span>+</span><span className={styles.keyLabel}>{t("RSIK.UI.ClickPedestrianArrows")}</span></div>}
  {mode==='SpeedHump'&&<div className={styles.keys}><span className={styles.keyLabel}>{t("RSIK.UI.ShiftClickBothDirections")}</span></div>}
  {mode.startsWith('Manual')&&<div className={styles.keys}><kbd>Alt</kbd><span className={styles.keyLabel}>{t("RSIK.UI.ReverseSharedLane")}</span></div>}
  {mode.startsWith('Manual')&&<div className={styles.keys}><kbd>Shift</kbd><span className={styles.keyLabel}>{t("RSIK.UI.SameDirectionLanes")}</span></div>}
  {(mode==='UTurn'||mode==='SideRoad')&&<div className={styles.keys}><kbd>+</kbd><kbd>−</kbd><span className={styles.keyLabel}>{t("RSIK.UI.AdjustLength")}</span></div>}
  {docked&&(mode==='UTurn'||mode==='SideRoad')&&<div className={styles.controls}><Button disabled={value<=(mode==='SideRoad'?3:4)} onSelect={()=>send('narrower')} aria-label={t("RSIK.UI.DecreaseBy1m")}>−</Button><span>{value} m</span><Button disabled={value>=(mode==='SideRoad'?12:24)} onSelect={()=>send('wider')} aria-label={t("RSIK.UI.IncreaseBy1m")}>+</Button></div>}
  {docked&&<Button className={styles.exit} onSelect={()=>send('exit')}>{t("RSIK.UI.DeselectTool")}</Button>}
 </div>;
}


















