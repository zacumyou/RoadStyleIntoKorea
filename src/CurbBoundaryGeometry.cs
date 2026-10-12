using System.Collections.Generic;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 internal static class CurbBoundaryGeometry {
  // Each pair is a boundary edge from the clipped pavement triangles. Do not
  // bridge missing/planted pieces or sort a turning boundary by world X/Z.
  internal static List<List<float3>> Chains(List<float3> pairs){
   var points=new List<float3>();var ids=new Dictionary<int3,int>();var edges=new HashSet<ulong>();
   var adjacency=new Dictionary<int,List<int>>();
   for(int i=0;i+1<pairs.Count;i+=2){
    var ends=new int[2];for(int j=0;j<2;j++){
     var key=(int3)math.round(pairs[i+j]*1000);
     if(!ids.TryGetValue(key,out int id)){id=points.Count;ids[key]=id;points.Add(pairs[i+j]);adjacency[id]=new List<int>();}ends[j]=id;
    }
    int a=ends[0],b=ends[1];if(a==b)continue;
    ulong edge=((ulong)(uint)math.min(a,b)<<32)|(uint)math.max(a,b);
    if(!edges.Add(edge))continue;adjacency[a].Add(b);adjacency[b].Add(a);
   }
   var result=new List<List<float3>>();var visited=new HashSet<ulong>();
   // Open chains first, then any remaining closed loop.
   for(int pass=0;pass<2;pass++)for(int start=0;start<points.Count;start++){
    if(pass==0&&adjacency[start].Count==2)continue;
    foreach(int first in adjacency[start]){
     ulong firstKey=((ulong)(uint)math.min(start,first)<<32)|(uint)math.max(start,first);
     if(visited.Contains(firstKey))continue;
     var chain=new List<float3>{points[start]};int current=start,next=first;
     while(true){
      ulong key=((ulong)(uint)math.min(current,next)<<32)|(uint)math.max(current,next);
      if(!visited.Add(key))break;chain.Add(points[next]);current=next;
      if(current==start||adjacency[current].Count!=2)break;
      next=-1;foreach(int candidate in adjacency[current]){
       ulong candidateKey=((ulong)(uint)math.min(current,candidate)<<32)|(uint)math.max(current,candidate);
       if(!visited.Contains(candidateKey)){next=candidate;break;}
      }
      if(next<0)break;
     }
     if(chain.Count>1)result.Add(chain);
    }
   }
   return result;
  }
 }
}
