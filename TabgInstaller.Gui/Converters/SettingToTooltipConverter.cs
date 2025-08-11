using System;
using System.Globalization;
using System.Windows.Data;
using TabgInstaller.Core.Services;

namespace TabgInstaller.Gui.Converters
{
    public class SettingToTooltipConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var key = value as string;
                if (string.IsNullOrWhiteSpace(key)) return null;

                var idx = KnowledgeIndex.Current;
                if (idx.GameSettings.TryGetValue(key, out var gs))
                {
                    var range = gs.Range != null ? $" Range: {gs.Range.Min}–{gs.Range.Max}." : string.Empty;
                    var allowed = (gs.Allowed?.Length ?? 0) > 0 ? $" Allowed: {string.Join(", ", gs.Allowed)}." : string.Empty;
                    var def = gs.Default != null ? $" Default: {gs.Default}." : string.Empty;
                    var desc = string.IsNullOrWhiteSpace(gs.Description) ? key : gs.Description;
                    return $"{desc}{def}{range}{allowed}".Trim();
                }
                if (idx.StarterPackSettings.TryGetValue(key, out var sp))
                {
                    var desc = string.IsNullOrWhiteSpace(sp.Description) ? key : sp.Description;
                    return string.IsNullOrWhiteSpace(sp.Syntax) ? desc : $"{desc} Syntax: {sp.Syntax}";
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}


