using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Lightweight workshop set: shared materials, simple geometry, no realtime reflections.</summary>
    public sealed class VoxelGarageEnvironment : MonoBehaviour
    {
        private readonly List<Material> materials = new();
        public static void Build(Transform parent)
        {
            var holder=new GameObject("Garage Set");holder.transform.SetParent(parent,false);holder.transform.localRotation=Quaternion.Euler(0,45,0);
            var set=holder.AddComponent<VoxelGarageEnvironment>();set.CreateSet();
        }
        private Material Material(string name,Color color,float smooth=0,float metal=0)
        {
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));mat.name=name;mat.color=color;
            mat.SetFloat("_Smoothness",smooth);mat.SetFloat("_Metallic",metal);materials.Add(mat);return mat;
        }
        private GameObject Box(string name,Vector3 position,Vector3 scale,Material mat)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(transform,false);
            go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;
            var collider=go.GetComponent<Collider>();if(Application.isPlaying)Destroy(collider);else DestroyImmediate(collider);return go;
        }
        private void Sign(string text,Vector3 position,float size,Color color)
        {
            var go=new GameObject("Workshop Sign");go.transform.SetParent(transform,false);go.transform.localPosition=position;
            go.transform.localRotation=Quaternion.Euler(0,180,0);
            var tm=go.AddComponent<TextMesh>();tm.text=text;tm.characterSize=size*.23f;tm.fontSize=60;tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.color=color;
            var font=Resources.Load<Font>("Fonts/VCR_OSD_MONO_1.001");if(font!=null){tm.font=font;go.GetComponent<MeshRenderer>().sharedMaterial=font.material;}
        }
        private void CreateSet()
        {
            var wall=Material("Garage Charcoal",new(.035f,.044f,.057f));
            var metal=Material("Garage Steel",new(.095f,.115f,.14f),.35f,.4f);
            var floor=Material("Garage Concrete",new(.085f,.095f,.105f),.65f,.25f);
            var tile=Material("Garage Concrete Variation",new(.074f,.082f,.095f),.55f,.2f);
            var red=Material("Tool Cabinet Red",new(.32f,.035f,.022f),.3f,.25f);
            var yellow=Material("Safety Ochre",new(.72f,.38f,.07f),.25f);
            var black=Material("Rubber and Shadow",new(.019f,.023f,.026f));
            var crate=Material("Olive Storage",new(.12f,.16f,.075f));
            var lightMat=Material("Warm Strip",new(1f,.68f,.30f));lightMat.EnableKeyword("_EMISSION");lightMat.SetColor("_EmissionColor",new Color(1,.45f,.12f)*3);
            Box("Workshop Floor",new(0,-.13f,0),new(24,.25f,24),floor);
            for(int x=-6;x<=6;x++)for(int z=-6;z<=6;z++)if((x+z)%2==0)Box("Concrete Tile",new(x*1.5f,.002f,z*1.5f),new(1.48f,.008f,1.48f),tile);
            Box("Back Wall",new(0,2.7f,-5.4f),new(20,5.5f,.25f),wall);
            Box("Side Wall",new(-7.5f,2.7f,0),new(.25f,5.5f,11),wall);
            for(int x=-7;x<=8;x+=3)
            {
                Box("Wall Support",new(x,2.65f,-5.18f),new(.22f,5.3f,.3f),metal);
                Box("Wall Panel",new(x+1.3f,2.5f,-5.23f),new(2.3f,3.4f,.1f),tile);
                Box("Strip Housing",new(x+1.3f,3.9f,-4.97f),new(1.95f,.19f,.27f),black);
                Box("Warm Workshop Strip",new(x+1.3f,3.86f,-4.80f),new(1.70f,.065f,.06f),lightMat);
            }
            for(int side=-1;side<=1;side+=2)
                Box("Parking Bay Stripe",new(side*2.05f,.014f,0),new(.055f,.014f,6.3f),yellow);
            Box("Parking Bay End",new(0,.014f,-3.15f),new(4.15f,.014f,.055f),yellow);
            for(int i=0;i<10;i++){var stripe=Box("Hazard Stripe",new(3.5f+i*.22f,.016f,-3.8f),new(.11f,.016f,.7f),yellow);stripe.transform.localRotation=Quaternion.Euler(0,35,0);}
            for(int cabinet=0;cabinet<4;cabinet++)
            {
                float x=-4.7f+cabinet*3.1f;
                Box("Red Tool Cabinet",new(x,.65f,-4.65f),new(1.05f,1.25f,.62f),red);
                Box("Tool Cabinet Top",new(x,1.3f,-4.63f),new(1.13f,.08f,.70f),metal);
                for(int drawer=0;drawer<5;drawer++)Box("Drawer Handle",new(x,.26f+drawer*.20f,-4.31f),new(.78f,.026f,.03f),metal);
                for(int wheel=-1;wheel<=1;wheel+=2)Box("Cabinet Caster",new(x+wheel*.38f,.05f,-4.62f),new(.14f,.12f,.35f),black);
            }
            Box("Workbench",new(2.1f,1.1f,-4.65f),new(2.0f,.14f,.85f),metal);
            Box("Diagnostic Monitor",new(2.1f,1.65f,-4.7f),new(1.35f,.78f,.12f),black);
            var screen=Material("Diagnostic Cyan",new(.015f,.11f,.19f));screen.EnableKeyword("_EMISSION");screen.SetColor("_EmissionColor",new Color(.02f,.25f,.40f));
            Box("Diagnostic Display",new(2.1f,1.65f,-4.62f),new(1.22f,.64f,.015f),screen);
            Sign("VEHICLE SYSTEMS\n///// ONLINE /////",new(2.1f,1.65f,-4.60f),.045f,new(.1f,.8f,1));
            Sign("SMALL CAR.\nBIG ADVANTAGE.",new(4.7f,2.9f,-5.05f),.14f,new(.65f,.63f,.54f));
            Sign("CARS\nWEAPONS\nFREEDOM",new(-1.5f,2.8f,-5.05f),.14f,new(.55f,.53f,.46f));
            for(int i=0;i<8;i++) Box("Storage Crate",new(-5.4f+(i%3)*.65f,.3f+(i/3)*.57f,-3.8f),new(.58f,.55f,.65f),crate);
            for(int i=0;i<4;i++)
            {var tyre=GameObject.CreatePrimitive(PrimitiveType.Cylinder);tyre.name="Stacked Spare Tyre";tyre.transform.SetParent(transform,false);tyre.transform.localPosition=new(-3.4f,.16f+i*.28f,-3.9f);tyre.transform.localScale=new(.8f,.13f,.8f);tyre.GetComponent<Renderer>().sharedMaterial=black;
             if(Application.isPlaying)Destroy(tyre.GetComponent<Collider>());else DestroyImmediate(tyre.GetComponent<Collider>());}
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new(.18f,.21f,.26f);
            RenderSettings.fog=false;
            Light("Garage Key",new(1,5,1),new(45,-30,0),LightType.Directional,new(1,.79f,.59f),1.15f);
            Light("Garage Blue Fill",new(4,3,4),new(25,145,0),LightType.Directional,new(.48f,.67f,1),.55f);
            Light("Warm Bench Light",new(1,3.5f,-3.9f),Vector3.zero,LightType.Point,new(1,.52f,.20f),5);
        }
        private void Light(string name,Vector3 p,Vector3 e,LightType type,Color color,float intensity)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.localPosition=p;go.transform.localRotation=Quaternion.Euler(e);
            var light=go.AddComponent<Light>();light.type=type;light.color=color;light.intensity=intensity;light.range=10;
            light.shadows=type==LightType.Directional?LightShadows.Soft:LightShadows.None;
        }
        private void OnDestroy(){foreach(var m in materials)if(m!=null){if(Application.isPlaying)Destroy(m);else DestroyImmediate(m);}}
    }
}
