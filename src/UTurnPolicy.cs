using System;
using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Net;
using Unity.Mathematics;

namespace RoadStyleIntoKorea
{
    internal static class UTurnPolicy
    {
        internal const float Reserve=1, DefaultLength=8;
        internal static bool EligibleRoad(int forward,int backward)=>forward>0&&backward>0&&forward+backward>2;
        internal static bool AllowedTurn(CarLaneFlags flags)=>(flags&(CarLaneFlags.UTurnLeft|CarLaneFlags.UTurnRight))!=0
            &&(flags&(CarLaneFlags.Forbidden|CarLaneFlags.Highway|CarLaneFlags.SideConnection))==0;
        internal static bool CenterLine(string name)=>(name.IndexOf("Car Center Line",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("Highway Center Line",StringComparison.OrdinalIgnoreCase)>=0);

        // Stops are distances along one continuous center line, regardless of its stored direction.
        // Positive travel approaches the high end; negative travel approaches the low end.
        internal struct Anchor {internal float At;internal bool Positive,Generate;internal Anchor(float at,bool positive,bool generate=true){At=at;Positive=positive;Generate=generate;}}
        internal static List<Bounds1> Zones(float length,List<Anchor> stops,float requested)
        {
            var result=new List<Bounds1>();
            foreach(var stop in stops)
            {
                if(!stop.Generate||stop.At<0||stop.At>length)continue;
                float near=stop.At+(stop.Positive?-Reserve:Reserve);
                foreach(var other in stops)if(other.Positive==stop.Positive)
                    near=stop.Positive?math.min(near,other.At-Reserve):math.max(near,other.At+Reserve);
                float limit=stop.Positive?Reserve:length-Reserve;
                foreach(var other in stops)
                {
                    if(other.Positive==stop.Positive)continue;
                    // Leave at least 1 m of original center line between facing zones.
                    if(stop.Positive&&other.At<stop.At)limit=math.max(limit,(stop.At+other.At)*.5f+Reserve*.5f);
                    if(!stop.Positive&&other.At>stop.At)limit=math.min(limit,(stop.At+other.At)*.5f-Reserve*.5f);
                }
                float available=stop.Positive?near-limit:limit-near;
                float size=math.floor(math.min(math.max(0,requested),available)+.0001f);
                if(size<1)continue; // Whole 0.5 m dash + 0.5 m gap cells only.
                var zone=stop.Positive?new Bounds1(near-size,near):new Bounds1(near,near+size);
                bool duplicate=false;foreach(var old in result)if(math.abs(old.min-zone.min)<.05f&&math.abs(old.max-zone.max)<.05f){duplicate=true;break;}
                if(!duplicate)result.Add(zone);
            }
            result.Sort((a,b)=>a.min.CompareTo(b.min));
            // Multiple connections from the same approach must never overpaint each other.
            for(int i=result.Count-1;i>0;i--)if(result[i].min<result[i-1].max+.001f)
            {result[i-1]=new Bounds1(result[i-1].min,math.max(result[i-1].max,result[i].max));result.RemoveAt(i);}
            return result;
        }
        internal static bool StopPlane(Geometry.Route line,float3 origin,float3 normal,out float at)
        {
            at=0;bool found=false;float best=float.MaxValue;
            float last=0,previous=math.dot(line.At(0)-origin,normal);
            for(int i=1;i<=128;i++)
            {
                float next=line.Length*i/128f,value=math.dot(line.At(next)-origin,normal);
                if((previous<=0&&value>=0)||(previous>=0&&value<=0))
                {
                    float lo=last,hi=next;bool rising=value>=previous;
                    for(int j=0;j<20;j++){float mid=(lo+hi)*.5f;bool below=math.dot(line.At(mid)-origin,normal)<0;if(below==rising)lo=mid;else hi=mid;}
                    float hit=(lo+hi)*.5f,error=math.distancesq(line.At(hit),origin);
                    if(error<best){best=error;at=hit;found=true;}
                }
                last=next;previous=value;
            }
            return found;
        }
    }
}
