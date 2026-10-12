using System;using System.Collections.Generic;
namespace RoadStyleIntoKorea {
 internal sealed class CoalescedIntEdits<T> {
  struct Edit {internal int Value,Clicks;internal float Due;}
  readonly Dictionary<T,Edit> edits=new Dictionary<T,Edit>();
  internal int Count=>edits.Count;
  internal int Value(T key,int fallback)=>edits.TryGetValue(key,out var edit)?edit.Value:fallback;
  internal int Adjust(T key,int current,int delta,float now){
   edits.TryGetValue(key,out var edit);int previous=Value(key,current);int value=Math.Max(0,Math.Min(20,previous+delta));
   if(value==previous)return value; // A clamped click must not postpone a real pending edit.
   edits[key]=new Edit{Value=value,Clicks=edit.Clicks+1,Due=now+.2f};return value;
  }
  internal bool Take(float now,out T key,out int value,out int clicks){
   key=default;value=clicks=0;bool found=false;
   foreach(var pair in edits)if(pair.Value.Due<=now){key=pair.Key;value=pair.Value.Value;clicks=pair.Value.Clicks;found=true;break;}
   if(found)edits.Remove(key);return found;
  }
  internal void Clear()=>edits.Clear();
 }
}
