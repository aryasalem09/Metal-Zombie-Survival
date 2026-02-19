using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AnimatorParamAdapter
{
    public static bool enableMissingParameterWarnings = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHooks()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ClearCaches();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ClearCaches();
    }

    public static bool HasBool(Animator animator, string parameterName)
    {
        return ResolveParameterName(animator, parameterName, AnimatorControllerParameterType.Bool) != null;
    }

    public static bool HasTrigger(Animator animator, string parameterName)
    {
        return ResolveParameterName(animator, parameterName, AnimatorControllerParameterType.Trigger) != null;
    }

    public static bool HasFloat(Animator animator, string parameterName)
    {
        return ResolveParameterName(animator, parameterName, AnimatorControllerParameterType.Float) != null;
    }

    public static bool SetBool(Animator animator, string parameterName, bool value)
    {
        string resolvedName = ResolveParameterName(animator, parameterName, AnimatorControllerParameterType.Bool);
        if (resolvedName == null)
        {
            WarnMissing(animator, parameterName, AnimatorControllerParameterType.Bool);
            return false;
        }

        string valueKey = BuildValueCacheKey(animator, resolvedName, AnimatorControllerParameterType.Bool);
        if (BoolValueCache.TryGetValue(valueKey, out bool cachedValue) && cachedValue == value)
        {
            return true;
        }

        animator.SetBool(resolvedName, value);
        BoolValueCache[valueKey] = value;
        return true;
    }

    public static bool SetFloat(Animator animator, string parameterName, float value)
    {
        string resolvedName = ResolveParameterName(animator, parameterName, AnimatorControllerParameterType.Float);
        if (resolvedName == null)
        {
            WarnMissing(animator, parameterName, AnimatorControllerParameterType.Float);
            return false;
        }

        string valueKey = BuildValueCacheKey(animator, resolvedName, AnimatorControllerParameterType.Float);
        if (FloatValueCache.TryGetValue(valueKey, out float cachedValue) && Mathf.Abs(cachedValue - value) <= 0.0001f)
        {
            return true;
        }

        animator.SetFloat(resolvedName, value);
        FloatValueCache[valueKey] = value;
        return true;
    }

    public static bool SetTrigger(Animator animator, string parameterName)
    {
        string triggerName = ResolveParameterName(animator, parameterName, AnimatorControllerParameterType.Trigger);
        if (triggerName != null)
        {
            animator.SetTrigger(triggerName);
            return true;
        }

        string boolName = ResolveParameterName(animator, parameterName, AnimatorControllerParameterType.Bool);
        if (boolName != null)
        {
            animator.SetBool(boolName, true);
            return true;
        }

        WarnMissing(animator, parameterName, AnimatorControllerParameterType.Trigger);
        return false;
    }

    public static bool ResetTrigger(Animator animator, string parameterName)
    {
        string resolvedName = ResolveParameterName(animator, parameterName, AnimatorControllerParameterType.Trigger);
        if (resolvedName == null)
        {
            WarnMissing(animator, parameterName, AnimatorControllerParameterType.Trigger);
            return false;
        }

        animator.ResetTrigger(resolvedName);
        return true;
    }

    private static readonly HashSet<string> MissingParameterWarnings = new HashSet<string>();
    private static readonly Dictionary<string, string> ResolvedParameterCache = new Dictionary<string, string>();
    private static readonly Dictionary<string, bool> BoolValueCache = new Dictionary<string, bool>();
    private static readonly Dictionary<string, float> FloatValueCache = new Dictionary<string, float>();

    private static void ClearCaches()
    {
        MissingParameterWarnings.Clear();
        ResolvedParameterCache.Clear();
        BoolValueCache.Clear();
        FloatValueCache.Clear();
    }

    private static string ResolveParameterName(
        Animator animator,
        string parameterName,
        AnimatorControllerParameterType expectedType)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return null;
        }

        string cacheKey = BuildCacheKey(animator, parameterName, expectedType);
        if (ResolvedParameterCache.TryGetValue(cacheKey, out string cachedName))
        {
            if (cachedName == null)
            {
                return null;
            }

            if (HasParameter(animator, cachedName, expectedType))
            {
                return cachedName;
            }

            ResolvedParameterCache.Remove(cacheKey);
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == expectedType && parameter.name == parameterName)
            {
                ResolvedParameterCache[cacheKey] = parameter.name;
                return parameter.name;
            }
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == expectedType &&
                string.Equals(parameter.name, parameterName, StringComparison.OrdinalIgnoreCase))
            {
                ResolvedParameterCache[cacheKey] = parameter.name;
                return parameter.name;
            }
        }

        string normalizedTarget = NormalizeParameterName(parameterName);
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type != expectedType)
            {
                continue;
            }

            if (NormalizeParameterName(parameter.name) == normalizedTarget)
            {
                ResolvedParameterCache[cacheKey] = parameter.name;
                return parameter.name;
            }
        }

        string trimmedNumericTarget = NormalizeParameterName(TrimNumericSuffix(parameterName));
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type != expectedType)
            {
                continue;
            }

            string candidate = NormalizeParameterName(TrimNumericSuffix(parameter.name));
            if (candidate == trimmedNumericTarget)
            {
                ResolvedParameterCache[cacheKey] = parameter.name;
                return parameter.name;
            }
        }

        ResolvedParameterCache[cacheKey] = null;
        return null;
    }

    private static void WarnMissing(
        Animator animator,
        string parameterName,
        AnimatorControllerParameterType expectedType)
    {
        if (!enableMissingParameterWarnings)
        {
            return;
        }

        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return;
        }

        string key = BuildCacheKey(animator, parameterName, expectedType);
        if (!MissingParameterWarnings.Add(key))
        {
            return;
        }

        Debug.LogWarning(
            $"AnimatorParamAdapter: Missing {expectedType} parameter '{parameterName}' on '{animator.name}'.");
    }

    private static string BuildCacheKey(
        Animator animator,
        string parameterName,
        AnimatorControllerParameterType expectedType)
    {
        int controllerId = 0;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            controllerId = animator.runtimeAnimatorController.GetInstanceID();
        }

        if (controllerId != 0)
        {
            return "controller:" + controllerId + "|" + expectedType + "|" + parameterName;
        }

        int animatorId = animator != null ? animator.GetInstanceID() : 0;
        return "animator:" + animatorId + "|" + expectedType + "|" + parameterName;
    }

    private static string BuildValueCacheKey(
        Animator animator,
        string parameterName,
        AnimatorControllerParameterType expectedType)
    {
        int animatorId = animator != null ? animator.GetInstanceID() : 0;
        return "animator-value:" + animatorId + "|" + expectedType + "|" + parameterName;
    }

    private static bool HasParameter(
        Animator animator,
        string parameterName,
        AnimatorControllerParameterType expectedType)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == expectedType && parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeParameterName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static string TrimNumericSuffix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        int endIndex = value.Length - 1;
        while (endIndex >= 0 && char.IsWhiteSpace(value[endIndex]))
        {
            endIndex--;
        }

        while (endIndex >= 0 && char.IsDigit(value[endIndex]))
        {
            endIndex--;
        }

        while (endIndex >= 0 && char.IsWhiteSpace(value[endIndex]))
        {
            endIndex--;
        }

        return endIndex >= 0 ? value.Substring(0, endIndex + 1) : string.Empty;
    }
}
