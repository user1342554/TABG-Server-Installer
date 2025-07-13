using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Gui.ViewModels
{
    public class StarterPackDynamicViewModel
    {
        private readonly TheStarterPackConfig _model;
        public ObservableCollection<SettingPropertyVM> Properties { get; }
        public StarterPackDynamicViewModel(TheStarterPackConfig cfg)
        {
            _model=cfg;
            var props = typeof(TheStarterPackConfig).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => !typeof(System.Collections.IEnumerable).IsAssignableFrom(p.PropertyType) || p.PropertyType == typeof(string))
                .OrderBy(p=>p.Name);
            Properties=new ObservableCollection<SettingPropertyVM>(props.Select(p=>new SettingPropertyVM(p,cfg)));
        }
        public TheStarterPackConfig ToModel()=>_model;
    }
} 