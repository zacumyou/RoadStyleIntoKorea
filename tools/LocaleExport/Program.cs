using System;using System.IO;using System.Reflection;using System.Runtime.Serialization;using System.Collections.Generic;using System.Text;using System.Web.Script.Serialization;
class Program {
 static void Main(string[] args){
  AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{var name=new AssemblyName(e.Name).Name+".dll";foreach(var dir in new[]{args[0],args[1]}){var p=Path.Combine(dir,name);if(File.Exists(p))return Assembly.LoadFrom(p);}return null;};
  var a=Assembly.LoadFrom(Path.Combine(args[0],"RoadStyleIntoKorea.dll"));
  var mod=FormatterServices.GetUninitializedObject(a.GetType("RoadStyleIntoKorea.Mod"));
  var settings=Activator.CreateInstance(a.GetType("RoadStyleIntoKorea.Settings"),new[]{mod});
  Directory.CreateDirectory(args[2]);
  foreach(var ko in new[]{false,true}){
   var type=a.GetType("RoadStyleIntoKorea.Locale");var locale=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{settings,ko},null);
   var entries=(IEnumerable<KeyValuePair<string,string>>)type.GetMethod("ReadEntries").Invoke(locale,new object[]{null,null});
   var d=new SortedDictionary<string,string>();foreach(var kv in entries)d.Add(kv.Key,kv.Value);
   File.WriteAllText(Path.Combine(args[2],ko?"ko-KR.json":"en-US.json"),new JavaScriptSerializer().Serialize(d),new UTF8Encoding(false));Console.WriteLine((ko?"ko-KR":"en-US")+": "+d.Count+" keys; "+settings.GetType().GetMethod("GetSettingsLocaleID").Invoke(settings,null));
  }
 }
}
