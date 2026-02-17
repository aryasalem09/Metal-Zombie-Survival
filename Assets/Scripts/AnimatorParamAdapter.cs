using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class AnimatorParamAdapter
{
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

        animator.SetBool(resolvedName, value);
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

        animator.SetFloat(resolvedName, value);
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
            return cachedName;
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
        int animatorId = animator != null ? animator.GetInstanceID() : 0;
        return animatorId + "|" + expectedType + "|" + parameterName;
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