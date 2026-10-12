using System.Collections.Generic;
namespace RoadStyleIntoKorea {
    internal sealed class WorkQueue<T>
    {
        readonly List<T> items=new List<T>();int head;
        internal int Count=>items.Count-head;
        internal void Enqueue(T item)=>items.Add(item);
        internal T Peek()=>items[head];
        internal void Promote(HashSet<T> priority){
            if(priority.Count==0||Count<2)return;
            var ordered=new List<T>();for(int i=head;i<items.Count;i++)if(priority.Contains(items[i]))ordered.Add(items[i]);
            for(int i=head;i<items.Count;i++)if(!priority.Contains(items[i]))ordered.Add(items[i]);
            items.Clear();items.AddRange(ordered);head=0;
        }
        internal T Dequeue(){
            var value=items[head];items[head++]=default(T);
            if(head==items.Count)Clear();
            else if(head>=1024&&head>=Count){items.RemoveRange(0,head);head=0;}
            return value;
        }
        internal void Clear(){items.Clear();head=0;}
    }
}
