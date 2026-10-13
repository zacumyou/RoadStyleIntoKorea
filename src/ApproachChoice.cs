using Colossal.Serialization.Entities;
using Unity.Entities;
namespace RoadStyleIntoKorea {
 public struct ApproachChoice:IComponentData,ISerializable {
  public bool Enabled,SuppressRoadSigns;
  public void Serialize<T>(T w) where T:IWriter {w.Write(2);w.Write(Enabled);w.Write(SuppressRoadSigns);}
  public void Deserialize<T>(T r) where T:IReader {
   r.Read(out int version);r.Read(out Enabled);SuppressRoadSigns=false;
   if(version==2)r.Read(out SuppressRoadSigns);
   if(version!=1&&version!=2)Enabled=false;
  }
 }
}
