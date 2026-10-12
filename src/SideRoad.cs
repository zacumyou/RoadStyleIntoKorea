using System;
using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Colossal.Mathematics;
using Game.Net;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Colossal.IO.AssetDatabase;
using Unity.Collections;
using UnityEngine.Rendering;

namespace RoadStyleIntoKorea
{
    public partial class StyleSystem
    {
        internal int SideRoadWidth=6; internal void AdjustSideRoadWidth(int delta,bool cycle=false){SideRoadWidth=cycle?(SideRoadWidth>=12?3:SideRoadWidth+1):math.clamp(SideRoadWidth+delta,3,12);InvalidateToolPreview();}
        readonly Dictionary<Entity,Material> sideRoadMaterials=new Dictionary<Entity,Material>();
        readonly Dictionary<Material,SideRoadUV> sideRoadUVs=new Dictionary<Material,SideRoadUV>();
        readonly Dictionary<Entity,List<SideRoadOpening>> sideRoadWork=new Dictionary<Entity,List<SideRoadOpening>>();
        Entity sideRoadHoverRoad;
        Geometry.Arc sideRoadHoverArc;
        Geometry.Arc SideRoadHoverArc(Entity road){
            var curve=CurveOf(road);
            if(road!=sideRoadHoverRoad||sideRoadHoverArc==null||!Geometry.SameCurve(sideRoadHoverArc.Curve,curve)){
                sideRoadHoverRoad=road;sideRoadHoverArc=new Geometry.Arc(curve);
            }
            return sideRoadHoverArc;
        }

        bool SideRoadAt(Entity road,float3 hit,out SideRoadChoice choice,out float3 point)
        {
            choice=default;point=default;
            if(!IsRoad(road)||!EntityManager.HasComponent<Curve>(road)||!EntityManager.HasComponent<Composition>(road))return false;
            var composition=EntityManager.GetComponentData<Composition>(road).m_Edge;
            if(!EntityManager.HasComponent<NetCompositionData>(composition))return false;
            float half=EntityManager.GetComponentData<NetCompositionData>(composition).m_Width*.5f;
            var arc=SideRoadHoverArc(road);
            if(arc.Length<SideRoadWidth+10)return false;
            float t=arc.Nearest(hit);var center=Geometry.Position(arc.Curve,t);
            var left=Geometry.Left(Geometry.Tangent(arc.Curve,t));
            float lateral=math.dot(hit-center,left),along=math.dot(hit-center,Geometry.Tangent(arc.Curve,t));
            if(math.abs(math.abs(lateral)-half)>3f||math.abs(along)>3f||math.abs(hit.y-center.y)>2f)return false;
            float distance=arc.Distance(t);
            if(distance<SideRoadWidth*.5f+6||arc.Length-distance<SideRoadWidth*.5f+6)return false;
            choice=new SideRoadChoice{T=t,Width=SideRoadWidth,Side=lateral>=0?1:-1};
            point=center+left*choice.Side*half;
            return true;
        }
        internal bool SideRoadTool(Entity road,float3 hit,Game.Rendering.OverlayRenderSystem.Buffer draw,bool apply,bool reset)
        {
            if(!S.Enabled||!SideRoadAt(road,hit,out var choice,out var point)){
                return false;
            }
            var arc=SideRoadHoverArc(road);float distance=arc.Distance(choice.T);
            float lo=arc.T(distance-choice.Width*.5f),hi=arc.T(distance+choice.Width*.5f);
            var side=Geometry.Left(Geometry.Tangent(arc.Curve,choice.T))*choice.Side;
            var preview=Geometry.Offset(Geometry.Cut(arc.Curve,lo,hi),choice.Side*math.distance(point,Geometry.Position(arc.Curve,choice.T)));
            draw.DrawCurve(new Color(.12f,.72f,1,.65f),preview,2.8f);
            draw.DrawCircle(new Color(.1f,.85f,1,.95f),point+new float3(0,.05f,0),.35f);
            if(!apply&&!reset)return true;
            if(apply){
                try{if(SideRoadMaterial(road)==null){Status="도로 아스팔트 재질을 찾지 못했습니다 / Native road asphalt unavailable";return true;}}
                catch(Exception ){Status="도로 아스팔트 준비 실패 / Asphalt material failed";return true;}
            }
            localStyleCommands.Enqueue(()=>{
                if(!IsRoad(road))return;
                if(!EntityManager.HasBuffer<SideRoadChoice>(road)){if(reset)return;EntityManager.AddBuffer<SideRoadChoice>(road);}
                var choices=EntityManager.GetBuffer<SideRoadChoice>(road);
                int found=-1;
                for(int i=0;i<choices.Length;i++)if(choices[i].Side==choice.Side&&math.abs(arc.Distance(choices[i].T)-distance)<math.max(choices[i].Width,choice.Width)*.65f){found=i;break;}
                if(reset){if(found<0)return;choices.RemoveAt(found);ToolResetHandled=true;}
                else if(found>=0)choices[found]=choice;else choices.Add(choice);
                EnqueueRoad(road);EnqueueCurb(road);EnqueuePaving(road);targetedActive=true;
                Status=reset?"샛길 입구 제거 / Side road entrance removed":"샛길 입구 적용 / Side road entrance applied";
            });targetedActive=true;
            return true;
        }
        List<float2> SideRoadIntervals(Entity road,Bezier4x3 curve,int side)
        {
            var result=new List<float2>();
            if(!EntityManager.HasBuffer<SideRoadChoice>(road))return result;
            var openings=SideRoadOpenings(road,false);if(openings.Count==0)return result;
            var paintArc=new Geometry.Arc(curve);
            foreach(var opening in openings){
                if(opening.Side!=side)continue;
                var roadArc=opening.Road;float t=roadArc.T(opening.Distance);
                float3 center=Geometry.Position(roadArc.Curve,t);
                float nearest=paintArc.Nearest(center);
                float3 at=Geometry.Position(paintArc.Curve,nearest);
                if(math.abs(math.dot(at-center,Geometry.Tangent(roadArc.Curve,t)))>2)continue;
                float lateral=math.dot(at-center,Geometry.Left(Geometry.Tangent(roadArc.Curve,t)))*side;
                if(lateral<=0)continue; // Never cut an unrelated curve across the road.
                float depth=lateral-opening.Inner;
                // Use the same world-space cut boundary as the apron and curved curb.
                // Arc distances on an offset/curved road are not interchangeable.
                float left=paintArc.Distance(paintArc.Nearest(opening.CutBoundaryPlan(-1,math.clamp(depth,0,opening.Outer-opening.Inner))));
                float right=paintArc.Distance(paintArc.Nearest(opening.CutBoundaryPlan(1,math.clamp(depth,0,opening.Outer-opening.Inner))));
                result.Add(new float2(math.min(left,right),math.max(left,right)));
            }
            DrivewayPolicy.Merge(result);return result;
        }
        int SideRoadRegionSide(Geometry.Arc roadArc,PavingRegion region)
        {
            // Composition offsets are measured from m_Left to m_Right, whose
            // sign is not the edge curve's left/right sign used by the preview.
            var sample=Geometry.Position(region.First,.5f);
            float t=roadArc.Nearest(sample);
            var center=Geometry.Position(roadArc.Curve,t);
            return math.dot(sample-center,Geometry.Left(Geometry.Tangent(roadArc.Curve,t)))>=0?1:-1;
        }
        Material SideRoadMaterial(Entity road)
        {
            var cp=EntityManager.GetComponentData<Composition>(road).m_Edge;
            if(!EntityManager.HasBuffer<NetCompositionPiece>(cp))return null;
            foreach(var part in EntityManager.GetBuffer<NetCompositionPiece>(cp,true)){
                string name=PieceName(part.m_Piece);
                // Stop bays contain a vehicle lane too, but their first surface
                // and UVs include sidewalk/gutter regions of the road atlas.
                if(!SideRoadUV.AsphaltSource(name))continue;
                if((part.m_SectionFlags&(NetSectionFlags.Hidden|NetSectionFlags.HiddenSurface|NetSectionFlags.Underground))!=0||
                    (part.m_PieceFlags&(NetPieceFlags.Side|NetPieceFlags.Bottom|NetPieceFlags.HasRoadLanes))!=NetPieceFlags.HasRoadLanes)continue;
                if(sideRoadMaterials.TryGetValue(part.m_Piece,out var cached))return cached;
                var surface=PavingSurfaceAsset(part.m_Piece);var shader=Shader.Find("HDRP/Lit");
                if(surface==null||shader==null||!SideRoadNativeUV(part.m_Piece,out var nativeUV))continue;
                Material material=null;
                var acquired=new List<TextureAsset>();
                bool loaded=false;
                try{
                    surface.LoadProperties(false);loaded=true;
                    // NetCompositionMeshLitShader expects the game's packed net
                    // vertices. Our world mesh must use an ordinary mesh shader.
                    material=new Material(shader){name="RSIK road asphalt",hideFlags=HideFlags.HideAndDontSave};
                    foreach(var key in new[]{"_BaseColorMap","_NormalMap"})if(surface.TryGetTexture(key,out var asset)&&asset!=null){material.SetTexture(key,asset.Load());acquired.Add(asset);}
                    if(material.GetTexture("_BaseColorMap")==null){UnityEngine.Object.Destroy(material);foreach(var a in acquired)a.Unload();continue;}
                    material.SetColor("_BaseColor",surface.TryGetColorProperty("_BaseColor",out var color)?color:Color.white);
                    material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.08f);
                    material.SetFloat("_SmoothnessRemapMin",.04f);material.SetFloat("_SmoothnessRemapMax",.12f);
                    material.SetFloat("_NormalScale",.6f);
                    HDMaterial.ValidateMaterial(material);
                    pavingTextures.AddRange(acquired);sideRoadMaterials.Add(part.m_Piece,material);
                    sideRoadUVs.Add(material,nativeUV);
                    return material;
                }catch(Exception ){foreach(var a in acquired)a.Unload();if(material!=null)UnityEngine.Object.Destroy(material);}
                finally{if(loaded)surface.Unload();}
            }
            return null;
        }
        bool SideRoadNativeUV(Entity piece,out SideRoadUV result)
        {
            result=default;
            if(!EntityManager.HasBuffer<MeshVertex>(piece)||!EntityManager.HasBuffer<MeshUV0>(piece)||!EntityManager.HasBuffer<MeshIndex>(piece))return SideRoadAssetUV(piece,out result);
            var vertices=EntityManager.GetBuffer<MeshVertex>(piece,true);var uv=EntityManager.GetBuffer<MeshUV0>(piece,true);
            var indices=EntityManager.GetBuffer<MeshIndex>(piece,true);int first=0,count=indices.Length;
            if(EntityManager.HasBuffer<MeshMaterial>(piece)){
                var materials=EntityManager.GetBuffer<MeshMaterial>(piece,true);
                if(materials.Length>0){first=materials[0].m_StartIndex;count=materials[0].m_IndexCount;}
            }
            float best=float.MinValue;
            for(int i=first;i+2<math.min(first+count,indices.Length);i+=3){
                int ia=indices[i].m_Index,ib=indices[i+1].m_Index,ic=indices[i+2].m_Index;
                if(ia>=vertices.Length||ib>=vertices.Length||ic>=vertices.Length||ia>=uv.Length||ib>=uv.Length||ic>=uv.Length)continue;
                var a=vertices[ia].m_Vertex;var b=vertices[ib].m_Vertex;var c=vertices[ic].m_Vertex;
                SelectRoadUV(a,b,c,uv[ia].m_Uv,uv[ib].m_Uv,uv[ic].m_Uv,ref best,ref result);
            }
            return best>float.MinValue;
        }
        static void SelectRoadUV(float3 a,float3 b,float3 c,float2 ua,float2 ub,float2 uc,ref float best,ref SideRoadUV result)
        {
            var n=math.normalizesafe(math.cross(b-a,c-a));if(math.abs(n.y)<.98f)return;
            float2 ab=(b-a).xz,ac=(c-a).xz;float determinant=ab.x*ac.y-ab.y*ac.x;
            float min=math.min(a.x,math.min(b.x,c.x)),max=math.max(a.x,math.max(b.x,c.x));
            if(math.abs(determinant)<.01f||max-min<.5f||min>0||max<0)return;
            float score=max-min-math.abs((a.y+b.y+c.y)/3)*.01f;if(score<=best)return;
            if(!math.all(math.isfinite(ua+ub+uc)))return;
            result=new SideRoadUV{Origin=ua,Anchor=a.xz,DX=((ub-ua)*ac.y-(uc-ua)*ab.y)/determinant,
                DZ=((uc-ua)*ab.x-(ub-ua)*ac.x)/determinant,MinX=min+.05f,MaxX=max-.05f};best=score;
        }
        bool SideRoadAssetUV(Entity piece,out SideRoadUV result)
        {
            result=default;var prefabs=World.GetExistingSystemManaged<PrefabSystem>();
            if(!prefabs.TryGetPrefab<PrefabBase>(piece,out var prefab)||!(prefab is RenderPrefab render)||render.geometryAsset==null)return false;
            var data=new GeometryAsset.Data();var loading=new GeometryAsset.Loading();float best=float.MinValue;
            try{
                var asset=render.geometryAsset;
                GeometryAsset.LoadSync(asset.database.GetAsyncReadDescriptor(asset.id),GeometryAsset.Attribute.Position|GeometryAsset.Attribute.TexCoord0,ref data,ref loading);
                if(data.meshCount==0)return false;
                int vc=GeometryAsset.GetVertexCount(ref data,0),ic=GeometryAsset.GetIndicesCount(ref data,0);
                using(var p=new NativeArray<MeshVertex>(vc,Allocator.Temp))
                using(var uv=new NativeArray<MeshUV0>(vc,Allocator.Temp))
                using(var ix=new NativeArray<MeshIndex>(ic,Allocator.Temp)){
                    GeometryAsset.GetAttributeFormat(ref data,0,VertexAttribute.Position,out var pf,out int pd);
                    GeometryAsset.GetAttributeFormat(ref data,0,VertexAttribute.TexCoord0,out var uf,out int ud);
                    if(ud<2)return false;
                    MeshVertex.Unpack(GeometryAsset.GetAttributeData(ref data,0,VertexAttribute.Position),p,vc,pf,pd);
                    MeshUV0.Unpack(GeometryAsset.GetAttributeData(ref data,0,VertexAttribute.TexCoord0),uv,vc,uf,ud);
                    MeshIndex.Unpack(GeometryAsset.GetIndices(ref data,0),ix,ic,GeometryAsset.GetIndexFormat(ref data,0),0);
                    var range=data.subMeshInfos[data.meshOffsets[0].subMeshesOffset].ToSubMeshDescriptor();
                    for(int i=range.indexStart;i+2<math.min(ic,range.indexStart+range.indexCount);i+=3){
                        int a=ix[i].m_Index,b=ix[i+1].m_Index,c=ix[i+2].m_Index;
                        SelectRoadUV(p[a].m_Vertex,p[b].m_Vertex,p[c].m_Vertex,uv[a].m_Uv,uv[b].m_Uv,uv[c].m_Uv,ref best,ref result);
                    }
                }
                return best>float.MinValue;
            }catch(Exception ){return false;}
            finally{loading.Dispose();data.Dispose();}
        }
        List<SideRoadOpening> SideRoadOpenings(Entity road,bool heights=true)
        {
            if(inWorkItem&&sideRoadWork.TryGetValue(road,out var cached)){
                if(heights&&cached.Count>0&&!cached[0].HeightsPrepared)PrepareSideRoadHeights(road,cached);
                return cached;
            }
            var result=new List<SideRoadOpening>();
            if(!IsRoad(road)||!EntityManager.HasBuffer<SideRoadChoice>(road)||EntityManager.GetBuffer<SideRoadChoice>(road,true).Length==0||!EntityManager.HasComponent<EdgeGeometry>(road))return result;
            if(SideRoadMaterial(road)==null)return result;
            var shape=EntityManager.GetComponentData<EdgeGeometry>(road);var regions=PavingRegions(road);
            var roadArc=new Geometry.Arc(CurveOf(road));
            var cp=EntityManager.GetComponentData<Composition>(road).m_Edge;
            float surfaceHeight=EntityManager.GetComponentData<NetCompositionData>(cp).m_SurfaceHeight.min;
            foreach(var choice in EntityManager.GetBuffer<SideRoadChoice>(road,true)){
                if(choice.Width<=0)continue;
                float3 center=Geometry.Position(roadArc.Curve,choice.T),outward=Geometry.Left(Geometry.Tangent(roadArc.Curve,choice.T))*choice.Side;
                SideRoadOpening opening=null;
                foreach(var region in regions){
                    // RoadBuilder empty strips can also exist beside the centerline.
                    // Never bridge the carriageway when assembling an outer entrance.
                    if(!AutomaticPavingRegion(road,region)||SideRoadRegionSide(roadArc,region)!=choice.Side)continue;
                    bool positiveOffset=region.Min+region.Max>=0;
                    float inner=positiveOffset?region.PaintMin:region.PaintMax;
                    float outer=positiveOffset?region.Max:region.Min;
                    float topHeight=CurbHeight(region)+(region.Kind==2&&CurbSelected(road,region)?NativeCurbGeometry.Rise:0);
                    float best=float.MaxValue;
                    foreach(var segment in new[]{shape.m_Start,shape.m_End}){
                        var a=Between(segment.m_Left,segment.m_Right,.5f+inner/region.RoadWidth,topHeight);
                        var b=Between(segment.m_Left,segment.m_Right,.5f+outer/region.RoadWidth,topHeight);
                        var arc=new Geometry.Arc(a);float at=arc.Nearest(center);
                        var near=Geometry.Position(a,at);
                        float delta=math.abs(math.dot(near-center,Geometry.Tangent(roadArc.Curve,choice.T)));
                        if(delta>1.5f||delta>=best)continue;best=delta;
                        float innerOffset=math.dot(near-center,outward)-(region.Kind==1?.6f:0),outerOffset=math.dot(Geometry.Position(b,at)-center,outward);
                        if(outerOffset-innerOffset<.25f)continue;
                        if(opening==null)opening=new SideRoadOpening{Road=roadArc,Distance=roadArc.Distance(choice.T),Width=choice.Width,Side=choice.Side,CurbJoinDepth=region.Kind==1?.6f:0f,Inner=innerOffset,Outer=outerOffset,Top=near.y-center.y,Bottom=near.y-center.y-topHeight+surfaceHeight};
                        else{opening.Inner=math.min(opening.Inner,innerOffset);opening.Outer=math.max(opening.Outer,outerOffset);opening.Top=math.max(opening.Top,near.y-center.y);}
                    }
                }
                if(opening!=null){opening.CutSurface=nativeSideRoadCuts.TryGetValue(road,out var cut)&&EntityManager.GetComponentData<Composition>(road).m_Edge==cut.Composition;result.Add(opening);}
            }
            if(heights)PrepareSideRoadHeights(road,result);
            result.Sort((a,b)=>a.Distance.CompareTo(b.Distance));
            if(inWorkItem)sideRoadWork[road]=result;
            return result;
        }
        void PaintSideRoadAprons(Entity road,Record record)
        {{
            var openings=SideRoadOpenings(road);if(openings.Count==0)return;
            var material=SideRoadMaterial(road);var mapping=sideRoadUVs[material];
            foreach(var opening in openings){
                opening.Mesh(out var vertices,out var indices,out var uv);
                opening.Skirt(vertices,uv,out var wallVertices,out var wallIndices,out var wallUV);
                for(int i=0;i<uv.Count;i++)uv[i]=mapping.Map(uv[i]);
                for(int i=0;i<wallUV.Count;i++)wallUV[i]=mapping.Map(wallUV[i]);
                float lift=opening.CutSurface?0f:.018f;
                CreatePavingMesh(record,material,vertices,indices,uv,lift);
                CreatePavingMesh(record,material,wallVertices,wallIndices,wallUV,lift,true);
            }
            PaintSideRoadCurbRims(road,record);
        }}
        void DisposeSideRoadMaterials(){sideRoadCutSources.Clear();sideRoadHoverRoad=Entity.Null;sideRoadHoverArc=null;sideRoadWork.Clear();sideRoadUVs.Clear();foreach(var material in sideRoadMaterials.Values)UnityEngine.Object.Destroy(material);sideRoadMaterials.Clear();}
    }
}




