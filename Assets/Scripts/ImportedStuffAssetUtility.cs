using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class ImportedStuffAssetUtility
{
    private const string ImportedRootFolder = "Assets/Imported Stuff";
    private const string GameplayFontPath = "Assets/Imported Stuff/FONT/Assets/font/BoldPixels SDF.asset";
    private const string FallbackUiFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string PaperNpcPrefabPath = "Assets/Imported Stuff/PAPER NPC/book.FBX";
    private const string TutorialBackgroundTexturePath = "Assets/Imported Stuff/Tutorial Background.png";
    private const string PaperPanelTexturePath = "Assets/Imported Stuff/PAPER NPC/bookscreenshot2.jpg";

    private static TMP_FontAsset cachedGameplayFont;
    private static GameObject cachedPaperNpcPrefab;
    private static Texture2D cachedTutorialBackgroundTexture;
    private static Sprite cachedTutorialBackgroundSprite;
    private static Texture2D cachedPaperPanelTexture;
    private static Sprite cachedPaperPanelSprite;

    private static readonly Dictionary<string, AudioClip> AudioClipCache =
        new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);

    public static TMP_FontAsset GetGameplayFont()
    {
        if (IsUsableFont(cachedGameplayFont))
        {
            return cachedGameplayFont;
        }

#if UNITY_EDITOR
        cachedGameplayFont = LoadAssetAtPath<TMP_FontAsset>(FallbackUiFontPath);
#endif

        if (!IsUsableFont(cachedGameplayFont))
        {
            cachedGameplayFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        if (!IsUsableFont(cachedGameplayFont))
        {
            cachedGameplayFont = TMP_Settings.defaultFontAsset;
        }

#if UNITY_EDITOR
        if (!IsUsableFont(cachedGameplayFont))
        {
            TMP_FontAsset importedFont = LoadAssetAtPath<TMP_FontAsset>(GameplayFontPath);
            if (IsUsableFont(importedFont))
            {
                cachedGameplayFont = importedFont;
            }
        }
#endif

        if (!IsUsableFont(cachedGameplayFont))
        {
            TMP_FontAsset[] loadedFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            for (int i = 0; i < loadedFonts.Length; i++)
            {
                if (IsUsableFont(loadedFonts[i]))
                {
                    cachedGameplayFont = loadedFonts[i];
                    break;
                }
            }
        }

        return cachedGameplayFont;
    }

    public static TMP_FontAsset ResolveUsableFont(TMP_FontAsset preferredFont = null)
    {
        if (IsUsableFont(preferredFont))
        {
            return preferredFont;
        }

        TMP_FontAsset gameplayFont = GetGameplayFont();
        if (IsUsableFont(gameplayFont))
        {
            return gameplayFont;
        }

        if (IsUsableFont(TMP_Settings.defaultFontAsset))
        {
            return TMP_Settings.defaultFontAsset;
        }

        TMP_FontAsset fallbackFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (IsUsableFont(fallbackFont))
        {
            return fallbackFont;
        }

        TMP_FontAsset[] loadedFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < loadedFonts.Length; i++)
        {
            if (IsUsableFont(loadedFonts[i]))
            {
                return loadedFonts[i];
            }
        }

        return null;
    }

    public static void ApplyUsableFont(TMP_Text text, TMP_FontAsset preferredFont = null)
    {
        if (text == null)
        {
            return;
        }

        TMP_FontAsset resolvedFont = ResolveUsableFont(preferredFont);
        if (!IsUsableFont(resolvedFont))
        {
            return;
        }

        if (text.font != resolvedFont)
        {
            text.font = resolvedFont;
        }

        Material fontMaterial = resolvedFont.material;
        if (fontMaterial != null && text.fontSharedMaterial != fontMaterial)
        {
            text.fontSharedMaterial = fontMaterial;
        }

        text.havePropertiesChanged = true;
        text.ForceMeshUpdate();
    }

    public static GameObject GetPaperNpcPrefab()
    {
        if (cachedPaperNpcPrefab != null)
        {
            return cachedPaperNpcPrefab;
        }

#if UNITY_EDITOR
        cachedPaperNpcPrefab = LoadAssetAtPath<GameObject>(PaperNpcPrefabPath);
#endif

        if (cachedPaperNpcPrefab == null)
        {
            cachedPaperNpcPrefab = FindLoadedAssetByName<GameObject>("book") ??
                                   FindLoadedAssetByName<GameObject>("grimoire");
        }

        return cachedPaperNpcPrefab;
    }

    public static Sprite GetTutorialBackgroundSprite()
    {
        return GetSpriteFromTexture(
            ref cachedTutorialBackgroundTexture,
            ref cachedTutorialBackgroundSprite,
            TutorialBackgroundTexturePath);
    }

    public static Sprite GetPaperPanelSprite()
    {
        return GetSpriteFromTexture(
            ref cachedPaperPanelTexture,
            ref cachedPaperPanelSprite,
            PaperPanelTexturePath);
    }

    public static AudioClip GetAudioClip(string clipNameWithoutExtension)
    {
        string normalizedName = NormalizeName(clipNameWithoutExtension);
        if (string.IsNullOrEmpty(normalizedName))
        {
            return null;
        }

        if (AudioClipCache.TryGetValue(normalizedName, out AudioClip cachedClip) && cachedClip != null)
        {
            return cachedClip;
        }

        AudioClip clip = null;

#if UNITY_EDITOR
        clip = LoadAudioClipFromImportedFolder(normalizedName);
#endif

        if (clip == null)
        {
            clip = FindLoadedAudioClip(normalizedName);
        }

        AudioClipCache[normalizedName] = clip;
        return clip;
    }

    private static AudioClip FindLoadedAudioClip(string normalizedName)
    {
        AudioClip[] loadedClips = Resources.FindObjectsOfTypeAll<AudioClip>();
        AudioClip partialMatch = null;

        for (int i = 0; i < loadedClips.Length; i++)
        {
            AudioClip clip = loadedClips[i];
            if (clip == null)
            {
                continue;
            }

            string clipName = NormalizeName(clip.name);
            if (clipName == normalizedName)
            {
                return clip;
            }

            if (partialMatch == null && clipName.Contains(normalizedName))
            {
                partialMatch = clip;
            }
        }

        return partialMatch;
    }

    private static bool IsUsableFont(TMP_FontAsset fontAsset)
    {
        if (fontAsset == null)
        {
            return false;
        }

        if (fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
        {
            Texture atlas = fontAsset.atlasTextures[i];
            if (atlas != null)
            {
                return true;
            }
        }

        return false;
    }

    private static T FindLoadedAssetByName<T>(string normalizedName) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(normalizedName))
        {
            return null;
        }

        T[] loadedAssets = Resources.FindObjectsOfTypeAll<T>();
        T partialMatch = null;

        for (int i = 0; i < loadedAssets.Length; i++)
        {
            T asset = loadedAssets[i];
            if (asset == null)
            {
                continue;
            }

            string assetName = NormalizeName(asset.name);
            if (assetName == normalizedName)
            {
                return asset;
            }

            if (partialMatch == null && assetName.Contains(normalizedName))
            {
                partialMatch = asset;
            }
        }

        return partialMatch;
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim();
        trimmed = Path.GetFileNameWithoutExtension(trimmed);

        return trimmed
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .ToLowerInvariant();
    }

    private static Sprite GetSpriteFromTexture(
        ref Texture2D cachedTexture,
        ref Sprite cachedSprite,
        string editorPath)
    {
        if (cachedSprite != null)
        {
            return cachedSprite;
        }

#if UNITY_EDITOR
        if (cachedTexture == null)
        {
            cachedTexture = LoadAssetAtPath<Texture2D>(editorPath);
        }
#endif

        if (cachedTexture == null)
        {
            string normalizedName = NormalizeName(Path.GetFileNameWithoutExtension(editorPath));
            cachedTexture = FindLoadedAssetByName<Texture2D>(normalizedName);
        }

        if (cachedTexture == null)
        {
            return null;
        }

        cachedSprite = Sprite.Create(
            cachedTexture,
            new Rect(0f, 0f, cachedTexture.width, cachedTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        cachedSprite.name = cachedTexture.name + "_RuntimeSprite";
        cachedSprite.hideFlags = HideFlags.DontSave;
        return cachedSprite;
    }

#if UNITY_EDITOR
    private static T LoadAssetAtPath<T>(string path) where T : UnityEngine.Object
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private static AudioClip LoadAudioClipFromImportedFolder(string normalizedName)
    {
        string[] guids = AssetDatabase.FindAssets(normalizedName + " t:AudioClip", new[] { ImportedRootFolder });
        if (guids == null || guids.Length == 0)
        {
            return null;
        }

        AudioClip partialMatch = null;
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                continue;
            }

            AudioClip candidate = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (candidate == null)
            {
                continue;
            }

            string candidateName = NormalizeName(candidate.name);
            if (candidateName == normalizedName)
            {
                return candidate;
            }

            if (partialMatch == null && candidateName.Contains(normalizedName))
            {
                partialMatch = candidate;
            }
        }

        return partialMatch;
    }
#endif
}
