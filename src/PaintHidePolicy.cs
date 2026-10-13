using System.Collections.Generic;
namespace RoadStyleIntoKorea {
 internal sealed class PaintHidePolicy<TItem,TOwner> {
  readonly Dictionary<TItem,HashSet<TOwner>> owners=new Dictionary<TItem,HashSet<TOwner>>();
  internal IEnumerable<TItem> Items=>owners.Keys;
  internal bool Contains(TItem item)=>owners.ContainsKey(item);
  internal bool Add(TItem item,TOwner owner){
   if(!owners.TryGetValue(item,out var set))owners[item]=set=new HashSet<TOwner>();
   return set.Add(owner);
  }
  internal bool Release(TItem item,TOwner owner){
   if(!owners.TryGetValue(item,out var set))return false;
   if(!set.Remove(owner)||set.Count>0)return false;
   owners.Remove(item);return true;
  }
 }
}
