using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DialogueDefinition", menuName = "Fallout Angles/Dialogue Definition")]
public class DialogueDefinition : ScriptableObject
{
    [SerializeField] private string dialogueId = "";
    [SerializeField] private string dialogueName = "Dialogue";
    [SerializeField] private string entryTreeId = "main";
    [SerializeField] private List<DialogueTreeDefinition> trees = new List<DialogueTreeDefinition>
    {
        new DialogueTreeDefinition()
    };

    private void OnValidate()
    {
        dialogueId = string.IsNullOrWhiteSpace(dialogueId) ? string.Empty : dialogueId.Trim();
        dialogueName = string.IsNullOrWhiteSpace(dialogueName) ? "Dialogue" : dialogueName.Trim();

        if (trees == null)
            trees = new List<DialogueTreeDefinition>();

        for (int i = 0; i < trees.Count; i++)
        {
            DialogueTreeDefinition tree = trees[i];
            if (tree == null)
            {
                trees[i] = new DialogueTreeDefinition();
                tree = trees[i];
            }

            tree.Sanitize(i);
        }

        if (string.IsNullOrWhiteSpace(entryTreeId))
            entryTreeId = trees.Count > 0 ? trees[0].GetTreeId() : "main";
        else
            entryTreeId = entryTreeId.Trim();
    }

    public string GetDialogueId()
    {
        return dialogueId;
    }

    public string GetDialogueName()
    {
        return dialogueName;
    }

    public string GetEntryTreeId()
    {
        return entryTreeId;
    }

    public List<DialogueTreeDefinition> GetTrees()
    {
        return trees;
    }

    public DialogueTreeDefinition GetEntryTree()
    {
        if (TryGetTree(entryTreeId, out DialogueTreeDefinition tree))
            return tree;

        return trees != null && trees.Count > 0 ? trees[0] : null;
    }

    public bool TryGetTree(string treeId, out DialogueTreeDefinition tree)
    {
        tree = null;

        if (trees == null || trees.Count == 0)
            return false;

        string safeTreeId = string.IsNullOrWhiteSpace(treeId) ? string.Empty : treeId.Trim();

        for (int i = 0; i < trees.Count; i++)
        {
            DialogueTreeDefinition candidate = trees[i];
            if (candidate == null)
                continue;

            if (!string.Equals(candidate.GetTreeId(), safeTreeId, StringComparison.OrdinalIgnoreCase))
                continue;

            tree = candidate;
            return true;
        }

        return false;
    }
}

[Serializable]
public class DialogueTreeDefinition
{
    [SerializeField] private string treeId = "main";
    [SerializeField] private string treeName = "Main";
    [SerializeField] private string startNodeId = "start";
    [SerializeField] private List<DialogueNodeDefinition> nodes = new List<DialogueNodeDefinition>
    {
        new DialogueNodeDefinition()
    };

    public void Sanitize(int treeIndex)
    {
        treeId = string.IsNullOrWhiteSpace(treeId) ? $"tree_{treeIndex}" : treeId.Trim();
        treeName = string.IsNullOrWhiteSpace(treeName) ? $"Tree {treeIndex + 1}" : treeName.Trim();

        if (nodes == null)
            nodes = new List<DialogueNodeDefinition>();

        for (int i = 0; i < nodes.Count; i++)
        {
            DialogueNodeDefinition node = nodes[i];
            if (node == null)
            {
                nodes[i] = new DialogueNodeDefinition();
                node = nodes[i];
            }

            node.Sanitize(i);
        }

        if (string.IsNullOrWhiteSpace(startNodeId))
            startNodeId = nodes.Count > 0 ? nodes[0].GetNodeId() : "start";
        else
            startNodeId = startNodeId.Trim();
    }

    public string GetTreeId()
    {
        return treeId;
    }

    public string GetTreeName()
    {
        return treeName;
    }

    public string GetStartNodeId()
    {
        return startNodeId;
    }

    public List<DialogueNodeDefinition> GetNodes()
    {
        return nodes;
    }

    public DialogueNodeDefinition GetStartNode()
    {
        if (TryGetNode(startNodeId, out DialogueNodeDefinition node))
            return node;

        return nodes != null && nodes.Count > 0 ? nodes[0] : null;
    }

    public bool TryGetNode(string nodeId, out DialogueNodeDefinition node)
    {
        node = null;

        if (nodes == null || nodes.Count == 0)
            return false;

        string safeNodeId = string.IsNullOrWhiteSpace(nodeId) ? string.Empty : nodeId.Trim();

        for (int i = 0; i < nodes.Count; i++)
        {
            DialogueNodeDefinition candidate = nodes[i];
            if (candidate == null)
                continue;

            if (!string.Equals(candidate.GetNodeId(), safeNodeId, StringComparison.OrdinalIgnoreCase))
                continue;

            node = candidate;
            return true;
        }

        return false;
    }
}

[Serializable]
public class DialogueNodeDefinition
{
    [SerializeField] private string nodeId = "start";
    [SerializeField] private string speakerNameOverride = "";
    [TextArea(2, 8)] [SerializeField] private string dialogueText = "";
    [SerializeField] private bool exitDialogueIfNoChoices;
    [SerializeField] private List<DialogueChoiceDefinition> choices = new List<DialogueChoiceDefinition>();

    public void Sanitize(int nodeIndex)
    {
        nodeId = string.IsNullOrWhiteSpace(nodeId) ? $"node_{nodeIndex}" : nodeId.Trim();
        speakerNameOverride = string.IsNullOrWhiteSpace(speakerNameOverride) ? string.Empty : speakerNameOverride.Trim();

        if (choices == null)
            choices = new List<DialogueChoiceDefinition>();

        for (int i = 0; i < choices.Count; i++)
        {
            DialogueChoiceDefinition choice = choices[i];
            if (choice == null)
            {
                choices[i] = new DialogueChoiceDefinition();
                choice = choices[i];
            }

            choice.Sanitize(i);
        }
    }

    public string GetNodeId()
    {
        return nodeId;
    }

    public string GetSpeakerNameOverride()
    {
        return speakerNameOverride;
    }

    public string GetDialogueText()
    {
        return dialogueText;
    }

    public bool ShouldExitDialogueIfNoChoices()
    {
        return exitDialogueIfNoChoices;
    }

    public List<DialogueChoiceDefinition> GetChoices()
    {
        return choices;
    }
}

[Serializable]
public class DialogueChoiceDefinition
{
    [SerializeField] private string choiceId = "choice_0";
    [TextArea(1, 3)] [SerializeField] private string playerText = "Continue.";
    [SerializeField] private string nextNodeId = "";
    [SerializeField] private bool exitDialogue;
    [SerializeField] private List<DialogueExternalActionDefinition> externalActions =
        new List<DialogueExternalActionDefinition>();
    [TextArea(1, 4)] [SerializeField] private string notes = "";

    public void Sanitize(int choiceIndex)
    {
        choiceId = string.IsNullOrWhiteSpace(choiceId) ? $"choice_{choiceIndex}" : choiceId.Trim();
        nextNodeId = string.IsNullOrWhiteSpace(nextNodeId) ? string.Empty : nextNodeId.Trim();

        if (externalActions == null)
            externalActions = new List<DialogueExternalActionDefinition>();
    }

    public string GetChoiceId()
    {
        return choiceId;
    }

    public string GetPlayerText()
    {
        return playerText;
    }

    public string GetNextNodeId()
    {
        return nextNodeId;
    }

    public bool HasNextNode()
    {
        return string.IsNullOrWhiteSpace(nextNodeId) == false;
    }

    public bool ShouldExitDialogue()
    {
        return exitDialogue;
    }

    public List<DialogueExternalActionDefinition> GetExternalActions()
    {
        return externalActions;
    }

    public string GetNotes()
    {
        return notes;
    }
}

[Serializable]
public class DialogueExternalActionDefinition
{
    [SerializeField] private DialogueExternalActionType actionType = DialogueExternalActionType.None;
    [SerializeField] private string targetId = "";
    [SerializeField] private int stageValue;
    [SerializeField] private string stringParameter = "";
    [TextArea(1, 4)] [SerializeField] private string notes = "";

    public DialogueExternalActionType GetActionType()
    {
        return actionType;
    }

    public string GetTargetId()
    {
        return targetId;
    }

    public int GetStageValue()
    {
        return stageValue;
    }

    public string GetStringParameter()
    {
        return stringParameter;
    }

    public string GetNotes()
    {
        return notes;
    }
}

public enum DialogueExternalActionType
{
    None,
    StartQuest,
    SetQuestStage,
    CompleteQuest,
    OpenDoor,
    UnlockDoor,
    CustomSignal,
    FailQuest,
    SetCurrentQuest,
    ClearCurrentQuest,
    DisplayQuestObjective,
    CompleteQuestObjective,
    FailQuestObjective,
    HideQuestObjective,
    SetCurrentQuestObjective
}
