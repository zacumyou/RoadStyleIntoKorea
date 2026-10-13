using Colossal.Serialization.Entities;
using Unity.Entities;
namespace RoadStyleIntoKorea {
    public struct CrosswalkChoice:IComponentData,ISerializable
    {
        public int Start,End;public bool ForceStartArrows,ForceEndArrows;
        public void Serialize<T>(T w) where T:IWriter {w.Write(2);w.Write(Start);w.Write(End);w.Write(ForceStartArrows);w.Write(ForceEndArrows);}
        public void Deserialize<T>(T r) where T:IReader {r.Read(out int v);r.Read(out Start);r.Read(out End);ForceStartArrows=ForceEndArrows=false;if(v>=2){r.Read(out ForceStartArrows);r.Read(out ForceEndArrows);}if(v<1||v>2)Start=End=0;}
    }
}
