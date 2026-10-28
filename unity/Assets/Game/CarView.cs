#nullable enable
using UnityEngine;

namespace DesktopDrift.Game
{
    /// <summary>Cuboid placeholder for the car; interpolates between the two last Core states.</summary>
    public sealed class CarView : MonoBehaviour
    {
        [SerializeField] Material? material;

        Transform? body;
        double prevX, prevY, prevA, curX, curY, curA;
        float pushedAt; // Time.fixedTime of the last Push/SetPose: a fixed step without one means paused/finished

        public void Configure(double lenUnits = 82, double widUnits = 30.220657276995304)
        {
            if (body != null) DestroyBody();
            float len = (float)lenUnits * WorldMapping.UnitsToMeters;
            float wid = (float)widUnits * WorldMapping.UnitsToMeters;
            float h = 0.4f * wid;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Body";
            var col = go.GetComponent<Collider>();
            if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
            body = go.transform;
            body.SetParent(transform, false);
            body.localScale = new Vector3(wid, h, len);
            body.localPosition = new Vector3(0f, h * 0.5f, 0f);
            if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        public void SetPose(double x, double y, double angle)
        {
            prevX = curX = x; prevY = curY = y; prevA = curA = angle;
            pushedAt = Time.fixedTime;
            Apply(1f);
        }

        public void Push(double x, double y, double angle)
        {
            prevX = curX; prevY = curY; prevA = curA;
            curX = x; curY = y; curA = angle;
            pushedAt = Time.fixedTime;
        }

        void LateUpdate()
        {
            // No Push since the last fixed step (paused / finished): hold the last state instead of
            // oscillating between prev and cur as Time.fixedTime keeps advancing.
            bool stale = Time.fixedTime - pushedAt > 0.5f * Time.fixedDeltaTime;
            float alpha = stale ? 1f : Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            Apply(alpha);
        }

        void Apply(float alpha)
        {
            transform.position = Vector3.Lerp(WorldMapping.ToWorld(prevX, prevY), WorldMapping.ToWorld(curX, curY), alpha);
            transform.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(WorldMapping.YawDeg(prevA), WorldMapping.YawDeg(curA), alpha), 0f);
        }

        void DestroyBody()
        {
            if (Application.isPlaying) Destroy(body!.gameObject); else DestroyImmediate(body!.gameObject);
            body = null;
        }
    }
}
