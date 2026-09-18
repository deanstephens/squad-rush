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

        [Header("Stats (run-time, modified by gates and perks)")]
        public int UnitCount;
        public float damage = 2f;
        public float fireRate = 2f;
        public float projectileSpeed = 24f;
        public float moveSpeed = 9f;
        public int pierce = 0;
        public float projectileScale = 1f;
        public int shields = 0;

        [Header("Input")]
        [Tooltip("World units moved per pixel of drag.")]
        public float dragSensitivity = 0.012f;
        public float projectileLifetime = 3f;

        public event Action Changed;

        readonly List<Unit> units = new List<Unit>();
        readonly Stack<Projectile> pool = new Stack<Projectile>();
        Transform projectileRoot;
        BoxCollider hitbox;
        float fireTimer;
        float targetX;
        int visibleUnitsThisVolley;

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
            damage = MetaProgression.Damage;
            fireRate = MetaProgression.FireRate;
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

            var pointer = Pointer.current;
            if (pointer != null && pointer.press.isPressed)
                targetX += pointer.delta.ReadValue().x * dragSensitivity;

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
                Fire(pos, dmgPerShot);
            }
        }

        void Fire(Vector3 position, float dmg)
        {
            Projectile p = pool.Count > 0 ? pool.Pop() : Instantiate(projectilePrefab, projectileRoot);
            p.Launch(position, projectileSpeed, dmg, pierce, projectileScale, projectileLifetime, ReturnToPool);
        }

        void ReturnToPool(Projectile p) => pool.Push(p);

        // ---------------------------------------------------------------- contact

        void OnTriggerEnter(Collider other)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;

            var ob = other.GetComponentInParent<Obstacle>();
            if (ob != null)
            {
                ob.OnSquadContact(this);
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
