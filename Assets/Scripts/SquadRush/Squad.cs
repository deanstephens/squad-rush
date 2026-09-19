using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SquadRush
{
    /// <summary>
    /// The player's army. Sits at the bottom of the lane, slides left/right with a finger drag,
    /// and every unit fires forward on a shared timer. Unit count is the "health".
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public class Squad : MonoBehaviour
    {
        [Header("Prefabs")]
        public Unit unitPrefab;
        public Projectile projectilePrefab;

        [Header("Formation")]
        public int maxVisibleUnits = 30;
        public int unitsPerRow = 6;
        public float spacing = 0.55f;
        public float laneHalfWidth = 2.6f;

        [Header("Stats (base values come from meta upgrades; gates and perks add to the bonuses)")]
        public int UnitCount;
        public float baseDamage = 2f;
        public float baseFireRate = 2f;
        public float baseMoveSpeed = 9f;
        public float baseProjectileSpeed = 24f;
        [Tooltip("Additive fractions: 0.25 = +25%.")]
        public float damageBonus;
        public float fireRateBonus;
        public float moveSpeedBonus;
        public float projectileSpeedBonus;
        public int pierce = 0;
        public float projectileScale = 1f;
        public int shields = 0;

        public float damage => baseDamage * (1f + damageBonus);
        public float fireRate => baseFireRate * (1f + fireRateBonus);
        public float moveSpeed => Mathf.Min(maxMoveSpeed, baseMoveSpeed * (1f + moveSpeedBonus));
        public float projectileSpeed => baseProjectileSpeed * (1f + projectileSpeedBonus);

        [Header("Aim")]
        [Tooltip("Units nudge their shots toward the nearest enemy inside this half-angle (degrees).")]
        public float aimConeDegrees = 30f;
        public float aimRange = 35f;

        [Header("Input")]
        [Tooltip("Dragging this fraction of the screen width moves the squad across the whole lane.")]
        public float dragScreenFraction = 0.6f;
        [Tooltip("Largest pointer jump (as a fraction of screen width) accepted in one frame; bigger jumps are ignored.")]
        public float maxDragStepFraction = 0.25f;
        public float maxMoveSpeed = 30f;
        public float projectileLifetime = 3f;

        public event Action Changed;

        readonly List<Unit> units = new List<Unit>();
        readonly Stack<Projectile> pool = new Stack<Projectile>();
        Transform projectileRoot;
        BoxCollider hitbox;
        float fireTimer;
        float targetX;
        bool dragging;
        float lastPointerX;

        void Awake()
        {
            hitbox = GetComponent<BoxCollider>();
            hitbox.isTrigger = true;
            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            projectileRoot = new GameObject("Projectiles").transform;
            targetX = transform.position.x;
        }

        /// <summary>Loads persistent upgrades. Safe to call while in the menu.</summary>
        public void ApplyMeta()
        {
            UnitCount = MetaProgression.StartUnits;
            baseDamage = MetaProgression.Damage;
            baseFireRate = MetaProgression.FireRate;
            SyncVisuals();
            Changed?.Invoke();
        }

        public void NotifyChanged() => Changed?.Invoke();

        // ---------------------------------------------------------------- units

        public void AddUnits(int n) => SetUnitCount(UnitCount + n);

        public void MultiplyUnits(float m) => SetUnitCount(Mathf.RoundToInt(UnitCount * m));

        public void TakeHit(int lost)
        {
            if (shields > 0)
            {
                shields--;
                Changed?.Invoke();
                return;
            }
            SetUnitCount(UnitCount - lost);
        }

        public void SetUnitCount(int n)
        {
            UnitCount = Mathf.Max(0, n);
            SyncVisuals();
            Changed?.Invoke();
            if (UnitCount <= 0 && GameManager.Instance != null) GameManager.Instance.GameOver();
        }

        void SyncVisuals()
        {
            int target = Mathf.Min(UnitCount, maxVisibleUnits);

            while (units.Count < target)
            {
                var u = Instantiate(unitPrefab, transform);
                u.transform.localPosition = FormationPos(units.Count, target) + Vector3.back * 1.5f;
                units.Add(u);
            }
            while (units.Count > target)
            {
                var u = units[units.Count - 1];
                units.RemoveAt(units.Count - 1);
                Destroy(u.gameObject);
            }

            for (int i = 0; i < units.Count; i++)
                units[i].targetLocalPos = FormationPos(i, target);

            int rows = Mathf.Max(1, Mathf.CeilToInt(target / (float)unitsPerRow));
            int cols = Mathf.Min(target, unitsPerRow);
            hitbox.size = new Vector3(Mathf.Max(0.6f, cols * spacing + 0.2f), 1.2f, rows * spacing + 0.4f);
            hitbox.center = new Vector3(0f, 0.6f, -(rows - 1) * spacing * 0.5f);
        }

        Vector3 FormationPos(int index, int total)
        {
            int row = index / unitsPerRow;
            int col = index % unitsPerRow;
            int inThisRow = Mathf.Min(unitsPerRow, total - row * unitsPerRow);
            float x = (col - (inThisRow - 1) * 0.5f) * spacing;
            float z = -row * spacing;
            return new Vector3(x, 0f, z);
        }

        // ---------------------------------------------------------------- loop

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;

            HandleInput();
            HandleFire();
        }

        void HandleInput()
        {
            float dt = Time.deltaTime;

            // Position-based drag: the Input System's per-frame delta can contain the jump from the
            // previous touch's position on the first frame of a new touch, which used to fling the
            // squad to a lane edge. Tracking positions ourselves (and ignoring the press frame) avoids that.
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.isPressed)
            {
                float x = pointer.position.ReadValue().x;
                if (!dragging || pointer.press.wasPressedThisFrame)
                {
                    dragging = true;
                }
                else
                {
                    float dx = x - lastPointerX;
                    float maxStep = Screen.width * maxDragStepFraction;
                    if (Mathf.Abs(dx) <= maxStep)
                    {
                        float worldPerPixel = (laneHalfWidth * 2f) / Mathf.Max(1f, Screen.width * dragScreenFraction);
                        targetX += dx * worldPerPixel;
                    }
                }
                lastPointerX = x;
            }
            else
            {
                dragging = false;
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) targetX -= moveSpeed * dt;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) targetX += moveSpeed * dt;
            }

            targetX = Mathf.Clamp(targetX, -laneHalfWidth, laneHalfWidth);

            var p = transform.position;
            p.x = Mathf.MoveTowards(p.x, targetX, moveSpeed * dt);
            transform.position = p;
        }

        void HandleFire()
        {
            fireTimer -= Time.deltaTime;
            if (fireTimer > 0f || units.Count == 0) return;
            fireTimer += 1f / Mathf.Max(0.1f, fireRate);

            // Hidden units (beyond the visible cap) still contribute: spread their damage across the visible shooters.
            float dmgPerShot = damage * UnitCount / units.Count;

            foreach (var u in units)
            {
                var pos = u.firePoint != null ? u.firePoint.position : u.transform.position + Vector3.up * 0.4f;
                Fire(pos, AimFrom(pos), dmgPerShot);
            }
        }

        /// <summary>Straight ahead unless an enemy is inside the aim cone, in which case lean toward the nearest one.</summary>
        Vector3 AimFrom(Vector3 from)
        {
            TreadmillEnemy best = null;
            float bestD = aimRange * aimRange;
            float cosLimit = Mathf.Cos(aimConeDegrees * Mathf.Deg2Rad);
            var all = TreadmillEnemy.All;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (e == null || e.Dead) continue;
                Vector3 to = e.transform.position - from;
                to.y = 0f;
                float d2 = to.sqrMagnitude;
                if (d2 < 0.25f || d2 > bestD) continue;
                if (Vector3.Dot(to / Mathf.Sqrt(d2), Vector3.forward) < cosLimit) continue;
                bestD = d2;
                best = e;
            }
            if (best == null) return Vector3.forward;
            Vector3 dir = best.transform.position - from;
            dir.y = 0f;
            return dir.normalized;
        }

        void Fire(Vector3 position, Vector3 dir, float dmg)
        {
            Projectile p = pool.Count > 0 ? pool.Pop() : Instantiate(projectilePrefab, projectileRoot);
            p.Launch(position, dir, projectileSpeed, dmg, pierce, projectileScale, projectileLifetime, ReturnToPool);
        }

        void ReturnToPool(Projectile p) => pool.Push(p);

        // ---------------------------------------------------------------- contact

        void OnTriggerEnter(Collider other)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;

            var enemy = other.GetComponentInParent<TreadmillEnemy>();
            if (enemy != null)
            {
                enemy.OnSquadContact(this);
                return;
            }

            var gate = other.GetComponentInParent<PowerUpGate>();
            if (gate != null)
            {
                // Only the gate under the squad's centre counts, so side-by-side gates are a real choice.
                if (Mathf.Abs(transform.position.x - gate.transform.position.x) <= gate.HalfWidth)
                    gate.Collect(this);
            }
        }
    }
}
