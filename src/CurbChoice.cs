using Colossal.Serialization.Entities;using Unity.Entities;using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 public struct CurbChoice:IBufferElementData,ISerializable {
  public int Kind;public float Min,Max;
  public void Serialize<T>(T w) where T:IWriter{w.Write(1);w.Write(Kind);w.Write(Min);w.Write(Max);}
  public void Deserialize<T>(T r) where T:IReader{r.Read(out int v);r.Read(out Kind);r.Read(out Min);r.Read(out Max);if(v!=1||!math.all(math.isfinite(new float2(Min,Max)))||Max<=Min)Kind=0;}
 }

}