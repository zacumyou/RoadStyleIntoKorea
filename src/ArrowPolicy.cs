using System.Collections.Generic;
using Game.Prefabs;
using Game.Net;
namespace RoadStyleIntoKorea {
internal static class ArrowPolicy {
        // The former 3.7m front reserve was shared by 5m and 7m symbols,
        // but their centers were aligned. Keep the old longest-arrow tip
        // location and derive each center from its actual forward bound.
        internal const float ReferenceFront=3.7f;
        internal static bool BehindStop(Unity.Mathematics.float3 position,Unity.Mathematics.float3 forward,Unity.Mathematics.float3 stop,Unity.Mathematics.float3 normal,Unity.Mathematics.float2 bounds,float halfWidth){
            float along=Unity.Mathematics.math.dot(forward,normal);
            float extent=Unity.Mathematics.math.max(bounds.x*along,bounds.y*along)+halfWidth*Unity.Mathematics.math.abs(Unity.Mathematics.math.dot(Geometry.Left(forward),normal));
            return Unity.Mathematics.math.dot(position-stop,normal)+extent<=-.1f;
        }
        internal static bool TryTopPlacement(float length,float stop,float gap,List<Unity.Mathematics.float2> bounds,out float first){
            first=0;if(bounds.Count==0||stop<0)return false;
            float tip=System.Math.Min(length-.1f,stop-System.Math.Max(.1f,gap-ReferenceFront));
            for(int i=0;i<bounds.Count;i++)if(tip-i*8-bounds[i].y+bounds[i].x<.1f)return false;
            first=tip-bounds[0].y;return true;
        }
        // Source SVG axis: 70 degrees to transverse = 20 degrees to travel.
        internal const float DiagonalDegrees=20;
        internal static CarLaneFlags GentleAngle(CarLaneFlags flags,float degrees)
        {
            const CarLaneFlags gentle=CarLaneFlags.GentleTurnLeft|CarLaneFlags.GentleTurnRight;
            return (flags&gentle)!=0&&System.Math.Abs(degrees)<DiagonalDegrees?(flags&~gentle)|CarLaneFlags.Forward:flags;
        }
        internal static LaneDirectionData MeasuredDirection(LaneDirectionData d,CarLaneFlags turns)
        {
            if(d.m_Left!=LaneDirectionType.None&&d.m_Left<LaneDirectionType.Square&&(turns&CarLaneFlags.GentleTurnLeft)==0){d.m_Left=LaneDirectionType.None;d.m_Forward=LaneDirectionType.Straight;}
            if(d.m_Right!=LaneDirectionType.None&&d.m_Right<LaneDirectionType.Square&&(turns&CarLaneFlags.GentleTurnRight)==0){d.m_Right=LaneDirectionType.None;d.m_Forward=LaneDirectionType.Straight;}
            return d;
        }
        internal const float MarkingGap=1;
        internal const float UTurnDiamondGap=3;
        internal static List<string> ConnectionSymbols(CarLaneFlags turns)
        {
            bool forward=(turns&CarLaneFlags.Forward)!=0;
            bool left=(turns&(CarLaneFlags.TurnLeft|CarLaneFlags.GentleTurnLeft))!=0;
            bool right=(turns&(CarLaneFlags.TurnRight|CarLaneFlags.GentleTurnRight))!=0;
            var result=new List<string>();
            if(left&&right&&(turns&CarLaneFlags.TurnLeft)!=0&&(turns&CarLaneFlags.TurnRight)!=0)return new List<string>{forward?"StraightLeftRight":"LeftRight"};
            if(forward&&left){result.Add("StraightLeft");forward=left=false;}
            else if(forward&&right){result.Add("StraightRight");forward=right=false;}
            if(forward)result.Add("Straight");
            if(left)result.Add(UTurnWarning(turns)=="LeftUTurn"?"LeftUTurn":(turns&CarLaneFlags.TurnLeft)!=0?"Left":"DiagonalLeft");
            if(right)result.Add((turns&CarLaneFlags.TurnRight)!=0?"Right":"DiagonalRight");
            if(result.Count==0&&(turns&CarLaneFlags.UTurnLeft)!=0)result.Add("UTurn");
            return result;
        }
        internal static List<string> AvailableSymbols(List<string> requested,System.Func<string,bool> available){
            var result=new List<string>();foreach(var symbol in requested){
                if((symbol=="LeftRight"||symbol=="StraightLeftRight")&&!available(symbol)){
                    result.Add(symbol=="LeftRight"?"Left":"StraightLeft");result.Add("Right");
                }else result.Add(symbol);
            }return result;
        }
        internal struct Occupied
        {
            internal float Center,Half;
            internal Occupied(float center,float half){Center=center;Half=half;}
        }
        internal static bool SecondaryClear(float center,float half,float arrowCenter,float arrowHalf)=>System.Math.Abs(center-arrowCenter)>=half+3*arrowHalf;
        internal static bool SecondaryClear(float center,float half,List<Occupied> arrows){
            foreach(var a in arrows)if(!SecondaryClear(center,half,a.Center,a.Half))return false;
            return true;
        }
        internal static string UTurnWarning(CarLaneFlags turns)
        {
            if((turns&CarLaneFlags.UTurnLeft)==0)return null;
            bool left=(turns&(CarLaneFlags.TurnLeft|CarLaneFlags.GentleTurnLeft))!=0;
            bool other=(turns&(CarLaneFlags.Forward|CarLaneFlags.TurnRight|CarLaneFlags.GentleTurnRight|CarLaneFlags.UTurnRight))!=0;
            return left&&!other?"LeftUTurn":"UTurn";
        }
        internal static List<string> RouteSymbols(LaneDirectionData direction,CarLaneFlags turns)
        {
            if(turns!=0)
            {
                if(direction.m_Forward!=LaneDirectionType.None)turns|=CarLaneFlags.Forward;
                if(direction.m_Right!=LaneDirectionType.None&&direction.m_Right!=LaneDirectionType.UTurn)turns|=direction.m_Right<LaneDirectionType.Square?CarLaneFlags.GentleTurnRight:CarLaneFlags.TurnRight;
                if(direction.m_Left!=LaneDirectionType.None&&direction.m_Left!=LaneDirectionType.UTurn)turns|=direction.m_Left<LaneDirectionType.Square?CarLaneFlags.GentleTurnLeft:CarLaneFlags.TurnLeft;
            }
            var actual=ConnectionSymbols(turns);
            // A native U-turn glyph may omit the ordinary turn permissions. Routing wins.
            if(actual.Count>0&&actual[0]!="UTurn")return actual;
            if(UTurnWarning(turns)=="LeftUTurn"&&direction.m_Forward==LaneDirectionType.None&&direction.m_Right==LaneDirectionType.None)
                return new List<string>{"LeftUTurn"};
            return ArrowSymbols(direction);
        }
        internal static bool TryUTurnWarning(float length,float before,float half,List<Occupied> arrows,List<Occupied> diamonds,out float center)
        {
            center=System.Math.Min(length-half,before-half-MarkingGap);
            // Advance U-turn paint belongs before the entire diamond group in travel order.
            // Never fall back into the reserved stop-arrow / diamond interval.
            float firstDiamond=float.MaxValue;
            foreach(var d in diamonds)firstDiamond=System.Math.Min(firstDiamond,d.Center-d.Half);
            if(diamonds.Count>0)center=System.Math.Min(center,firstDiamond-UTurnDiamondGap-half);
            var occupied=new List<Occupied>(arrows);occupied.AddRange(diamonds);
            for(int pass=0;pass<=occupied.Count+2;pass++)
            {
                if(center-half<0)return false;
                float next=center;
                foreach(var item in occupied)
                    if(center+half+MarkingGap>item.Center-item.Half&&center-half-MarkingGap<item.Center+item.Half)
                        next=System.Math.Min(next,item.Center-item.Half-MarkingGap-half);
                if(System.Math.Abs(next-center)<.0001f)return true;
                center=next;
            }
            return false;
        }
        internal static bool FitsStack(float length,float first,int count)
            =>count>0&&first-(count-1)*8>=3.7f&&first<=length-3.7f;
        internal static bool TryStopPlacement(float length,float stop,float gap,int count,out float first)
        {
            first=0;if(count<=0||stop<0)return false;
            float min=3.7f+(count-1)*8;
            float max=System.Math.Min(length-3.7f,stop-3.7f);
            if(max<min)return false;
            first=System.Math.Max(min,System.Math.Min(max,stop-System.Math.Max(3.8f,gap)));
            return true;
        }
        internal static List<string> ArrowSymbols(LaneDirectionData d)
        {
            // The supplied Korean pack has no right-hand U-turn glyph. Never mislabel it as a right turn.
            if(d.m_Right==LaneDirectionType.UTurn)return new List<string>();
            var v=new List<string>();bool straight=d.m_Forward!=LaneDirectionType.None;
            bool left=d.m_Left!=LaneDirectionType.None,right=d.m_Right!=LaneDirectionType.None;
            bool u=d.m_Left==LaneDirectionType.UTurn;
            if(left&&right&&!u&&d.m_Left>=LaneDirectionType.Square&&d.m_Right>=LaneDirectionType.Square)return new List<string>{straight?"StraightLeftRight":"LeftRight"};
            if(straight&&left&&!u&&d.m_Left>=LaneDirectionType.Square){v.Add("StraightLeft");straight=left=false;}
            else if(straight&&right&&d.m_Right>=LaneDirectionType.Square&&d.m_Right!=LaneDirectionType.UTurn){v.Add("StraightRight");straight=right=false;}
            if(straight)v.Add("Straight");
            if(left)v.Add(u?"UTurn":d.m_Left<LaneDirectionType.Square?"DiagonalLeft":"Left");
            if(right)v.Add(d.m_Right<LaneDirectionType.Square?"DiagonalRight":"Right");
            return v;
        }
}
}

