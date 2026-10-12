using Colossal.Serialization.Entities;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 public struct CrossingDistanceChoice:IComponentData,ISerializable {
  public int Metres;
  public void Serialize<T>(T writer) where T:IWriter {writer.Write(1);writer.Write(Metres);}
  public void Deserialize<T>(T reader) where T:IReader {reader.Read(out int version);reader.Read(out Metres);Metres=math.clamp(Metres,0,20);}
 }
}
