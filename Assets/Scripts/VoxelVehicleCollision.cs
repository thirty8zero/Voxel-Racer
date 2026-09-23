using UnityEngine;
namespace VoxelRacer
{
    /// <summary>Sweeps relative lane/track motion so boost lunges and fast oncoming traffic cannot skip contact.</summary>
    public static class VoxelVehicleCollision
    {
        public static bool Sweep(Vector2 previous, Vector2 current, Vector2 halfSize, out Vector2 contact)
        {
            float enter=0, exit=1;
            for(int axis=0;axis<2;axis++)
            {
                float delta=current[axis]-previous[axis];
                if(Mathf.Abs(delta)<.00001f)
                { if(Mathf.Abs(previous[axis])>halfSize[axis]) {contact=current;return false;} }
                else
                {
                    float a=(-halfSize[axis]-previous[axis])/delta, b=(halfSize[axis]-previous[axis])/delta;
                    enter=Mathf.Max(enter,Mathf.Min(a,b));exit=Mathf.Min(exit,Mathf.Max(a,b));
                    if(enter>exit) {contact=current;return false;}
                }
            }
            contact=Vector2.Lerp(previous,current,enter);return true;
        }
        public static Vector3 ImpactDirection(Vector2 contact,Transform player)
        {
            var forward=Vector3.ProjectOnPlane(player.forward,Vector3.up).normalized;
            var right=Vector3.Cross(Vector3.up,forward);
            var direction=-right*contact.x-forward*contact.y;
            return direction.sqrMagnitude>.0001f?direction.normalized:forward;
        }
    }
}
