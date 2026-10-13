using Colossal.Serialization.Entities;using Unity.Entities;using Unity.Mathematics;
namespace RoadStyleIntoKorea {
    public struct CenterPathChoice:IBufferElementData,ISerializable
    {
        public float Offset;public int Code;
        public void Serialize<T>(T w) where T:IWriter{w.Write(1);w.Write(Offset);w.Write(Code);}
        public void Deserialize<T>(T r) where T:IReader{r.Read(out int v);r.Read(out Offset);r.Read(out Code);if(v!=1||!math.isfinite(Offset)){Offset=0;Code=0;}Code=math.clamp(Code,0,5);}
    }
}
