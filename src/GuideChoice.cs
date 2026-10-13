using Colossal.Serialization.Entities;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 public struct GuideChoice:IBufferElementData,ISerializable {
  public float3 Start,End;public bool CleanupOnly;
  public void Serialize<T>(T w) where T:IWriter {w.Write(2);w.Write(Start);w.Write(End);w.Write(CleanupOnly);}
  public void Deserialize<T>(T r) where T:IReader {r.Read(out int v);r.Read(out Start);r.Read(out End);CleanupOnly=false;if(v>=2)r.Read(out CleanupOnly);if((v!=1&&v!=2)||!math.all(math.isfinite(Start))||!math.all(math.isfinite(End)))Start=End=default;}
 }
}
