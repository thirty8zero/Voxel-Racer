using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelRacer
{
    /// <summary>Stable world tiles surround the camera in every direction. Only visible props
    /// are submitted, grouped by mesh/material, without thousands of scenery GameObjects.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed partial class VoxelDistantScenery : MonoBehaviour
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
        private sealed class Tile
        {
            public readonly List<Prop> Props;
            public readonly Bounds Bounds;

            public Tile(List<Prop> props, Bounds bounds)
            {
                Props = props;
                foreach (var prop in props) bounds.Encapsulate(prop.bounds);
                Bounds = bounds;
            }
        }

        private Dictionary<Vector2Int, Tile> tiles = new();
        private readonly List<Vector2Int> expired = new();
        private readonly RoadSamples roadSamples = new();
        private readonly Dictionary<(Mesh,Material),List<Matrix4x4>> batches = new();
        private readonly Matrix4x4[] drawBuffer = new Matrix4x4[1023];
        private readonly Plane[] planes = new Plane[6];
        private EndlessVoxelRoad road;
        private VoxelScenerySet lastSet;
        private Vector2Int lastTile = new(int.MinValue,int.MinValue);
        private float lastRadius;
        private int lastSeed, lastDensity, lastSetRevision;
        private uint lastPathRevision;
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
            SubmitHorizonFoliage(camera);
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
            if(set==null || !set.distantSceneryEnabled)
            { tiles.Clear(); openingScenery=null; lastSet=null; CachedPropCount=0; ClearHorizonFoliage(); return; }
            float radius=CoverageRadius(camera,set);
            var center=new Vector2Int(Mathf.FloorToInt(camera.transform.position.x/TileSize),Mathf.FloorToInt(camera.transform.position.z/TileSize));
            uint pathRevision = PrepareOpeningScenery(camera, set, radius);
            bool settingsChanged=set!=lastSet || radius!=lastRadius || set.distantScenerySeed!=lastSeed || set.distantPropsPerTile!=lastDensity ||
                set.Revision!=lastSetRevision || pathRevision!=lastPathRevision ||
                set.groundCover != lastFoliage || (set.groundCover != null && set.groundCover.Revision != lastFoliageRevision);
            if(settingsChanged) { tiles.Clear(); batches.Clear(); }
            if(settingsChanged || center!=lastTile)
            {
                // Sample the actual curved road, including future path and the finish-camera rear view.
                roadSamples.Capture(road.SceneryTrackDistance, radius * 2 + 64, road.Evaluate);
                int extent = SceneryTileExtent(radius);
                expired.Clear();
                foreach(var tile in tiles.Keys)
                    if(Mathf.Abs(tile.x-center.x)>extent || Mathf.Abs(tile.y-center.y)>extent) expired.Add(tile);
                foreach(var tile in expired) tiles.Remove(tile);
                foreach (var key in SquareTiles(center, extent))
                    if (!tiles.ContainsKey(key)) tiles.Add(key, BuildTile(key, set, roadSamples));
                lastSet=set; lastTile=center; lastRadius=radius; lastSeed=set.distantScenerySeed; lastDensity=set.distantPropsPerTile;
                lastPathRevision=pathRevision;
                lastSetRevision=set.Revision;
                lastFoliage=set.groundCover; lastFoliageRevision=lastFoliage!=null?lastFoliage.Revision:0;
                foliageProperties=lastFoliage!=null?lastFoliage.DrawProperties():null;
                CachedPropCount=0; foreach(var tile in tiles.Values) CachedPropCount+=tile.Props.Count;
            }
            GeometryUtility.CalculateFrustumPlanes(camera,planes);
            foreach(var tile in tiles.Values)
            {
                // Reject whole off-camera tiles before inspecting individual plant bounds.
                if(tile.Props.Count==0 || !GeometryUtility.TestPlanesAABB(planes,tile.Bounds))continue;
                foreach(var prop in tile.Props)
                {
                    Vector3 delta=prop.bounds.center-camera.transform.position; delta.y=0;
                    float distance=prop.maximumDistance>0?Mathf.Min(radius,prop.maximumDistance):radius;
                    if(delta.sqrMagnitude>distance*distance || !GeometryUtility.TestPlanesAABB(planes,prop.bounds)) continue;
                    var key=(prop.mesh,prop.material);
                    if(!batches.TryGetValue(key,out var list)) { list=new List<Matrix4x4>(); batches.Add(key,list); }
                    var matrix = prop.matrix;
                    if (prop.maximumDistance <= 0)
                    {
                        // Instanced 3D props grow in across the outer 20% of their
                        // visible range; keep translation and cached full-size bounds.
                        float growth = Mathf.SmoothStep(0, 1, Mathf.Clamp01((distance - delta.magnitude) / Mathf.Max(1, distance * .2f)));
                        for (int axis = 0; axis < 3; axis++) matrix.SetColumn(axis, matrix.GetColumn(axis) * growth);
                    }
                    list.Add(matrix); VisiblePropCount++;
                }
            }
            foreach(var batch in batches.Values)if(batch.Count>0)VisibleBatchCount++;
            PrepareHorizonFoliage(camera, set, pathRevision);
        }

        private static int SceneryTileExtent(float radius) => Mathf.CeilToInt(radius / TileSize) + 1;

        private static IEnumerable<Vector2Int> SquareTiles(Vector2Int center, int extent)
        {
            for (int x = -extent; x <= extent; x++)
            for (int z = -extent; z <= extent; z++)
                yield return center + new Vector2Int(x, z);
        }

        private Tile BuildTile(Vector2Int key, VoxelScenerySet set, RoadSamples samples)
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
                    if(samples.WithinClearance(pos, clearance)) continue;
                    if(Random.value>set.PlacementDensity(pos)) continue;
                    bool blocked=false;
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
                        if(samples.WithinClearance(position, clearance))continue;
                        var matrix=Matrix4x4.TRS(position,Quaternion.Euler(0,Random.Range(0,360f),0),Vector3.one*scale);
                        result.Add(new Prop {mesh=mesh,material=foliage.material,matrix=matrix,
                            bounds=new Bounds(matrix.MultiplyPoint3x4(mesh.bounds.center),Vector3.one*mesh.bounds.size.magnitude*scale),maximumDistance=foliage.drawDistance});
                    }
                }
            }
            finally { Random.state=oldRandom; }
            var tileBounds=result.Count>0?result[0].bounds:new Bounds(new Vector3(key.x*TileSize,0,key.y*TileSize),Vector3.zero);
            return new Tile(result, tileBounds);
        }
        private void OnDisable() { tiles.Clear(); openingScenery=null; batches.Clear(); ClearHorizonFoliage(); lastSet=null; CachedPropCount=VisiblePropCount=VisibleBatchCount=0; }
    }
}
