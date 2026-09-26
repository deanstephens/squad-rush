using UnityEngine;

namespace SquadRush
{
    /// <summary>One visible soldier in the squad. Slides to its formation slot; animated when it has an Animator.</summary>
    public class Unit : MonoBehaviour
    {
        public Transform firePoint;
        public Transform visual;
        public Animator animator;

        [HideInInspector] public Vector3 targetLocalPos;
        float phase;
        float baseY;

        static readonly int ShootingId = Animator.StringToHash("Shooting");
        static readonly int CheerId = Animator.StringToHash("Cheer");

        void Awake()
        {
            phase = Random.value * Mathf.PI * 2f;
            if (visual != null) baseY = visual.localPosition.y;
            if (animator != null)
            {
                // Desync the squad so it does not move like one creature.
                animator.speed = Random.Range(0.9f, 1.1f);
                animator.Play("Idle", 0, Random.value);
            }
        }

        void Update()
        {
            float t = 1f - Mathf.Exp(-12f * Time.deltaTime);
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocalPos, t);

            if (animator == null && visual != null)
            {
                var p = visual.localPosition;
                p.y = baseY + Mathf.Sin(Time.time * 9f + phase) * 0.04f;
                visual.localPosition = p;
            }
        }

        public void SetShooting(bool on)
        {
            if (animator != null) animator.SetBool(ShootingId, on);
        }

        public void Cheer()
        {
            if (animator != null) animator.SetTrigger(CheerId);
        }
    }
}
