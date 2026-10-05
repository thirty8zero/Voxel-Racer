using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelRacer.Editor
{
    public static class VoxelDesertDetailBuilder
    {
        public const string Folder = "Assets/Resources/Scenery/DesertFoliage";
        public const string TuningPath = Folder + "/DesertGroundCover.asset";
        private const float Cell = .14f;
        private static readonly Dictionary<Vector3Int,int> cells = new();

        [MenuItem("Tools/Voxel Racer/Build Desert Foliage and Tall Cacti")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            string atlasPath=Folder+"/DesertFoliageAtlas.png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(atlasPath);
            if(importer==null)throw new System.InvalidOperationException("Missing generated foliage atlas at "+atlasPath);
            importer.textureType=TextureImporterType.Default; importer.sRGBTexture=true;
            importer.alphaSource=TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency=true;
            importer.mipmapEnabled=true; importer.mipMapsPreserveCoverage=true; importer.alphaTestReferenceValue=.45f;
            importer.wrapMode=TextureWrapMode.Clamp; importer.filterMode=FilterMode.Trilinear;
            importer.maxTextureSize=1024; importer.isReadable=true;
            importer.textureCompression=TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath); var pixels=atlas.GetPixels32();
            var shader=Shader.Find("Voxel Racer/Desert Foliage");
            if(shader==null)throw new System.InvalidOperationException("Missing foliage shader");
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/DesertFoliage.mat");
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,Folder+"/DesertFoliage.mat");}
            material.shader=shader;material.mainTexture=atlas;material.enableInstancing=true;material.SetFloat("_Cutoff",.45f);
            EditorUtility.SetDirty(material);
            var tuning=AssetDatabase.LoadAssetAtPath<VoxelDesertFoliageTuning>(TuningPath);
            if(tuning==null){tuning=ScriptableObject.CreateInstance<VoxelDesertFoliageTuning>();AssetDatabase.CreateAsset(tuning,TuningPath);}
            tuning.material=material;tuning.variants=new Mesh[16];
            for(int index=0;index<16;index++)
            {
                int col=index%4,row=3-index/4;
                int left=col*atlas.width/4,right=(col+1)*atlas.width/4-1;
                int bottom=row*atlas.height/4,top=(row+1)*atlas.height/4-1;
                int minX=right,minY=top,maxX=left,maxY=bottom;
                for(int y=bottom;y<=top;y++)for(int x=left;x<=right;x++)
                    if(pixels[y*atlas.width+x].a>128){minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);}
                // Insets and transparent gutters prevent adjacent atlas cells appearing in mipmaps.
                var rect=Rect.MinMaxRect((Mathf.Max(left,minX-2)+.5f)/atlas.width,(Mathf.Max(bottom,minY-2)+.5f)/atlas.height,
                    (Mathf.Min(right,maxX+2)+.5f)/atlas.width,(Mathf.Min(top,maxY+2)+.5f)/atlas.height);
                float height=index<4?1.05f:index<12?1.35f:1.5f;
                float width=height*(maxX-minX+1)/(float)Mathf.Max(1,maxY-minY+1);
                if(index>=4 && index<12)width*=1.35f;
                tuning.variants[index]=SaveMesh(CrossCard("Desert Foliage "+index,width,height,rect),Folder+"/FoliageCard"+index.ToString("00")+".asset");
            }
            importer.isReadable=false; importer.textureCompression=TextureImporterCompression.CompressedHQ;
            var android=importer.GetPlatformTextureSettings("Android");android.overridden=true;android.maxTextureSize=1024;
            android.format=TextureImporterFormat.ASTC_6x6;android.compressionQuality=70;importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();EditorUtility.SetDirty(tuning);
            var set=AssetDatabase.LoadAssetAtPath<VoxelScenerySet>(VoxelDesertSceneryBuilder.SetPath);
            if(set==null)throw new System.InvalidOperationException("Build the base Desert Scenery Collection first.");
            bool first=set.groundCover==null;set.groundCover=tuning;
            var entries=new List<VoxelScenerySet.Entry>(set.entries);
            if(first)
            {
                set.minimumPerSegment=6;set.maximumPerSegment=10;set.clustering=.85f;set.clusterSize=26;
                foreach(var entry in entries)
                {
                    string name=entry.prefab!=null?entry.prefab.name:"";
                    if(name.StartsWith("Saguaro") || name.StartsWith("Cactus"))entry.weight=.6f;
                    else if(name=="BarrelCactusCluster" || name=="PricklyPear")entry.weight=.4f;
                    else if(name=="DryScrub" || name=="DryBush")entry.weight=0;
                    else if(name.StartsWith("DeadTree"))entry.weight=.35f;
                    else entry.weight=2;
                }
            }
            var cactusMaterial=AssetDatabase.LoadAssetAtPath<Material>(VoxelDesertSceneryBuilder.Folder+"/DesertScenery.mat");
            if(cactusMaterial==null)throw new System.InvalidOperationException("Missing base cactus palette material");
            string[] names={"SaguaroGrandTwin","SaguaroGrandFork","SaguaroGrandColumn"};
            for(int index=0;index<3;index++)
            {
                TallCactus(index);var mesh=SaveMesh(CactusMesh(names[index]),Folder+"/"+names[index]+"Mesh.asset");
                var go=new GameObject(names[index],typeof(MeshFilter),typeof(MeshRenderer));
                go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=cactusMaterial;
                string prefabPath=VoxelDesertSceneryBuilder.Folder+"/"+names[index]+".prefab";
                var prefab=PrefabUtility.SaveAsPrefabAsset(go,prefabPath);Object.DestroyImmediate(go);
                if(!entries.Exists(e=>e.prefab==prefab))
                {
                    float radius=new Vector2(mesh.bounds.extents.x,mesh.bounds.extents.z).magnitude;
                    entries.Add(new VoxelScenerySet.Entry {prefab=prefab,weight=index==2?1.2f:2.4f,radius=radius,
                        scaleRange=new Vector2(.8f,1.2f),roadClearance=3,maximumRoadDistance=35,spacing=2});
                }
            }
            set.entries=entries.ToArray();EditorUtility.SetDirty(set);AssetDatabase.SaveAssets();
            Debug.Log("Built 16 atlas-backed 4-triangle crossed-card variants and three tall single-mesh voxel saguaros. Desert ground cover registered; existing settings preserved on subsequent builds.");
        }
        private static Mesh SaveMesh(Mesh mesh,string path)
        {
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);return existing;
        }
        private static Mesh CrossCard(string name,float width,float height,Rect rect)
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();var colours=new List<Color>();
            for(int card=0;card<2;card++)
            {
                var rotation=Quaternion.Euler(0,card*90,0);int first=vertices.Count;
                foreach(var point in new[]{new Vector3(-width*.5f,0,0),new Vector3(width*.5f,0,0),new Vector3(width*.5f,height,0),new Vector3(-width*.5f,height,0)})
                {vertices.Add(rotation*point);normals.Add(rotation*Vector3.back);colours.Add(Color.white);}
                uv.AddRange(new[]{new Vector2(rect.xMin,rect.yMin),new Vector2(rect.xMax,rect.yMin),new Vector2(rect.xMax,rect.yMax),new Vector2(rect.xMin,rect.yMax)});
                triangles.AddRange(new[]{first,first+2,first+1,first,first+3,first+2});
            }
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetColors(colours);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
        }
        private static void Put(Vector3Int p)
        {
            if(p.y<0)return;
            int shade=Mathf.Abs(p.x*7+p.z*11)%4;
            if(p.y%11==3 && (p.x+p.z)%3==0)shade=3;
            cells[p]=shade;
        }
        private static void Tube(Vector3Int[] path,int radius)
        {
            for(int point=1;point<path.Length;point++)
            {
                int steps=Mathf.CeilToInt(Vector3.Distance(path[point-1],path[point])*2);
                for(int step=0;step<=steps;step++)
                {
                    var center=Vector3Int.RoundToInt(Vector3.Lerp(path[point-1],path[point],step/(float)Mathf.Max(1,steps)));
                    for(int x=-radius;x<=radius;x++)for(int y=-radius;y<=radius;y++)for(int z=-radius;z<=radius;z++)
                        if(x*x+y*y+z*z<=radius*radius+1)Put(center+new Vector3Int(x,y,z));
                }
            }
        }
        private static void TallCactus(int variant)
        {
            cells.Clear();int height=variant==0?54:variant==1?61:66;
            Tube(new[]{new Vector3Int(0,2,0),new Vector3Int(0,height-3,0)},3);
            if(variant!=2)
            {
                Tube(new[]{new Vector3Int(0,22,0),new Vector3Int(-5,22,0),new Vector3Int(-9,25,0),new Vector3Int(-11,30,0),new Vector3Int(-11,45,0)},2);
                Tube(new[]{new Vector3Int(0,32,0),new Vector3Int(5,32,1),new Vector3Int(9,35,1),new Vector3Int(11,40,1),new Vector3Int(11,variant==1?55:46,1)},2);
                if(variant==1)Tube(new[]{new Vector3Int(0,16,0),new Vector3Int(1,17,-6),new Vector3Int(1,22,-8),new Vector3Int(1,34,-8)},2);
            }
            else Tube(new[]{new Vector3Int(0,18,0),new Vector3Int(5,18,0),new Vector3Int(8,21,0),new Vector3Int(8,37,0)},2);
        }
        private static Mesh CactusMesh(string name)
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            var visited=new HashSet<(Vector3Int,int)>();
            Vector3Int[] directions={Vector3Int.right,Vector3Int.left,Vector3Int.up,Vector3Int.down,Vector3Int.forward,Vector3Int.back};
            foreach(var cell in cells)for(int face=0;face<6;face++)
            {
                var direction=directions[face];if(cells.ContainsKey(cell.Key+direction) || visited.Contains((cell.Key,face)))continue;
                int run=1;
                if(direction.y==0)
                    while(cells.TryGetValue(cell.Key+Vector3Int.up*run,out int colour) && colour==cell.Value &&
                        !cells.ContainsKey(cell.Key+Vector3Int.up*run+direction) && !visited.Contains((cell.Key+Vector3Int.up*run,face)))run++;
                for(int n=0;n<run;n++)visited.Add((cell.Key+Vector3Int.up*n,face));
                Vector3 normal=direction;Vector3 u=Vector3.Cross(Mathf.Abs(normal.y)>.5f?Vector3.forward:Vector3.up,normal);
                Vector3 v=Vector3.Cross(normal,u);float vScale=direction.y==0?run:1;
                Vector3 center=(Vector3)cell.Key*Cell+Vector3.up*(Cell*.5f+(run-1)*Cell*.5f)+normal*Cell*.5f;
                int first=vertices.Count;
                foreach(var corner in new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)})
                {vertices.Add(center+(u*corner.x+v*corner.y*vScale)*Cell*.5f);normals.Add(normal);uv.Add(new Vector2((cell.Value+.5f)/16,.5f));}
                triangles.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});
            }
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt16};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
        }
    }
}
