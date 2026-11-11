using UnityEngine;
using UnityEngine.InputSystem;

namespace VoxelRacer
{
    /// <summary>Low-speed, centred loading manoeuvre for the carrier test scene.</summary>
    public sealed class VoxelCarrierDrive : MonoBehaviour
    {
        [SerializeField] private VoxelCarrierTruck truck;
        [SerializeField] private Transform car;
        [SerializeField] private Camera view;
        [SerializeField] private Bounds carBounds;
        [SerializeField] private Vector2 axles;
        [SerializeField] private float wheelGround;
        [SerializeField] private Transform[] wheels;
        [SerializeField] private float wheelRadius;

        private float rearZ;
        private float speed;
        private bool rearView;
        private GUIStyle statusStyle;

        private float FrontOverhang => carBounds.max.z - axles.x;
        private float LastGroundPosition => truck.RampTip - .20f - FrontOverhang;
        private float FirstStowedPosition => truck.BayRear + .15f - (carBounds.min.z - axles.x);
        public bool Stowed => rearZ >= FirstStowedPosition;
        public bool ClearOfRamp => rearZ <= LastGroundPosition;

        public void Configure(VoxelCarrierTruck carrier, Transform player, Camera camera, Bounds bounds,
            Vector2 wheelAxles, float tyreBottom, Transform[] wheelPivots, float tyreRadius)
        {
            truck = carrier;
            car = player;
            view = camera;
            carBounds = bounds;
            axles = wheelAxles;
            wheelGround = tyreBottom;
            wheels = wheelPivots;
            wheelRadius = tyreRadius;
            ResetCar();
        }

        private void Start() => ResetCar();

        private void Update()
        {
            var keyboard = Keyboard.current;
            var pad = Gamepad.current;
            if (keyboard?.rKey.wasPressedThisFrame == true || pad?.buttonNorth.wasPressedThisFrame == true)
                ResetCar();
            if (keyboard?.eKey.wasPressedThisFrame == true || pad?.buttonSouth.wasPressedThisFrame == true)
                ToggleRamp();
            if (keyboard?.cKey.wasPressedThisFrame == true || pad?.buttonWest.wasPressedThisFrame == true)
            {
                rearView = !rearView;
                PlaceCamera();
            }

            float throttle = pad?.leftStick.y.ReadValue() ?? 0f;
            if (keyboard != null)
            {
                bool forward = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;
                bool reverse = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
                if (forward || reverse)
                    throttle = (forward ? 1f : 0f) - (reverse ? 1f : 0f);
                if (keyboard.spaceKey.isPressed)
                    throttle = 0f;
            }
            Drive(throttle, Time.deltaTime);
        }

        public bool ToggleRamp()
        {
            if ((!Stowed && !ClearOfRamp) || (!truck.IsOpen && !truck.IsClosed))
                return false;

            truck.SetRampOpen(!truck.IsOpen);
            return true;
        }

        public void Drive(float throttle, float seconds)
        {
            float target = Mathf.Clamp(throttle, -1f, 1f) * (throttle < 0f ? 2f : 2.8f);
            speed = Mathf.MoveTowards(speed, target, (target == 0f ? 9f : 5f) * seconds);
            float min = truck.RampTip - FrontOverhang - 5f;
            float max = truck.BayFront - FrontOverhang - .13f;
            if (!truck.IsOpen || !truck.Opening)
            {
                if (Stowed)
                    min = FirstStowedPosition;
                else
                    max = LastGroundPosition;
            }

            float next = Mathf.Clamp(rearZ + speed * seconds, min, max);
            float distance = next - rearZ;
            rearZ = next;
            if (Mathf.Abs(distance) > .00001f)
            {
                PlaceCar();
                foreach (Transform wheel in wheels)
                    wheel.Rotate(Vector3.right, distance / wheelRadius * Mathf.Rad2Deg, Space.Self);
            }
            if (rearZ <= min || rearZ >= max)
                speed = 0f;
        }

        public void ResetCar()
        {
            rearZ = truck.RampTip - FrontOverhang - 2f;
            speed = 0f;
            PlaceCar();
            PlaceCamera();
        }

        private void PlaceCar()
        {
            float rearHeight = truck.SurfaceHeight(rearZ);
            float wheelbase = axles.y - axles.x;
            // Keep the axle spacing constant while the front and rear cross the two grade changes.
            float near = rearZ;
            float far = rearZ + wheelbase;
            for (int i = 0; i < 14; i++)
            {
                float mid = (near + far) * .5f;
                float rise = truck.SurfaceHeight(mid) - rearHeight;
                float run = mid - rearZ;
                if (run * run + rise * rise > wheelbase * wheelbase)
                    far = mid;
                else
                    near = mid;
            }
            float frontZ = (near + far) * .5f;
            float pitch = -Mathf.Atan2(truck.SurfaceHeight(frontZ) - rearHeight, frontZ - rearZ);
            var rotation = Quaternion.AngleAxis(pitch * Mathf.Rad2Deg, Vector3.right);
            var rearContact = new Vector3(-carBounds.center.x, rearHeight, rearZ);
            Vector3 origin = rearContact - rotation * new Vector3(0f, wheelGround, axles.x);
            car.SetPositionAndRotation(truck.transform.TransformPoint(origin), truck.transform.rotation * rotation);
        }

        private void PlaceCamera()
        {
            Vector3 offset = rearView ? new Vector3(.1f, 4.1f, -19.2f) : new Vector3(10.8f, 6.1f, -15.1f);
            Vector3 target = rearView ? new Vector3(0f, 1.65f, -4.1f) : new Vector3(0f, 1.3f, -3.6f);
            view.transform.position = truck.transform.TransformPoint(offset);
            view.transform.LookAt(truck.transform.TransformPoint(target), truck.transform.up);
        }

        private void OnGUI()
        {
            statusStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
            string status = truck.IsOpen ? Stowed ? "CAR LOADED" : "RAMP DOWN" :
                truck.IsClosed ? Stowed ? "CAR SECURED" : "RAMP CLOSED" :
                truck.Opening ? "LOWERING RAMP" : "RAISING RAMP";
            var position = new Rect((Screen.width - 548f) * .5f, Screen.height - 66f, 548f, 48f);
            GUI.Box(position, status + "\nW/S  Drive & reverse     E  Ramp     Space  Brake     C  View     R  Reset", statusStyle);
        }
    }
}
