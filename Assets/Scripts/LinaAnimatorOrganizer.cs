#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Editor-only: Unity performs all controller and backup asset changes from this menu.
public static class LinaAnimatorOrganizer
{
    private const string ControllerPath = "Assets/Animations/Lina/Lina.controller";
    private static readonly string[] ActionParameters =
        { "Ziplining", "GroundPounding", "Gliding", "Jumping" };

    [MenuItem("Tools/Lina/Organize Existing Animator")]
    private static void Organize()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play Mode before organizing Lina's Animator.");
            return;
        }

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null || controller.layers.Length != 1)
        {
            Debug.LogError("Expected Lina.controller with one animation layer at " + ControllerPath);
            return;
        }

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        if (machine.stateMachines.Length != 0)
        {
            Debug.LogError("Lina's controller has sub-state machines. Organization stopped to preserve them.");
            return;
        }

        var actions = new Dictionary<AnimatorState, int>();
        var variants = new HashSet<string>();
        AnimatorState defaultRun = null;
        foreach (ChildAnimatorState child in machine.states)
        {
            AnimatorState state = child.state;
            int action = GetAction(state.name);
            bool withoutBackpack = state.name.EndsWith("_withoutbackpack", StringComparison.OrdinalIgnoreCase);
            if (action < 0 || state.motion == null || !variants.Add(action + ":" + withoutBackpack))
            {
                Debug.LogError("Unrecognized, duplicate, or empty animation state: " + state.name +
                    ". No changes were made.");
                return;
            }
            actions.Add(state, action);
            if (action == 4 && !withoutBackpack)
                defaultRun = state;
        }

        if (defaultRun == null)
        {
            Debug.LogError("The normal Run state is missing. No changes were made.");
            return;
        }

        foreach (string parameter in new[] { "Ziplining", "GroundPounding", "Gliding", "Jumping", "BackpackBulwarkActive" })
        {
            foreach (AnimatorControllerParameter existing in controller.parameters)
            {
                if (existing.name == parameter && existing.type != AnimatorControllerParameterType.Bool)
                {
                    Debug.LogError(parameter + " must be a Bool. No changes were made.");
                    return;
                }
            }
        }

        string backupPath = AssetDatabase.GenerateUniqueAssetPath(
            "Assets/Animations/Lina/Lina_before_organization.controller");
        AssetDatabase.SaveAssets();
        if (!AssetDatabase.CopyAsset(ControllerPath, backupPath))
        {
            Debug.LogError("Could not back up Lina's controller. No changes were made.");
            return;
        }

        foreach (string parameter in new[] { "Ziplining", "GroundPounding", "Gliding", "Jumping", "BackpackBulwarkActive" })
        {
            bool found = false;
            foreach (AnimatorControllerParameter existing in controller.parameters)
                found |= existing.name == parameter;
            if (!found)
                controller.AddParameter(parameter, AnimatorControllerParameterType.Bool);
        }

        foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
            machine.RemoveAnyStateTransition(transition);

        foreach (ChildAnimatorState child in machine.states)
        {
            AnimatorState state = child.state;
            foreach (AnimatorStateTransition transition in state.transitions)
                state.RemoveTransition(transition);

            int action = actions[state];
            bool withoutBackpack = state.name.EndsWith("_withoutbackpack", StringComparison.OrdinalIgnoreCase);
            AnimatorStateTransition entry = machine.AddAnyStateTransition(state);
            entry.name = "Select " + state.name;
            entry.hasExitTime = false;
            entry.hasFixedDuration = true;
            entry.duration = 0f;
            entry.canTransitionToSelf = false;

            // Priority: zipline, ground pound, glide, jump, then run.
            for (int index = 0; index < ActionParameters.Length && index <= action; index++)
                entry.AddCondition(index == action ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                    0f, ActionParameters[index]);
            entry.AddCondition(withoutBackpack ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                0f, "BackpackBulwarkActive");

            int row = action == 4 ? 0 : action + 1;
            ChildAnimatorState[] children = machine.states;
            for (int index = 0; index < children.Length; index++)
                if (children[index].state == state)
                    children[index].position = new Vector3(withoutBackpack ? 560 : 260, row * 110);
            machine.states = children;
            EditorUtility.SetDirty(state);
        }

        machine.defaultState = defaultRun;
        machine.anyStatePosition = new Vector3(0, 160);
        machine.entryPosition = new Vector3(0, 0);
        EditorUtility.SetDirty(machine);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Selection.activeObject = controller;
        EditorGUIUtility.PingObject(controller);
        Debug.Log("Organized Lina's existing " + actions.Count + " animation states. Backup: " + backupPath, controller);
        string groundPoundStatus = variants.Contains("1:False") && variants.Contains("1:True")
            ? "Both ground-pound variants are linked to GroundPounding."
            : "Add both ground-pound states with their clips, then run this menu again to link them.";
        EditorUtility.DisplayDialog("Lina Animator organized",
            "Existing clips and states preserved. Transitions now use Any State, with normal and backpack-away animations in two columns.\n\n" +
            "Backup saved at:\n" + backupPath +
            "\n\n" + groundPoundStatus, "OK");
    }

    private static int GetAction(string stateName)
    {
        string name = stateName.ToLowerInvariant().Replace("_withoutbackpack", "");
        if (name.StartsWith("zipline")) return 0;
        if (name.Replace(" ", "").Replace("_", "").Replace("-", "").StartsWith("groundpound")) return 1;
        if (name == "glide") return 2;
        if (name == "jump") return 3;
        if (name == "run") return 4;
        return -1;
    }
}
#endif
