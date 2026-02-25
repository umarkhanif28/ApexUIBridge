
using ApexUIBridge.Core;
using ApexUIBridge.Core.Extensions;

using FlaUI.Core;

using System.Collections.Generic;

namespace ApexUIBridge.ViewModels;
public class DetailGroupViewModel : ObservableObject
{
    public DetailGroupViewModel(string name, IEnumerable<IDetailViewModel> details)
    {
        Name = name;
        Details = new ExtendedObservableCollection<IDetailViewModel>(details);
    }

    public string? Name 
    { 
        get 
        { 
            return GetProperty<string?>(); 
        } 
        set 
        { 
            SetProperty(value); 
        }
    }

    public ExtendedObservableCollection<IDetailViewModel> Details { get; set; }
}


public interface IDetailViewModel
{
    string Key { get; set; }
    string Value { get; set; }
    bool Important { get; set; }
}

public class DetailViewModel : ObservableObject, IDetailViewModel
{
    public DetailViewModel(string key, string value)
    {
        Key = key;
        Value = value;
    }

    public string Key
    {
        get
        {
            return GetProperty<string>();
        }
        set
        {
            SetProperty(value);
        }
    }
    public string Value
    {
        get
        {
            return GetProperty<string>();
        }
        set
        {
            SetProperty(value);
        }
    }
    public bool Important
    {
        get
        {
            return GetProperty<bool>();
        }
        set
        {
            SetProperty(value);
        }
    }

    public static DetailViewModel FromAutomationProperty<T>(string key, IAutomationProperty<T> value)
    {
        return new DetailViewModel(key, value.ToDisplayText());
    }
}
