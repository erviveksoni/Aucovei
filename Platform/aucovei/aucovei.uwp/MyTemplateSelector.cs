using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using aucovei.uwp.Model;

namespace aucovei.uwp
{
    public class MyTemplateSelector : DataTemplateSelector
    {
        public DataTemplate WayPoint { get; set; }
        public DataTemplate Polyline { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item != null)
            {
                if (item is PolylinePath)
                {
                    return Polyline;
                }

                return WayPoint;
            }

            return null;
        }
    }
}