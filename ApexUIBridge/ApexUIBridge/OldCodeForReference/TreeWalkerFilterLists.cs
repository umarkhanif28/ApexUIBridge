using FlaUI.Core;
using FlaUI.Core.Definitions;

using System;
using System.Collections.Generic;
using System.Text;

public sealed class TreeWalkerFilterLists
{
    public bool showDefaultDebugOutput { get; set; } = false;

    private readonly ITreeWalker _baseWalker;

    public HashSet<string> ExclusionKeywords;
    public HashSet<string> InclusionKeywords;
    public List<ControlType> ControlsToExclude;
    public List<ControlType> ControlsToInclude;

    public TreeWalkerFilterLists(
    IEnumerable<string>? keywordsToExclude = null,
    IEnumerable<string>? keywordsToInclude = null,
    IEnumerable<ControlType>? controlsToInclude = null,
    IEnumerable<ControlType>? controlsToExclude = null,
    bool enableDebugMessages = false,
    bool loadDefaults = true)
    {
        showDefaultDebugOutput = enableDebugMessages;

        // Always initialize HashSets to prevent null reference exceptions
        // This is required before SetDefaults() can call ClearAllLists()
        ControlsToInclude = new List<ControlType>(controlsToInclude ?? Enumerable.Empty<ControlType>());
        ControlsToExclude = new List<ControlType>(controlsToExclude ?? Enumerable.Empty<ControlType>());
        ExclusionKeywords = new HashSet<string>(keywordsToExclude ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        InclusionKeywords = new HashSet<string>(keywordsToInclude ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        if (loadDefaults)
        {
            SetDefaults();
        }
    }

    public void SetIncludeControls(IEnumerable<ControlType> controlsToInclude) =>
        ControlsToInclude = new List<ControlType>(controlsToInclude ??
            Enumerable.Empty<ControlType>());

    public void SetExcludeControls(IEnumerable<ControlType> controlsToExclude) =>
        ControlsToExclude = new List<ControlType>(controlsToExclude ??
            Enumerable.Empty<ControlType>());

    public void SetIncludeKeywords(IEnumerable<string> keywordsToInclude) =>
        InclusionKeywords = new HashSet<string>(keywordsToInclude ??
            Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

    public void SetExcludeKeywords(IEnumerable<string> keywordsToExclude) =>
        ExclusionKeywords = new HashSet<string>(keywordsToExclude ??
            Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);


    public void ClearIncludeControls() => ControlsToInclude.Clear();

    public void ClearExcludeControls() => ControlsToExclude.Clear();

    public void ClearIncludeKeywords() => InclusionKeywords.Clear();

    public void ClearExcludeKeywords() => ExclusionKeywords.Clear();

    public void SetDefaults()
    {
        ClearAllLists();
 
        ControlsToInclude = new List<ControlType> {
            ControlType.Button,
            ControlType.CheckBox,
            ControlType.ComboBox,
            ControlType.DataGrid,
            ControlType.Document,
            ControlType.Edit,
            ControlType.Group,
            ControlType.Hyperlink,
            ControlType.List,
            ControlType.ListItem,
            ControlType.Menu,
            ControlType.MenuBar,
            ControlType.MenuItem,
            ControlType.Slider,
            ControlType.Spinner,
            ControlType.StatusBar,
            ControlType.ScrollBar,
            ControlType.Tab,
            ControlType.TitleBar,
            ControlType.ToolTip,
            ControlType.ToolBar,
            ControlType.TabItem,
            ControlType.Pane,
            ControlType.Window,
            ControlType.Image,        // Mostly needed for the web browser.
            ControlType.AppBar,       // Added
            ControlType.Calendar,     // Added
            ControlType.Custom,       // Added
            ControlType.DataItem,     // Added
            ControlType.Header,       // Added
            ControlType.HeaderItem,   // Added
            ControlType.ProgressBar,  // Added
            ControlType.RadioButton,  // Added
            ControlType.SemanticZoom, // Added
            ControlType.Separator,    // Added 
            ControlType.SplitButton,  // Added
            ControlType.Table,        // Added
            ControlType.Text,         // Added
            ControlType.Thumb,        // Added //
            ControlType.Tree,         // Added
            ControlType.TreeItem,     // Added
            ControlType.Unknown       // Might help the browser load more elements.
            };


 
        ControlsToExclude = new List<ControlType> {
            ControlType.Separator,
            //ControlType.Unknown
            };



        InclusionKeywords = new HashSet<string>(new HashSet<string>
        {
            //"notepad",
            //"wordpad",
            //"calculator",
            //"FlaUI WinForms Test App"
            }, StringComparer.OrdinalIgnoreCase);



        ExclusionKeywords = new HashSet<string>(new HashSet<string>
        {
            "explorer",
            "Claude",
            "devenv",
            "VS Code",
            "Visual Studio",
            "Resource Monitor",
            "Task Manager",
            "FormClient",
            "FlaUInspect",
            "FormUIBridgeDemo"
            }, StringComparer.OrdinalIgnoreCase);
    }


    public void ClearAllLists()
    {
        ControlsToInclude.Clear();

        ControlsToExclude.Clear();

        InclusionKeywords.Clear();

        ExclusionKeywords.Clear();
    }


}