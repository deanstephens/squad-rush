using UnityEngine;

namespace SquadRush
{
    /// <summary>Moves an object down the treadmill and despawns it once it is behind the squad.</summary>
    public class ScrollingObject : MonoBehaviour
    {
        public float despawnZ = -10f;

        void Update()
        {
            float d = Treadmill.Delta;
            if (d <= 0f) return;
            transform.position += Vector3.back * d;
            if (transform.position.z < despawnZ) Destroy(gameObject);
        }
    }
}
