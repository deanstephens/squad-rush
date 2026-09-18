using UnityEngine;

namespace SquadRush
{
    /// <summary>Tiny effects helper: spawns a burst of physics cubes when something is destroyed.</summary>
    public static class Fx
    {
        public static Material DebrisMaterial;

        public static void Burst(Vector3 position, Color color, int count, float size = 0.18f)
        {
            for (int i = 0; i < count; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Debris";
                cube.transform.position = position + Random.insideUnitSphere * 0.3f;
                cube.transform.localScale = Vector3.one * size * Random.Range(0.6f, 1.3f);
                cube.transform.rotation = Random.rotation;

                var r = cube.GetComponent<Renderer>();
                if (DebrisMaterial != null) r.sharedMaterial = DebrisMaterial;
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_BaseColor", color);
                r.SetPropertyBlock(mpb);

                var rb = cube.AddComponent<Rigidbody>();
                rb.linearVelocity = Random.insideUnitSphere * 4f + Vector3.up * 4f;
                rb.angularVelocity = Random.insideUnitSphere * 8f;

                Object.Destroy(cube, Random.Range(0.6f, 1.1f));
            }
        }
    }
}
