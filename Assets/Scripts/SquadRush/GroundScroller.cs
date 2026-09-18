using UnityEngine;

namespace SquadRush
{
    /// <summary>Recycles a row of ground segments so the floor appears to slide toward the player.</summary>
    public class GroundScroller : MonoBehaviour
    {
        public float segmentLength = 8f;
        public Transform[] segments;

        void Update()
        {
            float d = Treadmill.Delta;
            if (d <= 0f || segments == null || segments.Length == 0) return;

            float total = segmentLength * segments.Length;
            foreach (var s in segments)
            {
                var p = s.position;
                p.z -= d;
                if (p.z < -segmentLength * 2f) p.z += total;
                s.position = p;
            }
        }
    }
}
