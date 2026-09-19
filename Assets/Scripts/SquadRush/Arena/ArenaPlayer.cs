using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SquadRush.Arena
{
    /// <summary>
    /// The lone survivor. Steered with a virtual joystick (press anywhere, drag) or WASD;
    /// carries the equipped guns which aim and fire on their own.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class ArenaPlayer : MonoBehaviour
    {
        [Header("Wiring")]
        public Transform visual;
        public Renderer bodyRenderer;
        public GameObject gunVisualPrefab;
        public float arenaHalfSize = 29f;

        [Header("Stats (run-time)")]
        public float maxHp = 100f;
        public float hp = 100f;
        public float moveSpeed = 6f;
        public float pickupRadius = 3.5f;
        [Tooltip("Seconds of invulnerability after any hit, so a swarm cannot land twenty hits in one frame.")]
        public float hitInvulnerability = 0.25f;
        public float damageMult = 1f;
        public float fireRateMult = 1f;
        public int extraPierce;
        public float regenPerSecond;
        public float damageTakenMult = 1f;

        [Header("Input")]
        [Tooltip("Drag this fraction of the screen width for full joystick deflection.")]
        public float joystickFraction = 0.12f;

        public float Radius => 0.45f;
        public Gun Gun { get; private set; }

        Rigidbody rb;
        Vector2 pressOrigin;
        bool pressing;
        float flashTimer;
        float invulnTimer;
        Vector3 facing = Vector3.forward;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color BodyColor = new Color(0.3f, 0.65f, 1f);

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        public void EquipGun(GunDef def, GunStats stats)
        {
            if (Gun != null) Destroy(Gun.gameObject);

            var go = gunVisualPrefab != null ? Instantiate(gunVisualPrefab, transform) : new GameObject("Gun");
            go.name = "Gun_" + def.Id;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0.28f, 0.55f, 0.4f);

            var gun = go.GetComponent<Gun>();
            if (gun == null) gun = go.AddComponent<Gun>();
            gun.Init(def, stats, this, go.transform.Find("Muzzle"));

            var tip = go.transform.Find("Tip");
            if (tip != null)
            {
                var r = tip.GetComponent<Renderer>();
                if (r != null)
                {
                    var mpb = new MaterialPropertyBlock();
                    mpb.SetColor(BaseColorId, def.Color);
                    r.SetPropertyBlock(mpb);
                }
            }
            Gun = gun;
        }

        void Update()
        {
            var am = ArenaManager.Instance;
            if (am == null || am.State != ArenaState.Playing) return;

            float dt = Time.deltaTime;
            Vector2 move = ReadMove();

            if (move.sqrMagnitude > 0.0001f)
            {
                Vector3 delta = new Vector3(move.x, 0f, move.y) * (moveSpeed * dt);
                Vector3 p = transform.position + delta;
                p.x = Mathf.Clamp(p.x, -arenaHalfSize, arenaHalfSize);
                p.z = Mathf.Clamp(p.z, -arenaHalfSize, arenaHalfSize);
                p.y = 0f;
                transform.position = p;
                facing = new Vector3(move.x, 0f, move.y).normalized;
            }

            if (visual != null)
                visual.rotation = Quaternion.Slerp(visual.rotation, Quaternion.LookRotation(facing, Vector3.up), 1f - Mathf.Exp(-14f * dt));

            if (regenPerSecond > 0f && hp < maxHp)
            {
                hp = Mathf.Min(maxHp, hp + regenPerSecond * dt);
            }

            if (invulnTimer > 0f) invulnTimer -= dt;

            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                if (flashTimer <= 0f) SetColor(BodyColor);
            }
        }

        Vector2 ReadMove()
        {
            Vector2 move = Vector2.zero;

            var pointer = Pointer.current;
            if (pointer != null && pointer.press.isPressed)
            {
                Vector2 pos = pointer.position.ReadValue();
                if (!pressing || pointer.press.wasPressedThisFrame)
                {
                    pressing = true;
                    pressOrigin = pos;
                }
                else
                {
                    float full = Mathf.Max(1f, Screen.width * joystickFraction);
                    move = Vector2.ClampMagnitude((pos - pressOrigin) / full, 1f);
                    // Let the origin trail the finger so reversing direction is instant.
                    if ((pos - pressOrigin).magnitude > full) pressOrigin = pos - (pos - pressOrigin).normalized * full;
                }
            }
            else pressing = false;

            var kb = Keyboard.current;
            if (kb != null)
            {
                Vector2 k = Vector2.zero;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) k.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) k.x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) k.y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) k.y += 1f;
                if (k.sqrMagnitude > 0f) move = k.normalized;
            }
            return move;
        }

        public void TakeDamage(float amount)
        {
            var am = ArenaManager.Instance;
            if (am == null || am.State != ArenaState.Playing || invulnTimer > 0f) return;
            invulnTimer = hitInvulnerability;
            hp -= amount * damageTakenMult;
            flashTimer = 0.1f;
            SetColor(Color.white);
            am.NotifyHud();
            if (hp <= 0f)
            {
                hp = 0f;
                am.GameOver();
            }
        }

        public void Heal(float amount)
        {
            hp = Mathf.Min(maxHp, hp + amount);
            ArenaManager.Instance?.NotifyHud();
        }

        void SetColor(Color c)
        {
            if (bodyRenderer == null) return;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(BaseColorId, c);
            bodyRenderer.SetPropertyBlock(mpb);
        }
    }
}
