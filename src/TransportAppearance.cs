using System;using System.Collections.Generic;using Colossal.IO.AssetDatabase;using Game.Prefabs;using Unity.Entities;using Unity.Collections;using UnityEngine;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
sealed class TransportMaterial {internal Material Material;internal Texture Original;}
readonly List<TransportMaterial> transportMaterials=new List<TransportMaterial>();Texture2D transportClear;
bool transportScanned,transportSettingsSeen,lastTransportText;float transportScanAt;int transportBatchCount=-1;
void UpdateTransportAppearance(){
bool running=S.Enabled&&ready&&!restoring&&!restoreRequested;
if(running&&transportSettingsSeen&&lastTransportText!=S.HideTransportText){using(var edges=roads.ToEntityArray(Allocator.Temp))foreach(var edge in edges){if(EntityManager.HasComponent<ApproachChoice>(edge)&&EntityManager.GetComponentData<ApproachChoice>(edge).Enabled)approachPending.Add(edge);if(active){EnqueueMarkings(edge);var ends=EntityManager.GetComponentData<Game.Net.Edge>(edge);EnqueueMarkings(ends.m_Start);EnqueueMarkings(ends.m_End);}}}
lastTransportText=S.HideTransportText;transportSettingsSeen=true;
if(!running||!S.HideTransportColor){if(transportScanned||transportMaterials.Count>0)RestoreTransportMaterials();return;}
var batches=World.GetExistingSystemManaged<Game.Rendering.ManagedBatchSystem>();if(batches==null)return;
float now=UnityEngine.Time.realtimeSinceStartup;
if(transportScanned&&now<transportScanAt)return;
transportScanAt=now+1;
// Material keys retain the asset identity; loaded batch materials are separate from SurfaceAsset.Load().
if(transportScanned&&transportBatchCount==batches.materialCount&&now<transportFullScanAt)return;
transportFullScanAt=now+5;transportBatchCount=batches.materialCount;transportScanned=true;
int before=transportMaterials.Count;int property=Shader.PropertyToID("_BaseColorMap");
transportMaterials.RemoveAll(item=>item.Material==null);
foreach(var entry in batches.materials){
 if(!entry.Key.textures.TryGetValue(property,out var source))continue;
 string name=source is TextureAsset asset?asset.name:source is Texture texture?texture.name:"";
 if(name!="BusLane_BaseColor"&&!name.StartsWith("BusLane_BaseColor_",StringComparison.Ordinal))continue;
 var material=entry.Value;if(material==null||!material.HasProperty(property))continue;
 if(transportMaterials.Exists(item=>item.Material==material))continue;
 if(transportClear==null){transportClear=new Texture2D(2,2,TextureFormat.RGBA32,false){name="RSIK transparent transport overlay",hideFlags=HideFlags.HideAndDontSave};transportClear.SetPixels(new[]{Color.clear,Color.clear,Color.clear,Color.clear});transportClear.Apply();}
 transportMaterials.Add(new TransportMaterial{Material=material,Original=material.GetTexture(property)});
 material.SetTexture(property,transportClear);
}
}
float transportFullScanAt;
void RestoreTransportMaterials(){
foreach(var item in transportMaterials)if(item.Material!=null&&item.Material.GetTexture("_BaseColorMap")==transportClear)item.Material.SetTexture("_BaseColorMap",item.Original);
transportMaterials.Clear();if(transportClear!=null)UnityEngine.Object.Destroy(transportClear);transportClear=null;transportScanned=false;transportBatchCount=-1;transportScanAt=transportFullScanAt=0;
}
void HideTransportLabels(Entity owner,Record record,List<Entity> lanes){
if(!S.HideTransportText)return;var queue=new WorkQueue<Entity>();var seen=new HashSet<Entity>();queue.Enqueue(owner);foreach(var lane in lanes)queue.Enqueue(lane);
while(queue.Count>0){var entity=queue.Dequeue();if(!Live(entity)||!seen.Add(entity))continue;
if(EntityManager.HasBuffer<Game.Objects.SubObject>(entity)){var children=EntityManager.GetBuffer<Game.Objects.SubObject>(entity,true);foreach(var child in children)queue.Enqueue(child.m_SubObject);}
if(MarkingPolicy.TransportText(assets.Name(Prefab(entity))))Hide(entity,record);
}
}
bool SmallRoadJunction(Entity node){
if(!SafeNode(node))return false;var roadsSeen=new HashSet<Entity>();var counts=new List<int>();
foreach(var connection in EntityManager.GetBuffer<Game.Net.ConnectedEdge>(node,true)){
var edge=connection.m_Edge;if(!IsRoad(edge)||!roadsSeen.Add(edge))continue;int count=0;
if(EntityManager.HasComponent<Game.Net.Composition>(edge)){var cp=EntityManager.GetComponentData<Game.Net.Composition>(edge).m_Edge;
if(EntityManager.HasBuffer<NetCompositionLane>(cp))foreach(var lane in EntityManager.GetBuffer<NetCompositionLane>(cp,true))
if((lane.m_Flags&LaneFlags.Road)!=0&&(lane.m_Flags&(LaneFlags.Master|LaneFlags.Secondary|LaneFlags.Parking|LaneFlags.BicyclesOnly|LaneFlags.Virtual))==0)count++;}
counts.Add(count);
}return MarkingPolicy.SmallJunction(counts);
}
}}

