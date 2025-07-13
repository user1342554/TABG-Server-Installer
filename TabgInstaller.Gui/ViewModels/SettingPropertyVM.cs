using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace TabgInstaller.Gui.ViewModels
{
    public class SettingPropertyVM : INotifyPropertyChanged
    {
        private readonly PropertyInfo _prop;
        private readonly object _model;
        public SettingPropertyVM(PropertyInfo prop, object model)
        {
            _prop = prop;
            _model = model;
        }

        public string Name => _prop.Name;
        public Type PropType => _prop.PropertyType;

        public bool IsBool => PropType == typeof(bool);

        public bool BoolValue
        {
            get => (bool)_prop.GetValue(_model)!;
            set
            {
                if (value != BoolValue)
                {
                    _prop.SetValue(_model, value);
                    OnPropertyChanged(nameof(BoolValue));
                    OnPropertyChanged(nameof(ValueString));
                }
            }
        }

        public string ValueString
        {
            get
            {
                var val = _prop.GetValue(_model);
                return val switch
                {
                    null => string.Empty,
                    float f => f.ToString(CultureInfo.InvariantCulture),
                    _ => val.ToString() ?? string.Empty
                };
            }
            set
            {
                try
                {
                    object? converted = PropType switch
                    {
                        Type t when t == typeof(string) => value,
                        Type t when t == typeof(int) => int.Parse(value, CultureInfo.InvariantCulture),
                        Type t when t == typeof(float) => float.Parse(value, CultureInfo.InvariantCulture),
                        Type t when t == typeof(bool) => bool.Parse(value),
                        _ => null
                    };
                    if (converted != null)
                    {
                        _prop.SetValue(_model, converted);
                        OnPropertyChanged();
                    }
                }
                catch
                {
                    // invalid input ignored; ideally you would surface validation feedback
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name ?? string.Empty));
    }
} 