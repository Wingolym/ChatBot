using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ChatBot
{
    public static class GridLengthAnimationHelper
    {
        private static readonly Dictionary<ColumnDefinition, Storyboard> _activeAnimations = new();

        public static void AnimateWidth(ColumnDefinition column, double toValue, int durationMs)
        {
            if (_activeAnimations.TryGetValue(column, out var existing))
            {
                existing.Stop();
                _activeAnimations.Remove(column);
            }

            var fromValue = column.Width.Value;
            if (fromValue == toValue) return;

            var startTime = DateTime.Now;
            var startValue = fromValue;
            var delta = toValue - fromValue;

            void OnRendering(object? sender, EventArgs e)
            {
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                var progress = Math.Min(elapsed / durationMs, 1.0);
                progress = EasingFunction(progress);
                column.Width = new GridLength(startValue + delta * progress, GridUnitType.Pixel);

                if (progress >= 1.0)
                {
                    CompositionTarget.Rendering -= OnRendering;
                    _activeAnimations.Remove(column);
                }
            }

            _activeAnimations[column] = new Storyboard();
            CompositionTarget.Rendering += OnRendering;
        }

        private static double EasingFunction(double t)
        {
            return t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2;
        }
    }
}