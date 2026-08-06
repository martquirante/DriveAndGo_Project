using System;
using System.Collections.Generic;
using System.Windows.Forms;
using DriveAndGo_Admin.Controls;

namespace DriveAndGo_Admin.Helpers
{
    public enum SkeletonLayoutType
    {
        Grid,
        DashboardCard,
        ListRow,
        FormFields,
        Custom
    }

    /// <summary>
    /// Extension methods for WinForms controls to show and hide modern animated Skeleton Loaders.
    /// </summary>
    public static class UIHelper
    {
        private static readonly Dictionary<Control, SkeletonLoader> _skeletonRegistry = new();

        /// <summary>
        /// Overlays a modern animated Skeleton Loader with Shimmer Effect onto the target Control.
        /// </summary>
        /// <param name="control">The target panel, form, or grid to show skeleton on.</param>
        /// <param name="layoutType">The skeleton layout preset (Grid, DashboardCard, ListRow, FormFields, Custom).</param>
        /// <returns>The created or updated SkeletonLoader instance.</returns>
        public static SkeletonLoader ShowSkeleton(this Control control, SkeletonLayoutType layoutType = SkeletonLayoutType.Grid)
        {
            if (control == null || control.IsDisposed) return null;

            if (control.InvokeRequired)
            {
                return (SkeletonLoader)control.Invoke(new Func<SkeletonLoader>(() => ShowSkeleton(control, layoutType)));
            }

            if (_skeletonRegistry.TryGetValue(control, out var existing) && !existing.IsDisposed)
            {
                existing.LayoutType = layoutType;
                existing.BringToFront();
                existing.Visible = true;
                existing.StartAnimation();
                return existing;
            }

            var skeleton = new SkeletonLoader
            {
                Dock = DockStyle.Fill,
                LayoutType = layoutType
            };

            _skeletonRegistry[control] = skeleton;
            control.Controls.Add(skeleton);
            control.Controls.SetChildIndex(skeleton, 0);
            skeleton.BringToFront();
            skeleton.Visible = true;
            skeleton.StartAnimation();
            return skeleton;
        }

        /// <summary>
        /// Removes and disposes the Skeleton Loader overlay from the target Control.
        /// </summary>
        /// <param name="control">The target control hiding its skeleton.</param>
        public static void HideSkeleton(this Control control)
        {
            if (control == null || control.IsDisposed) return;

            if (control.InvokeRequired)
            {
                control.Invoke(new Action(() => HideSkeleton(control)));
                return;
            }

            if (_skeletonRegistry.TryGetValue(control, out var skeleton))
            {
                _skeletonRegistry.Remove(control);
                if (skeleton != null && !skeleton.IsDisposed)
                {
                    skeleton.StopAnimation();
                    control.Controls.Remove(skeleton);
                    skeleton.Dispose();
                }
            }

            // Fallback scan for any unindexed child SkeletonLoader controls
            for (int i = control.Controls.Count - 1; i >= 0; i--)
            {
                if (control.Controls[i] is SkeletonLoader sk)
                {
                    sk.StopAnimation();
                    control.Controls.RemoveAt(i);
                    sk.Dispose();
                }
            }
        }
    }
}
