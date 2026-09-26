using UnityEngine;

namespace SquadRush
{
    /// <summary>Tints and hit-flashes textured models through a shared MaterialPropertyBlock (no material instances).</summary>
    public static class RendererTint
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static MaterialPropertyBlock mpb;

        public static void Apply(Renderer[] renderers, Color baseColor, Color emission)
        {
            if (renderers == null) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, baseColor);
                mpb.SetColor(EmissionId, emission);
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
