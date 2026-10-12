using Colossal.Serialization.Entities;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
    [InternalBufferCapacity(0)]
    public struct SideRoadChoice : IBufferElementData, ISerializable
    {
        public float T,Width;
        public int Side; // -1 = right, +1 = left in edge curve direction
        public void Serialize<TWriter>(TWriter w) where TWriter:IWriter {w.Write(1);w.Write(T);w.Write(Width);w.Write(Side);}
        public void Deserialize<TReader>(TReader r) where TReader:IReader {
            r.Read(out int version);r.Read(out T);r.Read(out Width);r.Read(out Side);
            if(version!=1||!math.isfinite(T)||!math.isfinite(Width)||T<0||T>1||Width<3||Width>20||math.abs(Side)!=1){T=0;Width=0;Side=0;}
        }
    }
}
