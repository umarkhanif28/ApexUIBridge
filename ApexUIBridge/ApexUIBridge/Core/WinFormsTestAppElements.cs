using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApexUIBridge.Core
{

    public class WinFormsTestAppElements
    {
        // Window
        public int WindowId { get; set; }

        // Buttons
        public int Button1Id { get; set; }

        public int Button2Id { get; set; }

        // CheckBoxes
        public int SimpleCheckBoxId { get; set; }

        public int ThreeStateCheckBoxId { get; set; }

        // RadioButtons
        public int RadioButton1Id { get; set; }

        public int RadioButton2Id { get; set; }

        // ComboBoxes
        public int EditableComboId { get; set; }

        public int NonEditableComboId { get; set; }

        // TextBoxes
        public int TextBoxId { get; set; }

        public int PasswordBoxId { get; set; }

        // Numeric/Range
        public int SliderId { get; set; }

        public int SpinnerId { get; set; }
        public int ProgressBarId { get; set; }

        // Lists
        public int ListBoxId { get; set; }

        public int ListViewId { get; set; }
        public int TreeViewId { get; set; }
        public int DataGridId { get; set; }

        // Tabs
        public int TabControlId { get; set; }

        public int TabPage1Id { get; set; }
        public int TabPage2Id { get; set; }

        // Menus
        public int MenuBarId { get; set; }

        public int FileMenuId { get; set; }
        public int EditMenuId { get; set; }

        // Other
        public int StatusBarId { get; set; }

        public int DateTimePickerId { get; set; }
    }
}
