// <copyright file="DpiHelper.cs" company="MaaAssistantArknights">
// Part of the MaaWpfGui project, maintained by the MaaAssistantArknights team (Maa Team)
// Copyright (C) 2021-2025 MaaAssistantArknights Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.0 only as published by
// the Free Software Foundation, either version 3 of the License, or
// any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using MaaWpfGui.Configuration.Factory;

namespace MaaWpfGui.Helper;

/// <summary>
/// Applies the configured DPI override to WPF root visual trees.
/// </summary>
public static class DpiHelper
{
    public const int DefaultDpi = 96;
    public const int MinimumDpi = 48;
    public const int MaximumDpi = 960;

    private static readonly ConditionalWeakTable<Window, DetectedDpi> _detectedDpi = new();
    private static bool _initialized;

    /// <summary>
    /// Registers the application-wide window hook used to apply the DPI override.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));
        _initialized = true;
    }

    /// <summary>
    /// Reapplies the current setting to every open WPF window.
    /// </summary>
    public static void ApplyToAllWindows()
    {
        if (Application.Current == null)
        {
            return;
        }

        foreach (Window window in Application.Current.Windows)
        {
            if (window.IsLoaded)
            {
                Apply(window);
            }
        }
    }

    /// <summary>
    /// Converts a point from device pixels to WPF logical units using the effective DPI.
    /// </summary>
    /// <param name="visual">The visual whose effective DPI is used.</param>
    /// <param name="point">The point in device pixels.</param>
    /// <returns>The point in WPF logical units.</returns>
    public static Point FromDevice(Visual visual, Point point)
    {
        var dpi = VisualTreeHelper.GetDpi(visual);
        return new Point(point.X / dpi.DpiScaleX, point.Y / dpi.DpiScaleY);
    }

    /// <summary>
    /// Converts a point from WPF logical units to device pixels using the effective DPI.
    /// </summary>
    /// <param name="visual">The visual whose effective DPI is used.</param>
    /// <param name="point">The point in WPF logical units.</param>
    /// <returns>The point in device pixels.</returns>
    public static Point ToDevice(Visual visual, Point point)
    {
        var dpi = VisualTreeHelper.GetDpi(visual);
        return new Point(point.X * dpi.DpiScaleX, point.Y * dpi.DpiScaleY);
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is Window window)
        {
            Apply(window);
        }
    }

    private static void Apply(Window window)
    {
        int dpiOverride = ConfigFactory.Root.Gui.DpiOverride;
        if (dpiOverride is >= MinimumDpi and <= MaximumDpi) {
            var e = VisualTreeHelper.GetDpi(window);
            MessageBox.Show($"Current DPI scale: {e.DpiScaleX} {e.DpiScaleY} {e.PixelsPerDip} {e.PixelsPerInchX} {e.PixelsPerInchY}");
            VisualTreeHelper.SetRootDpi(window, new DpiScale((double)dpiOverride / DefaultDpi, (double)dpiOverride / DefaultDpi));
        }
    }

    private sealed record DetectedDpi(DpiScale Value);
}
