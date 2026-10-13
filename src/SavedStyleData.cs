using Colossal.Serialization.Entities;
using Colossal.Mathematics;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 // Entity references use the game's remapping writer, never raw entity indices.
 public struct SavedStyleEntry:IBufferElementData,ISerializable {
  public Entity Item;public int Group,Flags;
  public void Serialize<T>(T w) where T:IWriter {w.Write(Item);w.Write(Group);w.Write(Flags);}
  public void Deserialize<T>(T r) where T:IReader {r.Read(out Item);r.Read(out Group);r.Read(out Flags);}
 }
 public struct SavedStyleState:IComponentData,ISerializable {
  public int Version;
  public void Serialize<T>(T w) where T:IWriter {w.Write(Version);}
  public void Deserialize<T>(T r) where T:IReader {r.Read(out Version);}
 }
 public struct SavedStyleShape:IBufferElementData,ISerializable {
  public int Kind;public Entity Item;public Bezier4x3 Curve;public float Width;
  public void Serialize<T>(T w) where T:IWriter {w.Write(Kind);w.Write(Item);w.Write(Curve.a);w.Write(Curve.b);w.Write(Curve.c);w.Write(Curve.d);w.Write(Width);}
  public void Deserialize<T>(T r) where T:IReader {r.Read(out Kind);r.Read(out Item);r.Read(out Curve.a);r.Read(out Curve.b);r.Read(out Curve.c);r.Read(out Curve.d);r.Read(out Width);}
 }
 public struct SavedStyleFlow:IComponentData,ISerializable {
  public Curve Original,Applied;
  public void Serialize<T>(T w) where T:IWriter {w.Write(Original.m_Bezier);w.Write(Original.m_Length);w.Write(Applied.m_Bezier);w.Write(Applied.m_Length);}
  public void Deserialize<T>(T r) where T:IReader {r.Read(out Original.m_Bezier);r.Read(out Original.m_Length);r.Read(out Applied.m_Bezier);r.Read(out Applied.m_Length);}
 }
 public struct SavedPavingVertex:IBufferElementData,ISerializable {
  public float3 Position,Normal;public float2 UV;public float4 Tangent;
  public void Serialize<T>(T w) where T:IWriter {w.Write(Position);w.Write(Normal);w.Write(UV);w.Write(Tangent);}
  public void Deserialize<T>(T r) where T:IReader {r.Read(out Position);r.Read(out Normal);r.Read(out UV);r.Read(out Tangent);}
 }
 public struct SavedPavingIndex:IBufferElementData,ISerializable {
  public int Value;
  public void Serialize<T>(T w) where T:IWriter {w.Write(Value);}
  public void Deserialize<T>(T r) where T:IReader {r.Read(out Value);}
 }
 public struct SavedPavingMesh:IBufferElementData,ISerializable {
  public int Style,Vertices,Indices;public float3 Position;
  public void Serialize<T>(T w) where T:IWriter {w.Write(Style);w.Write(Vertices);w.Write(Indices);w.Write(Position);}
  public void Deserialize<T>(T r) where T:IReader {r.Read(out Style);r.Read(out Vertices);r.Read(out Indices);r.Read(out Position);}
 }
}
