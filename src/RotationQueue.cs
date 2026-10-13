using System.Collections.Generic;
namespace RoadStyleIntoKorea {
 // Compact consumed slots so repeated offscreen scans retain O(pending owners) storage.
 internal sealed class RotationQueue<T> {
  readonly List<T> items=new List<T>();int head;
  internal int Count=>items.Count-head;
  internal void Enqueue(T item)=>items.Add(item);
  internal T Dequeue(){var item=items[head++];if(head==items.Count)Clear();else if(head>=256&&head>=items.Count/2){items.RemoveRange(0,head);head=0;}return item;}
  internal void Clear(){items.Clear();head=0;}
 }
}
