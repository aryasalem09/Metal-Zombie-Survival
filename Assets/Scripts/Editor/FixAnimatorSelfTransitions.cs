#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// One-click tool that disables "Can Transition To Self" on every transition
/// in every AnimatorController under Assets/.
///
/// Run via  Tools ➜ Fix Animator Self-Transitions  in the Unity menu bar.
///
/// WHY: When CanTransitionToSelf is true the animator can restart the same
///      clip every frame that the transition condition is satisfied, which
///      causes visible flicker / stuttering on sprite-sheet animations.
/// </summary>
public static class FixAnimatorSelfTransitions
{
    [MenuItem("Tools/Fix Animator Self-Transitions")]
    public static void FixAll()
    {
        string[] controllerGuids = AssetDatabase.FindAssets("t:AnimatorController", new[] { "Assets" });

        int totalFixed = 0;
        int controllersModified = 0;

        foreach (string guid in controllerGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) continue;

            int fixedInController = 0;

            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                fixedInController += FixStateMachine(layer.stateMachine);
            }

            if (fixedInController > 0)
            {
                EditorUtility.SetDirty(controller);
                controllersModified++;
                totalFixed += fixedInController;
                Debug.Log($"[FixAnimatorSelfTransitions] {path}: fixed {fixedInController} transitions.");
            }
        }

        if (totalFixed > 0)
        {
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"[FixAnimatorSelfTransitions] Done. Fixed {totalFixed} transitions across {controllersModified} controllers.");
        EditorUtility.DisplayDialog(
            "Fix Animator Self-Transitions",
            $"Done!\n\nFixed {totalFixed} transitions across {controllersModified} controller(s).",
            "OK");
    }

    private static int FixStateMachine(AnimatorStateMachine stateMachine)
    {
        if (stateMachine == null) return 0;

        int count = 0;

        // Any-State transitions
        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
        {
            if (transition.canTransitionToSelf)
            {
                transition.canTransitionToSelf = false;
                count++;
            }
        }

        // Per-state transitions
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            AnimatorState state = childState.state;
            if (state == null) continue;

            foreach (AnimatorStateTransition transition in state.transitions)
            {
                if (transition.canTransitionToSelf)
                {
                    transition.canTransitionToSelf = false;
                    count++;
                }
            }
        }

        // Recurse into sub-state-machines
        foreach (ChildAnimatorStateMachine childMachine in stateMachine.stateMachines)
        {
            count += FixStateMachine(childMachine.stateMachine);
        }

        return count;
    }
}
#endif
