using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 bool SnapRedEndpoint(float3 hit,out LinePoint point,out Entity owner){
  point=default;owner=Entity.Null;float best=1.44f;
  foreach(var entry in lineHits){if(!IsRoad(entry.Owner))continue;
   foreach(int end in new[]{0,1}){
    var position=end==0?entry.Curve.a:entry.Curve.d;
    float d=math.distancesq(position,hit);if(d>=best)continue;
    bool joint=false;foreach(var other in lineHits)if(other!=entry&&other.Group==entry.Group&&entry.Group!=0&&
     (math.distancesq(position,other.Curve.a)<.01f||math.distancesq(position,other.Curve.d)<.01f)){joint=true;break;}
    if(joint)continue;
    PenLane nearest=null;float at=0,distance=float.MaxValue;
    foreach(var lane in penLanes){if(lane.Owner!=entry.Owner)continue;float t=lane.Arc.Nearest(position);
     float delta=math.distancesq(Geometry.Position(lane.Curve,t),position);
     if(delta<distance&&delta<math.pow(math.max(.75f,entry.Width*.5f),2)&&math.abs(math.dot(Geometry.Tangent(lane.Curve,t),Geometry.Tangent(entry.Curve,end)))>.8f){nearest=lane;distance=delta;at=t;}}
    if(nearest==null)continue;best=d;owner=entry.Owner;
    point=new LinePoint{Position=position,Tangent=Geometry.Tangent(nearest.Curve,at),Lane=nearest.Lane,PointOwner=owner,T=at,RedCapWidth=entry.Width};
   }
  }return owner!=Entity.Null;
 }
 void AlignRedCaps(LinePoint first,LinePoint last){
  if(lineRoute.Count==0)return;
  for(int i=1;i+1<lineRoute.Count;i++)if(LineNode(lineRoute[i].Road)){
   var bridge=lineRoute[i];var before=lineRoute[i-1];var after=lineRoute[i+1];
   bridge.Curve.b+=before.Curve.d-bridge.Curve.a;bridge.Curve.a=before.Curve.d;
   bridge.Curve.c+=after.Curve.a-bridge.Curve.d;bridge.Curve.d=after.Curve.a;
   bridge.Width=math.min(before.Width,after.Width);lineRoute[i]=bridge;
  }
  if(first.RedCapWidth>0){var head=lineRoute[0];var delta=first.Position-head.Curve.a;head.Curve.a=first.Position;head.Curve.b+=delta;head.Width=first.RedCapWidth;lineRoute[0]=head;}
  if(last.RedCapWidth>0){int i=lineRoute.Count-1;var tail=lineRoute[i];var delta=last.Position-tail.Curve.d;tail.Curve.d=last.Position;tail.Curve.c+=delta;tail.Width=last.RedCapWidth;lineRoute[i]=tail;}
 }
}}
