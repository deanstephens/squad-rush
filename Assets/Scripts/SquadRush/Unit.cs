using UnityEngine;

namespace SquadRush
{
    /// <summary>One visible soldier in the squad. Slides to its formation slot and bobs idly.</summary>
    public class Unit : MonoBehaviour
    {
        public Transform firePoint;
        public Transform visual;

        [HideInInspector] public Vector3 targetLocalPos;
        float phase;
        float baseY;

        void Awake()
        {
            phase = Random.value * Mathf.PI * 2f;
            if (visual != null) baseY = visual.localPosition.y;
        }

        void Update()
        {
            float t = 1f - Mathf.Exp(-12f * Time.deltaTime);
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocalPos, t);

            if (visual != null)
            {
                var p = visual.localPosition;
                p.y = baseY + Mathf.Sin(Time.time * 9f + phase) * 0.04f;
                visual.localPosition = p;
            }
        }
    }
}
