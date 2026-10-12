using Game.Prefabs;
namespace RoadStyleIntoKorea {
 internal static class NativeCrossingPolicy {
  internal const CompositionFlags.Side Mask=CompositionFlags.Side.AddCrosswalk|CompositionFlags.Side.RemoveCrosswalk;
  internal static CompositionFlags.Side Add(CompositionFlags.Side flags)=>(flags&~Mask)|CompositionFlags.Side.AddCrosswalk;
  internal static CompositionFlags.Side Restore(CompositionFlags.Side current,CompositionFlags.Side original)
   =>(current&Mask)==CompositionFlags.Side.AddCrosswalk?(current&~Mask)|(original&Mask):current;
 }
}
