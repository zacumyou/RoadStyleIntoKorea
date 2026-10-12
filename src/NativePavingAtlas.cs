using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.IO.AssetDatabase;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Rendering;
using Game.Tools;
using Unity.Entities;
using UnityEngine;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 sealed class NativePavingVariant {internal RenderPrefab Prefab;internal bool Failed;internal float Deadline;}
 sealed class NativePavingBinding {
  internal Composition Original,Styled;
  internal readonly List<Entity> Owned=new List<Entity>();
 }
 readonly Dictionary<(RenderPrefab,int),NativePavingVariant> nativePavingVariants=new Dictionary<(RenderPrefab,int),NativePavingVariant>();
 readonly Dictionary<(SurfaceAsset,int),SurfaceAsset> nativePavingSurfaces=new Dictionary<(SurfaceAsset,int),SurfaceAsset>();
 readonly Dictionary<Entity,NativePavingBinding> nativePavingBindings=new Dictionary<Entity,NativePavingBinding>();
 readonly HashSet<Entity> nativePavingWaiting=new HashSet<Entity>();
 readonly List<TextureAsset> nativePavingPins=new List<TextureAsset>();
 float nativePavingRetry;
 readonly HashSet<RenderPrefab> nativePavingValidated=new HashSet<RenderPrefab>(),nativePavingRejected=new HashSet<RenderPrefab>();
 readonly HashSet<(SurfaceAsset,int)> nativeAtlasFailed=new HashSet<(SurfaceAsset,int)>();
 IEnumerator<SurfaceAsset> nativeAtlasJob;
 (SurfaceAsset,int) nativeAtlasKey;
 int nativePavingRetryStamp=-1;
 Texture2D ReadAtlas(Texture texture,int limit=8192){
  if(texture==null)throw new InvalidOperationException("Pavement texture unavailable");
  float scale=Math.Min(1f,(float)limit/Math.Max(texture.width,texture.height));
  int width=Math.Max(1,(int)(texture.width*scale)),height=Math.Max(1,(int)(texture.height*scale));
  var rt=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
  var previous=RenderTexture.active;Texture2D result=null;
  try{Graphics.Blit(texture,rt);RenderTexture.active=rt;result=new Texture2D(width,height,TextureFormat.RGBA32,true,false);result.ReadPixels(new Rect(0,0,width,height),0,0,false);return result;}
  catch{if(result!=null)UnityEngine.Object.Destroy(result);throw;}
  finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
 }
 SurfaceAsset NativeAtlasSurface(SurfaceAsset source,int style){
  var key=(source,style);if(nativePavingSurfaces.TryGetValue(key,out var existing))return existing;
  if(nativeAtlasFailed.Contains(key))throw new InvalidOperationException("Native atlas preparation previously failed");
  if(nativePavingSurfaces.Count>=8)throw new InvalidOperationException("Native atlas cache limit reached");
  if(nativeAtlasJob==null){nativeAtlasKey=key;nativeAtlasJob=BuildNativeAtlas(source,style).GetEnumerator();}
  return null;
 }
 IEnumerable<SurfaceAsset> BuildNativeAtlas(SurfaceAsset source,int style){
  Texture2D atlas=null,tile=null;TextureAsset sourceTexture=null;bool loaded=false;
  try{
{
    source.LoadProperties(false);loaded=true;
    if(source.GetTemplateMaterial()?.shader?.name!="BH/NetCompositionMeshLitShader"||!source.TryGetTexture("_BaseColorMap",out sourceTexture))throw new InvalidOperationException("Unsupported native pavement material");
    atlas=ReadAtlas(sourceTexture.Load());
    source.UnloadProperties();loaded=false;
   }
   yield return null;
   Color32[] pixels,pattern;int[] columns,rows;int first=(int)Math.Ceiling(NativePavingAtlasPolicy.Min*atlas.width);
{
    tile=ReadAtlas(PavingMaterial(style).GetTexture("_BaseColorMap"),2048);
    pixels=atlas.GetPixels32();pattern=tile.GetPixels32();
    columns=NativePavingAtlasPolicy.Columns(atlas.width,tile.width);
    rows=NativePavingAtlasPolicy.Rows(atlas.height,tile.height);
   }
   yield return null;
   int row=0;
   while(row<atlas.height){
{
     var timer=System.Diagnostics.Stopwatch.StartNew();
     
     do{
      int offset=row*atlas.width,patternRow=rows[row]*tile.width;
      for(int x=first;x<atlas.width;x++){var color=pattern[patternRow+columns[x]];color.a=pixels[offset+x].a;pixels[offset+x]=color;}
      row++;
     }while(row<atlas.height&&timer.Elapsed.TotalMilliseconds<1);
    }
    yield return null;
   }
{
    atlas.name="RSIK native pavement atlas "+style;atlas.wrapMode=TextureWrapMode.Repeat;atlas.SetPixels32(pixels);atlas.Apply(true,false);
   }
   // Release large managed staging buffers before asset registration.
   pixels=null;pattern=null;columns=null;rows=null;
   yield return null;
   SurfaceAsset surface;
{
    var texture=AssetDatabase.user.AddAsset<TextureAsset,Texture>(AssetDataPath.Create("RSIKRuntime",Guid.NewGuid().ToString("N")),atlas);
    atlas=null;nativePavingPins.Add(texture);
    surface=AssetDatabase.user.AddAsset<SurfaceAsset>(AssetDataPath.Create("RSIKRuntime",Guid.NewGuid().ToString("N")));
    source.LoadProperties(false);loaded=true;
    surface.CopyFrom(source);surface.UpdateTexture("_BaseColorMap",texture);
    source.UnloadProperties();loaded=false;
   }
   yield return surface;
  }finally{sourceTexture?.Unload();if(loaded)source.UnloadProperties();if(atlas!=null)UnityEngine.Object.Destroy(atlas);if(tile!=null)UnityEngine.Object.Destroy(tile);}
 }
 void AdvanceNativeAtlas(){
  if(nativeAtlasJob==null)return;
  try{
   if(nativeAtlasJob.MoveNext()){
    if(nativeAtlasJob.Current==null)return;
    nativePavingSurfaces[nativeAtlasKey]=nativeAtlasJob.Current;
   }
  }catch(Exception ){nativeAtlasFailed.Add(nativeAtlasKey);}
  nativeAtlasJob.Dispose();nativeAtlasJob=null;
 }
 Entity NativePavingPiece(Entity original,int style,Entity road){
  var prefabs=World.GetExistingSystemManaged<PrefabSystem>();var prefab=prefabs.GetPrefab<RenderPrefab>(original);var key=(prefab,style);
  if(!nativePavingVariants.TryGetValue(key,out var variant)){
   if(nativePavingRejected.Contains(prefab))return Entity.Null;
   variant=new NativePavingVariant();
   try{
    if(!nativePavingValidated.Contains(prefab)){
{
      try{
       var source=ReadCutSource(original);var top=NativeSurfacePolicy.Pavement(source.P,source.I,out _);
       if(source.Materials.Length!=1||!NativePavingAtlasPolicy.Validate(source.UV,source.I,top))throw new InvalidOperationException("Atlas overlaps protected faces or unsupported mesh");
       nativePavingValidated.Add(prefab);
      }catch{nativePavingRejected.Add(prefab);throw;}
     }
    }
    var surface=NativeAtlasSurface(prefab.GetSurfaceAsset(0),style);
    if(surface==null){nativePavingWaiting.Add(road);return Entity.Null;}
    variant.Deadline=UnityEngine.Time.realtimeSinceStartup+30;
    // Keep the net-piece type so native initialization supplies the raw mesh
    // buffers. No road section or traffic lane references this render variant.
    // Unity Instantiate invokes OnEnable with shared ComponentBase references,
    // which also reassigns the original components' prefab owner. Native Clone
    // creates independent components through AddComponentFrom instead.
    var copy=(RenderPrefab)prefab.Clone("RSIK Native Pavement "+Guid.NewGuid().ToString("N"));copy.asset=null;
    copy.Remove<ObsoleteIdentifiers>();copy.Remove<UIObject>();
    copy.surfaceAssets=new[]{surface};
    if(!prefabs.AddPrefab(copy)){UnityEngine.Object.Destroy(copy);throw new InvalidOperationException("Native pavement prefab registration failed");}
    variant.Prefab=copy;
   }catch(Exception ){variant.Failed=true;}
  }
  nativePavingVariants[key]=variant;
  if(variant.Failed)return Entity.Null;
  if(!prefabs.TryGetEntity(variant.Prefab,out var entity)||!EntityManager.HasComponent<MeshData>(entity)||!EntityManager.HasBuffer<MeshMaterial>(entity)||!EntityManager.HasBuffer<MeshVertex>(entity)||!EntityManager.HasBuffer<MeshIndex>(entity)||EntityManager.HasComponent<Created>(entity)){
   if(UnityEngine.Time.realtimeSinceStartup>variant.Deadline){variant.Failed=true;return Entity.Null;}
   nativePavingWaiting.Add(road);return Entity.Null;
  }
  return entity;
 }
 Entity NativePavingComposition(Entity cp,int left,int right,bool reverse,Entity road,NativePavingBinding binding,bool prepare=false,bool requireSides=false){
  if(!EntityManager.HasComponent<NetCompositionMeshRef>(cp)){if(requireSides)throw new InvalidOperationException("Native edge mesh unavailable");return cp;}
  var reference=EntityManager.GetComponentData<NetCompositionMeshRef>(cp);var source=reference.m_Mesh;
  if(!EntityManager.HasBuffer<NetCompositionPiece>(source)){if(requireSides)throw new InvalidOperationException("Native edge pieces unavailable");return cp;}
  var parts=new List<NetCompositionPiece>();bool changed=false;int covered=0;
  foreach(var part in EntityManager.GetBuffer<NetCompositionPiece>(source,true))parts.Add(part);
  for(int i=0;i<parts.Count;i++){
   var part=parts[i];if(!NativePavingAtlasPolicy.Candidate(PieceName(part.m_Piece))||(part.m_SectionFlags&(NetSectionFlags.Median|NetSectionFlags.Hidden|NetSectionFlags.HiddenSurface|NetSectionFlags.Underground|NetSectionFlags.Overhead))!=0||(part.m_PieceFlags&(NetPieceFlags.Side|NetPieceFlags.Bottom))!=0)continue;
   bool positive=part.m_Offset.x>0;if(reference.m_Rotate^reverse)positive=!positive;
   int style=positive?right:left;if(style==0)continue;
   if(Math.Abs(part.m_Offset.x)<part.m_Size.x*.5f&&left!=right)throw new InvalidOperationException("Shared center piece needs two different materials");
   var replacement=NativePavingPiece(part.m_Piece,style,road);if(replacement==Entity.Null)throw new InvalidOperationException("Native pavement variant not ready");
   part.m_Piece=replacement;parts[i]=part;changed=true;covered|=positive?2:1;
  }
  if(requireSides&&((left!=0&&(covered&1)==0)||(right!=0&&(covered&2)==0)))throw new InvalidOperationException("Selected sidewalk not covered by native pieces");
  if(!changed||prepare)return cp;
  var mesh=EntityManager.CreateEntity();binding.Owned.Add(mesh);
  var data=EntityManager.GetComponentData<NetCompositionMeshData>(source);data.m_Hash=int.MinValue+mesh.Index;
  EntityManager.AddComponentData(mesh,data);var pieces=EntityManager.AddBuffer<NetCompositionPiece>(mesh);foreach(var p in parts)pieces.Add(p);
  var materials=EntityManager.AddBuffer<MeshMaterial>(mesh);var unique=new HashSet<int>();
  foreach(var p in parts)foreach(var m in EntityManager.GetBuffer<MeshMaterial>(p.m_Piece,true))if(unique.Add(m.m_MaterialIndex))materials.Add(new MeshMaterial{m_MaterialIndex=m.m_MaterialIndex});
  EntityManager.AddBuffer<BatchGroup>(mesh);EntityManager.AddComponent<Created>(mesh);EntityManager.AddComponent<Updated>(mesh);
  var clone=EntityManager.Instantiate(cp);binding.Owned.Add(clone);
  EntityManager.RemoveComponent<PrefabData>(clone);EntityManager.RemoveComponent<LoadedIndex>(clone);EntityManager.RemoveComponent<Created>(clone);EntityManager.RemoveComponent<Updated>(clone);
  reference.m_Mesh=mesh;EntityManager.SetComponentData(clone,reference);return clone;
 }
 bool ApplyNativePaving(Entity road){{
  if(!IsRoad(road)||!S.Paving||!EntityManager.HasBuffer<PavingChoice>(road)||EntityManager.HasBuffer<SideRoadChoice>(road)&&EntityManager.GetBuffer<SideRoadChoice>(road,true).Length>0)return false;
  var choices=new List<PavingChoice>();foreach(var c in EntityManager.GetBuffer<PavingChoice>(road,true))choices.Add(c);
  int left=0,right=0,leftRegions=0,rightRegions=0;foreach(var r in PavingRegions(road)){
   if(r.Kind==1){if(r.Min+r.Max<0)leftRegions++;else rightRegions++;}
   int style=PavingSelection(road,r,choices);if(style==0)continue;if(r.Kind!=1||style<1||style>PavingPolicy.StyleCount)return false;
   if(r.Min+r.Max<0){if(left!=0&&left!=style)return false;left=style;}else{if(right!=0&&right!=style)return false;right=style;}
  }
  if(left==0&&right==0)return false;
  if(left!=0&&leftRegions>1||right!=0&&rightRegions>1)return false;
  var binding=new NativePavingBinding{Original=EntityManager.GetComponentData<Composition>(road)};
  try{
   var cp=binding.Original;
   NativePavingComposition(cp.m_Edge,left,right,false,road,binding,true,true);
   NativePavingComposition(cp.m_StartNode,left,right,true,road,binding,true);
   NativePavingComposition(cp.m_EndNode,left,right,false,road,binding,true);
   cp.m_Edge=NativePavingComposition(cp.m_Edge,left,right,false,road,binding,false,true);
   cp.m_StartNode=NativePavingComposition(cp.m_StartNode,left,right,true,road,binding);
   cp.m_EndNode=NativePavingComposition(cp.m_EndNode,left,right,false,road,binding);
   if(binding.Owned.Count==0)return false;
   binding.Styled=cp;nativePavingBindings.Add(road,binding);EntityManager.SetComponentData(road,cp);DirtyBatch(road);
   var ends=EntityManager.GetComponentData<Edge>(road);DirtyBatch(ends.m_Start);DirtyBatch(ends.m_End);
   EnqueuePaving(ends.m_Start);EnqueuePaving(ends.m_End);
return true;
  }catch(Exception ){RetireNativePaving(binding);return false;}
 }}
 Composition OriginalPavingComposition(Entity road,Composition cp){
  if(nativePavingBindings.TryGetValue(road,out var b)){
   if(cp.m_Edge==b.Styled.m_Edge)cp.m_Edge=b.Original.m_Edge;
   if(cp.m_StartNode==b.Styled.m_StartNode)cp.m_StartNode=b.Original.m_StartNode;
   if(cp.m_EndNode==b.Styled.m_EndNode)cp.m_EndNode=b.Original.m_EndNode;
  }return cp;
 }
 void RetireNativePaving(NativePavingBinding b){foreach(var e in b.Owned)if(EntityManager.Exists(e)){if(!EntityManager.HasComponent<Temp>(e))EntityManager.AddComponent<Temp>(e);if(!EntityManager.HasComponent<Deleted>(e))EntityManager.AddComponent<Deleted>(e);}}
 void ReleaseNativePaving(Entity road){
  if(!nativePavingBindings.TryGetValue(road,out var b))return;
  if(EntityManager.HasComponent<Composition>(road)){EntityManager.SetComponentData(road,OriginalPavingComposition(road,EntityManager.GetComponentData<Composition>(road)));DirtyBatch(road);}
  nativePavingBindings.Remove(road);RetireNativePaving(b);
  if(EntityManager.HasComponent<Edge>(road)){var ends=EntityManager.GetComponentData<Edge>(road);DirtyBatch(ends.m_Start);DirtyBatch(ends.m_End);EnqueuePaving(ends.m_Start);EnqueuePaving(ends.m_End);}
 }
 void RetryNativePaving(){
  AdvanceNativeAtlas();
  if(nativeAtlasJob!=null||nativePavingWaiting.Count==0||UnityEngine.Time.realtimeSinceStartup<nativePavingRetry)return;
  nativePavingRetry=UnityEngine.Time.realtimeSinceStartup+1;
  var prefabs=World.GetExistingSystemManaged<PrefabSystem>();
  int stamp=nativePavingSurfaces.Count+nativeAtlasFailed.Count+nativePavingVariants.Count;
  foreach(var variant in nativePavingVariants.Values){
   if(variant.Failed||UnityEngine.Time.realtimeSinceStartup>variant.Deadline)stamp++;
   if(variant.Prefab!=null&&prefabs.TryGetEntity(variant.Prefab,out var entity)&&EntityManager.HasComponent<MeshData>(entity)&&EntityManager.HasBuffer<MeshMaterial>(entity)&&EntityManager.HasBuffer<MeshVertex>(entity)&&EntityManager.HasBuffer<MeshIndex>(entity)&&!EntityManager.HasComponent<Created>(entity))stamp++;
  }
  // An unchanged readiness state cannot improve by rebuilding all waiting roads.
  if(stamp==nativePavingRetryStamp)return;
  nativePavingRetryStamp=stamp;
  var waiting=nativePavingWaiting.ToArray();nativePavingWaiting.Clear();foreach(var road in waiting)if(IsRoad(road))EnqueuePaving(road);
 }
 void SuspendNativePaving(){
  foreach(var pair in nativePavingBindings){
   if(EntityManager.HasComponent<Composition>(pair.Key))EntityManager.SetComponentData(pair.Key,OriginalPavingComposition(pair.Key,EntityManager.GetComponentData<Composition>(pair.Key)));
   foreach(var e in pair.Value.Owned)if(EntityManager.Exists(e)&&!EntityManager.HasComponent<Temp>(e)){EntityManager.AddComponent<Temp>(e);saveTemps.Add(e);}
  }
  // Registered prefabs MUST retain PrefabData: PrefabSystem.Serialize queries
  // its enabled bit for every registered prefab. BeginPrefabSerialization
  // disables unreferenced prefabs after the temporary compositions are removed.
 }
 void ResumeNativePaving(){
  foreach(var pair in nativePavingBindings)if(EntityManager.HasComponent<Composition>(pair.Key)){
   var cp=EntityManager.GetComponentData<Composition>(pair.Key);var b=pair.Value;
   if(cp.m_Edge==b.Original.m_Edge)cp.m_Edge=b.Styled.m_Edge;if(cp.m_StartNode==b.Original.m_StartNode)cp.m_StartNode=b.Styled.m_StartNode;if(cp.m_EndNode==b.Original.m_EndNode)cp.m_EndNode=b.Styled.m_EndNode;
   EntityManager.SetComponentData(pair.Key,cp);DirtyBatch(pair.Key);
  }
 }
 void ClearNativePaving(){
  nativeAtlasJob?.Dispose();nativeAtlasJob=null;nativePavingRetryStamp=-1;
  foreach(var road in nativePavingBindings.Keys.ToArray())ReleaseNativePaving(road);
  nativePavingWaiting.Clear();
  // Keep registered render variants cached for this world. They are reused on
  // reload; removing registered prefab indices would invalidate render aliases.
 }
}}
