using Colossal.Serialization.Entities;using Unity.Entities;using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 // Placement instructions only; rendered entities remain transient.
 public struct ManualDecalChoice:IBufferElementData,ISerializable {
  public int Kind,Symbol;public float T,Offset,Height,Width;public bool Reverse;
  public void Serialize<TWriter>(TWriter w) where TWriter:IWriter {w.Write(5);w.Write(Kind);w.Write(Symbol);w.Write(T);w.Write(Offset);w.Write(Height);w.Write(Width);w.Write(Reverse);}
  public void Deserialize<TReader>(TReader r) where TReader:IReader {r.Read(out int v);r.Read(out Kind);r.Read(out Symbol);r.Read(out T);r.Read(out Offset);r.Read(out Height);r.Read(out Width);r.Read(out Reverse);if((v<1||v>5)||Kind<1||Kind>(v==1?4:v==2?6:v==3?7:v==4?8:10)||!math.all(math.isfinite(new float4(T,Offset,Height,Width))))Kind=0;T=math.saturate(T);Width=math.clamp(Width,Kind==9?.3f:.5f,(Kind==1||Kind==8)?128:12);Symbol=math.clamp(Symbol,0,Kind==8?1:Kind==5?38:Kind==6?32:Kind==7?6:9);}
 }

}

