using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SquadRush.EditorTools
{
    /// <summary>Import settings for everything produced by Tools/blender/process_models.py and Tools/audio/generate_sfx.py.</summary>
    public class ArtImportRules : AssetPostprocessor
    {
        const string ModelRoot = "Assets/Art/Models/";
        static readonly string[] OneShots = { "Cheer", "Hit_A", "Death_A", "Taunt", "Spellcast_Shoot" };

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelRoot)) return;
            var mi = (ModelImporter)assetImporter;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.bakeAxisConversion = true;
            mi.isReadable = false;

            bool characters = assetPath.Contains("/Characters/");
            bool anims = assetPath.Contains("/Animations/");
            if (characters || anims)
            {
                mi.animationType = ModelImporterAnimationType.Generic;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importAnimation = anims;
                mi.animationCompression = ModelImporterAnimationCompression.Optimal;
            }
            else
            {
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
            }
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(ModelRoot + "Animations/")) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.defaultClipAnimations;
            foreach (var c in clips)
            {
                string n = c.name.Contains("|") ? c.name.Substring(c.name.LastIndexOf('|') + 1) : c.name;
                c.name = n;
                c.takeName = c.takeName;
                c.loopTime = !OneShots.Contains(n);
                c.loopPose = c.loopTime;
                c.lockRootPositionXZ = true;
                c.keepOriginalPositionXZ = true;
                c.keepOriginalPositionY = true;
                c.keepOriginalOrientation = true;
            }
            mi.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            var ti = (TextureImporter)assetImporter;
            if (assetPath.StartsWith("Assets/Art/Textures/"))
            {
                ti.maxTextureSize = 512;
                ti.mipmapEnabled = true;
                ti.filterMode = FilterMode.Bilinear;
                ti.textureCompression = TextureImporterCompression.Compressed;
            }
            else if (assetPath.StartsWith("Assets/Art/Icons/"))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.maxTextureSize = 256;
            }
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Audio/")) return;
            var ai = (AudioImporter)assetImporter;
            ai.forceToMono = true;
            var s = ai.defaultSampleSettings;
            bool music = assetPath.Contains("/Music/");
            s.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = music ? 0.5f : 0.7f;
            s.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            s.sampleRateOverride = music ? 44100u : 22050u;
            ai.defaultSampleSettings = s;
        }
    }
}
