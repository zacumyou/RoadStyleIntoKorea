using System.Text;
using System.Reflection;
using Colossal.Mathematics;
namespace RoadStyleIntoKorea { internal static class CaptureFields {
        internal static string Fields(object value)
        {
            if(value==null)return "null";
            var b=new StringBuilder();
            foreach(var f in value.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance))b.Append(f.Name).Append('=').Append(Value(f.GetValue(value))).Append(';');
            return b.ToString();
        }
        internal static string Value(object value)
        {
            if(value is Bounds3 b3)return "{min="+b3.min+",max="+b3.max+",size="+(b3.max-b3.min)+"}";
            if(value is Bounds2 b2)return "{min="+b2.min+",max="+b2.max+"}";
            if(value is Bounds1 b1)return "{min="+b1.min+",max="+b1.max+"}";
            return value?.ToString()??"null";
        }
}}
