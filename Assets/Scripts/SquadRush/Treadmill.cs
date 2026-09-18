using UnityEngine;

namespace SquadRush
{
    /// <summary>
    /// Global "conveyor belt" state. Everything in the lane moves toward -Z at this speed
    /// while the squad stays put at the bottom of the screen.
    /// </summary>
    public static class Treadmill
    {
        public static float Speed { get; set; }
        public static bool Running { get; set; }

        /// <summary>World units to move this frame (0 when the belt is stopped).</summary>
        public static float Delta => Running ? Speed * Time.deltaTime : 0f;

        public static void Reset()
        {
            Speed = 0f;
            Running = false;
        }
    }

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
