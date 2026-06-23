using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace SEE_INSADE.Core.Localization
{
    public static class LocalizationHelper
    {
        public static void Apply(Window window)
        {
            var localization = LocalizationManager.Instance;
            window.Title = localization.TText(window.Title);

            foreach (DependencyObject element in Traverse(window))
            {
                if (element is TextBlock textBlock)
                    textBlock.Text = localization.TText(textBlock.Text);

                if (element is ContentControl contentControl && contentControl.Content is string content)
                    contentControl.Content = localization.TText(content);

                if (element is HeaderedContentControl headeredContentControl && headeredContentControl.Header is string header)
                    headeredContentControl.Header = localization.TText(header);
            }
        }

        private static IEnumerable<DependencyObject> Traverse(DependencyObject root)
        {
            foreach (object childObject in LogicalTreeHelper.GetChildren(root))
            {
                if (childObject is not DependencyObject child)
                    continue;

                yield return child;

                foreach (DependencyObject descendant in Traverse(child))
                    yield return descendant;
            }
        }
    }
}
