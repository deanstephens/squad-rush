using UnityEngine;

namespace SquadRush
{
    /// <summary>Keeps world-space labels facing the camera.</summary>
    public class Billboard : MonoBehaviour
    {
        Transform cam;

        void LateUpdate()
        {
            if (cam == null)
            {
                var c = Camera.main;
                if (c == null) return;
                cam = c.transform;
            }
            transform.rotation = cam.rotation;
        }
    }
}
