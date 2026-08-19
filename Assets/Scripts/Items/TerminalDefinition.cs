using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;



[CreateAssetMenu(fileName = "TerminalDefinition", menuName = "Fallout Angles/Terminal Definition")]
public class TerminalDefinition : ScriptableObject
{
    [SerializeField] private string terminalName = "Terminal";
    [SerializeField] private TerminalDocument document = new TerminalDocument();


    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(terminalName))
            terminalName = "Terminal";
    }


    public string GetTerminalName()
    {
        return string.IsNullOrWhiteSpace(terminalName) ? "Terminal" : terminalName.Trim();
    }


    public void SetTerminalName(string newTerminalName)
    {
        terminalName = string.IsNullOrWhiteSpace(newTerminalName) ? "Terminal" : newTerminalName.Trim();
    }


    public TerminalDocument GetDocument()
    {
        return document;
    }
}



[Serializable]
public class TerminalDocument
{
    [TextArea(2, 6)]
    public string terminalTitle = "ROBCO INDUSTRIES UNIFIED OPERATING SYSTEM";
    public string startupPageId = "main";
    public List<TerminalPage> pages = new List<TerminalPage>
    {
        new TerminalPage()
    };


    public TerminalPage GetStartupPage()
    {
        if (TryGetPage(startupPageId, out TerminalPage startupPage))
            return startupPage;

        return pages != null && pages.Count > 0 ? pages[0] : null;
    }


    public bool TryGetPage(string pageId, out TerminalPage page)
    {
        page = null;

        if (pages == null || pages.Count == 0)
            return false;

        string safePageId = string.IsNullOrWhiteSpace(pageId) ? string.Empty : pageId.Trim();

        for (int i = 0; i < pages.Count; i++)
        {
            TerminalPage candidate = pages[i];
            if (candidate == null)
                continue;

            string candidateId = string.IsNullOrWhiteSpace(candidate.pageId) ? string.Empty : candidate.pageId.Trim();
            if (!string.Equals(candidateId, safePageId, StringComparison.OrdinalIgnoreCase))
                continue;

            page = candidate;
            return true;
        }

        return false;
    }
}



[Serializable]
public class TerminalPage
{
    public string pageId = "main";

    [TextArea(4, 14)]
    public string body = "Select an option.";

    public bool includeBackOption;
    public string backOptionLabel = "> Back";

    public bool includeExitOption = true;
    public string exitOptionLabel = "> Log Off";

    public List<TerminalOption> options = new List<TerminalOption>();
}



[Serializable]
public class TerminalOption
{
    public string label = "> Open Page";
    public TerminalOptionAction action = TerminalOptionAction.Navigate;
    public string targetPageId;
    public bool addCurrentPageToHistory = true;

    [TextArea(1, 3)]
    public string promptMessage;

    public UnityEvent onSelected = new UnityEvent();
}



public enum TerminalOptionAction
{
    Navigate,
    Back,
    Close,
    InvokeEvent,
    PromptMessage
}
