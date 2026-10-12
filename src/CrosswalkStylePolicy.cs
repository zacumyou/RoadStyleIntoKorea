namespace RoadStyleIntoKorea
{
    internal static class CrosswalkStylePolicy
    {
        // Keep saved 2/3/4 values as double-row styles. 13 adds single-row 3m.
        internal const int SingleThree=13;
        internal static bool Valid(int value)=>value==2||value==3||value==4||value==SingleThree;
        internal static bool Single(int value)=>value==SingleThree;
        internal static int Length(int value)=>Single(value)?3:value;
        internal static float Width(int value)=>Single(value)?3f:Length(value)*2+.2f;
        // Hold the junction-facing edge fixed when replacing a narrower native crossing.
        internal static float Setback(float nativeWidth,float paintWidth,float alignment)
            =>System.Math.Max(0,(paintWidth-System.Math.Max(0,nativeWidth))*.5f)/System.Math.Max(.5f,System.Math.Abs(alignment));
        internal static int Next(int value)=>value==2?3:value==3?4:value==4?SingleThree:2;
        internal static int Minimum(int requested,bool automatic)=>Single(requested)||!automatic?requested:2;
        internal static string Asset(int value)=>Single(value)?"CSKRCrosswalkSingle3mNetLane":"CSKRCrosswalk"+value+"mNetLane";
        internal static string Label(int value,bool ko)=>(Single(value)?(ko?"한 줄 ":"Single row "):(ko?"두 줄 ":"Double row "))+Length(value)+" m";
    }
}
