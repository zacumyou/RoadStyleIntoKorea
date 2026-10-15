using System;
using System.Collections.Generic;
namespace RoadStyleIntoKorea
{
    internal static class CaptureGraph
    {
        internal static List<T> Collect<T>(T root,int radius,int limit,Func<T,IEnumerable<T>> neighbors)
        {
            var result=new List<T>();var depth=new List<int>();var seen=new HashSet<T>();
            if(limit<=0)return result;
            result.Add(root);depth.Add(0);seen.Add(root);
            for(int head=0;head<result.Count;head++)
            {
                if(depth[head]>=radius)continue;
                foreach(var next in neighbors(result[head]))
                {
                    if(result.Count>=limit)break;
                    if(seen.Add(next)){result.Add(next);depth.Add(depth[head]+1);}
                }
            }
            return result;
        }
    }
}
