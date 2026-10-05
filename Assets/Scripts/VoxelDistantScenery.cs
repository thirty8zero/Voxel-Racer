using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelRacer
{
    /// <summary>Stable world tiles surround the camera in every direction. Only visible props
    /// are submitted, grouped by mesh/material, without thousands of scenery GameObjects.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class VoxelDistantScenery : MonoBehaviour
    {
        private const float TileSize = 32;
        private sealed class Prop
        {
            public Mesh mesh;
            public Material material;
            public Matrix4x4 matrix;
            public Bounds bounds;
            public float maximumDistance;
        }
        private readonly Dictionary<Vector2Int,List<Prop>> tiles = new();
        private readonly Dictionary<Vector2Int,Bounds> tileBounds = new();
        private readonly List<Vector2Int> expired = new();
        private readonly List<Vector3> roadSamples = new();
        private readonly Dictionary<(Mesh,Material),List<Matrix4x4>> batches = new();
        private readonly Matrix4x4[] drawBuffer = new Matrix4x4[1023];
        private readonly Plane[] planes = new Plane[6];
        private EndlessVoxelRoad road;
        private VoxelScenerySet lastSet;
        private Vector2Int lastTile = new(int.MinValue,int.MinValue);
        private float lastRadius;
        private int lastSeed, lastDensity, lastSetRevision;
        private VoxelDesertFoliageTuning lastFoliage;
        private int lastFoliageRevision;
        private MaterialPropertyBlock foliageProperties;
        public int CachedPropCount { get; private set; }
        public int VisiblePropCount { get; private set; }
        public int VisibleBatchCount { get; private set; }

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            var camera = Camera.main;
            if (camera == null) return;
            SubmitDraws(camera);
        }

        public void SubmitDraws(Camera camera)
        {
            Prepare(camera);
            foreach (var batch in batches)
            {
                for (int start=0;start<batch.Value.Count;start+=1023)
                {
                    int count=Mathf.Min(1023,batch.Value.Count-start);
                    batch.Value.CopyTo(start,drawBuffer,0,count);
                    var properties = lastFoliage != null && batch.Key.Item2 == lastFoliage.material ? foliageProperties : null;
                    if (SystemInfo.supportsInstancing && batch.Key.Item2.enableInstancing)
                        Graphics.DrawMeshInstanced(batch.Key.Item1,0,batch.Key.Item2,drawBuffer,count,properties,
                            ShadowCastingMode.Off,true,gameObject.layer,camera,LightProbeUsage.Off);
                    else
                        for(int i=0;i<count;i++) Graphics.DrawMesh(batch.Key.Item1,drawBuffer[i],batch.Key.Item2,
                            gameObject.layer,camera,0,properties,ShadowCastingMode.Off,true,null,LightProbeUsage.Off);
                }
            }
        }

        public float CoverageRadius(Camera camera, VoxelScenerySet set)
        {
            float distance=set.distantSceneryDistance>0?set.distantSceneryDistance:camera.farClipPlane;
            if(set.distantSceneryDistance<=0 && RenderSettings.fog && RenderSettings.fogMode==FogMode.Linear)
                distance=Mathf.Min(distance,RenderSettings.fogEndDistance);
            // Stay on the continuous ground sheet, including when its centre follows the player.
            return Mathf.Max(0,Mathf.Min(distance,camera.farClipPlane,road.groundWidth*.5f-TileSize));
        }

        /// <summary>Also usable by editor validation; does not submit any draw calls.</summary>
        public void Prepare(Camera camera)
        {
            road ??= GetComponent<EndlessVoxelRoad>();
            foreach(var batch in batches.Values) batch.Clear();
            VisiblePropCount=0;
            VisibleBatchCount=0;
            var set=road!=null && road.trackDefinition!=null?road.trackDefinition.scenerySet:null;
            if(set==null || !set.distantSceneryEnabled || set.distantPropsPerTile<=0)
            { tiles.Clear(); tileBounds.Clear(); lastSet=null; CachedPropCount=0; return; }
            float radius=CoverageRadius(camera,set);
            var center=new Vector2Int(Mathf.FloorToInt(camera.transform.position.x/TileSize),Mathf.FloorToInt(camera.transform.position.z/TileSize));
            bool settingsChanged=set!=lastSet || radius!=lastRadius || set.distantScenerySeed!=lastSeed || set.distantPropsPerTile!=lastDensity ||
                set.Revision!=lastSetRevision || set.groundCover != lastFoliage || (set.groundCover != null && set.groundCover.Revision != lastFoliageRevision);
            if(settingsChanged) { tiles.Clear(); tileBounds.Clear(); batches.Clear(); }
            if(settingsChanged || center!=lastTile)
            {
                roadSamples.Clear();
                // Sample the actual curved road, including future path and the finish-camera rear view.
                for(float d=road.SceneryTrackDistance-radius*2-64;d<=road.SceneryTrackDistance+radius*2+64;d+=4)
                    roadSamples.Add(road.Evaluate(d).position);
                int extent=Mathf.CeilToInt(radius/TileSize)+1;
                expired.Clear();
                foreach(var tile in tiles.Keys)
                    if(Mathf.Abs(tile.x-center.x)>extent || Mathf.Abs(tile.y-center.y)>extent) expired.Add(tile);
                foreach(var tile in expired) { tiles.Remove(tile); tileBounds.Remove(tile); }
                for(int x=-extent;x<=extent;x++) for(int z=-extent;z<=extent;z++)
                {
                    var key=center+new Vector2Int(x,z);
                    if(!tiles.ContainsKey(key)) tiles[key]=BuildTile(key,set);
                }
                lastSet=set; lastTile=center; lastRadius=radius; lastSeed=set.distantScenerySeed; lastDensity=set.distantPropsPerTile;
                lastSetRevision=set.Revision;
                lastFoliage=set.groundCover; lastFoliageRevision=lastFoliage!=null?lastFoliage.Revision:0;
                foliageProperties=lastFoliage!=null?lastFoliage.DrawProperties():null;
                CachedPropCount=0; foreach(var tile in tiles.Values) CachedPropCount+=tile.Count;
            }
            GeometryUtility.CalculateFrustumPlanes(camera,planes);
            foreach(var tile in tiles)
            {
                // Reject whole off-camera tiles before inspecting individual plant bounds.
                if(tile.Value.Count==0 || !GeometryUtility.TestPlanesAABB(planes,tileBounds[tile.Key]))continue;
                foreach(var prop in tile.Value)
                {
                    Vector3 delta=prop.bounds.center-camera.transform.position; delta.y=0;
                    float distance=prop.maximumDistance>0?Mathf.Min(radius,prop.maximumDistance):radius;
                    if(delta.sqrMagnitude>distance*distance || !GeometryUtility.TestPlanesAABB(planes,prop.bounds)) continue;
                    var key=(prop.mesh,prop.material);
                    if(!batches.TryGetValue(key,out var list)) { list=new List<Matrix4x4>(); batches.Add(key,list); }
                    list.Add(prop.matrix); VisiblePropCount++;
                }
            }
            foreach(var batch in batches.Values)if(batch.Count>0)VisibleBatchCount++;
        }

        private List<Prop> BuildTile(Vector2Int key,VoxelScenerySet set)
        {
            var result=new List<Prop>();
            var oldRandom=Random.state;
            try
            {
                Random.InitState(unchecked(key.x*73856093 ^ key.y*19349663 ^ set.distantScenerySeed));
                for(int i=0;i<set.distantPropsPerTile;i++)
                {
                    var entry=set.Choose(); if(entry==null) break;
                    float scale=VoxelScenerySet.ChooseScale(entry);
                    float footprint=entry.radius*scale+entry.spacing*.5f;
                    // Tile margins keep neighbouring tiles' footprints from overlapping.
                    if(footprint>=TileSize*.5f) continue;
                    Vector3 pos=new(key.x*TileSize+Random.Range(footprint,TileSize-footprint),0,
                        key.y*TileSize+Random.Range(footprint,TileSize-footprint));
                    float clearance=road.roadWidth*.5f+Mathf.Max(entry.maximumRoadDistance,entry.roadClearance)+footprint+2;
                    bool blocked=false;
                    foreach(var sample in roadSamples)
                    {
                        Vector3 delta=sample-pos; delta.y=0;
                        if(delta.sqrMagnitude<clearance*clearance) { blocked=true; break; }
                    }
                    if(blocked) continue;
                    if(Random.value>set.PlacementDensity(pos)) continue;
                    foreach(var other in result)
                        if(Vector3.Distance(other.bounds.center,pos)<footprint+other.bounds.extents.magnitude) { blocked=true; break; }
                    if(blocked) continue;
                    var matrix=Matrix4x4.TRS(pos,Quaternion.Euler(0,entry.randomRotation?Random.Range(0,360f):0,0),Vector3.one*scale);
                    foreach(var filter in entry.prefab.GetComponentsInChildren<MeshFilter>())
                    {
                        var renderer=filter.GetComponent<MeshRenderer>();
                        if(filter.sharedMesh==null || renderer==null || renderer.sharedMaterial==null) continue;
                        // Desert assets are one-submesh props; specialised interactive prefabs stay roadside.
                        if(filter.sharedMesh.subMeshCount!=1) continue;
                        Matrix4x4 model=matrix*entry.prefab.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix*
                            Matrix4x4.Scale(entry.prefab.transform.localScale);
                        var bounds=filter.sharedMesh.bounds;
                        Vector3 e=bounds.extents;
                        Vector3 a=model.MultiplyVector(new Vector3(e.x,0,0)), b=model.MultiplyVector(new Vector3(0,e.y,0)), c=model.MultiplyVector(new Vector3(0,0,e.z));
                        var extents=new Vector3(Mathf.Abs(a.x)+Mathf.Abs(b.x)+Mathf.Abs(c.x),Mathf.Abs(a.y)+Mathf.Abs(b.y)+Mathf.Abs(c.y),Mathf.Abs(a.z)+Mathf.Abs(b.z)+Mathf.Abs(c.z));
                        result.Add(new Prop { mesh=filter.sharedMesh,material=renderer.sharedMaterial,matrix=model,bounds=new Bounds(model.MultiplyPoint3x4(bounds.center),extents*2) });
                    }
                }
                var foliage=set.groundCover;
                if(foliage!=null && foliage.enabled && foliage.material!=null && foliage.variants.Length>0)
                {
                    for(int i=0;i<foliage.distantClumpsPerTile*4;i++)
                    {
                        if(i>=foliage.distantClumpsPerTile && result.Count>=set.distantPropsPerTile+foliage.distantClumpsPerTile)break;
                        var mesh=foliage.variants[Random.Range(0,foliage.variants.Length)]; if(mesh==null)continue;
                        float scale=Random.Range(Mathf.Max(.1f,foliage.scaleRange.x),Mathf.Max(.1f,foliage.scaleRange.y));
                        float footprint=mesh.bounds.extents.magnitude*scale;
                        Vector3 position=new(key.x*TileSize+Random.Range(footprint,TileSize-footprint),0,
                            key.y*TileSize+Random.Range(footprint,TileSize-footprint));
                        if(Random.value>foliage.Density(position))continue;
                        float clearance=road.roadWidth*.5f+foliage.roadsideDistance+footprint+2;
                        bool blocked=false;
                        foreach(var sample in roadSamples)if((sample-position).sqrMagnitude<clearance*clearance){blocked=true;break;}
                        if(blocked)continue;
                        var matrix=Matrix4x4.TRS(position,Quaternion.Euler(0,Random.Range(0,360f),0),Vector3.one*scale);
                        result.Add(new Prop {mesh=mesh,material=foliage.material,matrix=matrix,
                            bounds=new Bounds(matrix.MultiplyPoint3x4(mesh.bounds.center),Vector3.one*mesh.bounds.size.magnitude*scale),maximumDistance=foliage.drawDistance});
                    }
                }
            }
            finally { Random.state=oldRandom; }
            var aggregateBounds=result.Count>0?result[0].bounds:new Bounds(new Vector3(key.x*TileSize,0,key.y*TileSize),Vector3.zero);
            foreach(var prop in result)aggregateBounds.Encapsulate(prop.bounds);
            tileBounds[key]=aggregateBounds;
            return result;
        }
        private void OnDisable() { tiles.Clear(); tileBounds.Clear(); batches.Clear(); lastSet=null; CachedPropCount=VisiblePropCount=VisibleBatchCount=0; }
    }
}
