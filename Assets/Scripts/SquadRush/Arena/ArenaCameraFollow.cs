using UnityEngine;

namespace SquadRush.Arena
{
    /// <summary>Top-down chase camera for the arena.</summary>
    public class ArenaCameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 17f, -9f);
        public float smoothing = 10f;

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 want = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, want, 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime));
        }
    }
}
