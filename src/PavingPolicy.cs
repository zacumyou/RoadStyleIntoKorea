using System;
using System.Collections.Generic;
namespace RoadStyleIntoKorea
{
    internal static class PavingPolicy
    {
        internal const int StyleCount=5;
        internal static int NextStyle(int style)=>style<1||style>=StyleCount?1:style+1;
        internal struct Strip { internal float Center,Width; }
        internal static float PaintMargin(float half,float gap)=>2*System.Math.Max(.13f,half)+System.Math.Max(0,System.Math.Min(1,gap))+.1f;
        internal static readonly float[] Widths={8,5,4,3.5f,2,1,.5f};
        internal static string Asset(int style,float width)=>"CSKRBrickPed0"+style+"W"+width.ToString("0.#",System.Globalization.CultureInfo.InvariantCulture).Replace('.','p')+"mNetLane";
        internal static bool Mixed(string name)=>!string.IsNullOrEmpty(name)&&(name.IndexOf("Grass",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("Gravel",StringComparison.OrdinalIgnoreCase)>=0);
        internal static bool PlantedSurface(string name)=>Mixed(name)||(!string.IsNullOrEmpty(name)&&(name.IndexOf("Soil",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("Dirt",StringComparison.OrdinalIgnoreCase)>=0));
        internal static bool Bay(string name)=>!string.IsNullOrEmpty(name)&&name.EndsWith(" - Bay",StringComparison.OrdinalIgnoreCase);
        internal static bool Contains(float min,float max,float innerMin,float innerMax)=>max>min&&innerMax>innerMin&&innerMin>=min-.05f&&innerMax<=max+.05f;
        internal static bool DetailOffset(bool bay,float pieceMin,float pieceMax,float min,float max,float half,float saved,out float offset){
            offset=saved;
            if(max-min<half*2-.001f)return false;
            if(saved>=min+half-.001f&&saved<=max-half+.001f)return true;
            if(!bay||saved<pieceMin||saved>pieceMax)return false;
            offset=Math.Max(min+half,Math.Min(max-half,saved));return true;
        }
        internal static int Kind(string name)
        {
            if(string.IsNullOrEmpty(name))return 0;
            if(name.IndexOf("Median",StringComparison.OrdinalIgnoreCase)>=0)return 0;
            if(name.IndexOf("Sidewalk",StringComparison.OrdinalIgnoreCase)>=0&&name.IndexOf("ToSidewalk",StringComparison.OrdinalIgnoreCase)<0)return 1;
            if(Mixed(name))return 0;
            if(name.StartsWith("RB Empty Piece",StringComparison.OrdinalIgnoreCase)||name.StartsWith("RB Tiled Empty Piece",StringComparison.OrdinalIgnoreCase)||name.StartsWith("Highway Shoulder ",StringComparison.OrdinalIgnoreCase)||name.StartsWith("Road Shoulder Piece",StringComparison.OrdinalIgnoreCase)||name.StartsWith("Alley Shoulder ",StringComparison.OrdinalIgnoreCase)||name.StartsWith("Alley Shoulder Piece",StringComparison.OrdinalIgnoreCase))return 2;
            return 0;
        }
        internal static List<Strip> Cover(float min,float max)
        {
            var result=new List<Strip>();if(float.IsNaN(min)||float.IsNaN(max)||float.IsInfinity(min)||float.IsInfinity(max)||max-min<.5f)return result;
            float cursor=min;
            while(max-cursor>.001f&&result.Count<128)
            {
                float remaining=max-cursor,width=.5f;
                foreach(var w in Widths)if(w<=remaining+.001f){width=w;break;}
                // A short final remainder overlaps the preceding opaque strip instead of
                // widening a brick or spilling outside the target boundary.
                float start=Math.Min(cursor,max-width);
                result.Add(new Strip{Center=start+width*.5f,Width=width});cursor=start+width;
            }
            return result;
        }
    }
}

