using System;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 internal static class NativePavingAtlasPolicy {
  // RoadParts native sidewalk atlas: curb ends before U=.30; pavement starts
  // at U=.3276. Validate each source mesh before using this range.
  internal const float Min=.3125f,Max=1f;
  internal static bool Candidate(string name)=>!string.IsNullOrEmpty(name)&&name.IndexOf("Sidewalk",StringComparison.OrdinalIgnoreCase)>=0&&name.IndexOf("Median",StringComparison.OrdinalIgnoreCase)<0;
  internal static bool Validate(float2[] uv,int[] faces,int[] pavement){
   if(pavement.Length==0)return false;
   var selected=new System.Collections.Generic.HashSet<int>();foreach(int i in pavement)selected.Add(i);
   for(int i=0;i+2<faces.Length;i+=3){
    int a=faces[i],b=faces[i+1],c=faces[i+2];
    float lo=math.min(uv[a].x,math.min(uv[b].x,uv[c].x)),hi=math.max(uv[a].x,math.max(uv[b].x,uv[c].x));
    if(selected.Contains(a)&&selected.Contains(b)&&selected.Contains(c)){if(lo<Min||hi>Max+.0001f)return false;}
    else if(hi>Min+.001f&&lo<Max-.001f)return false;
   }
   return true;
  }
  internal static int[] Columns(int width,int tileWidth){
   var result=new int[width];for(int x=0;x<width;x++)result[x]=(int)(math.frac(Tile((x+.5f)/width,0).x)*tileWidth);return result;
  }
  internal static int[] Rows(int height,int tileHeight){
   var result=new int[height];for(int y=0;y<height;y++)result[y]=(int)(math.frac(Tile(0,(y+.5f)/height).y)*tileHeight);return result;
  }
  internal static float2 Tile(float u,float v)=>new float2((u-.32763671875f)*2f,v*4f);
 }
}
