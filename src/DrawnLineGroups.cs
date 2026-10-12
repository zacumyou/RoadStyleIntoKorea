using Unity.Collections;
using Unity.Entities;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 void RemoveDrawnGroup(long group){
  using(var query=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<DrawnLineChoice>()))
  using(var owners=query.ToEntityArray(Allocator.Temp))foreach(var owner in owners){
   var choices=EntityManager.GetBuffer<DrawnLineChoice>(owner);bool changed=false;
   for(int i=choices.Length-1;i>=0;i--)if(choices[i].Group==group){choices.RemoveAt(i);changed=true;}
   if(changed)manualDirty.Add(owner);
  }
 }
}}
