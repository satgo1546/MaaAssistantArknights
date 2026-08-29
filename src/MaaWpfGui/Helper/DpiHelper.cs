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

#nullable enable

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using MaaWpfGui.Configuration.Factory;

namespace MaaWpfGui.Helper;

/// <summary>
/// Applies the configured DPI override through WPF's complete HWND DPI-change path.
/// </summary>
public static class DpiHelper
{
    public const int DefaultDpi = 96;
    public const int MinimumDpi = 48;
    public const int MaximumDpi = 960;

    private static readonly ConditionalWeakTable<Window, DetectedDpi> _detectedDpi = [];

    // WPF exposes the notification types but keeps the method that performs the complete
    // HwndTarget/world-transform/layout update internal. Keep this reflection localized here.
    private static readonly ConstructorInfo? _dpiChangedEventArgsConstructor = typeof(HwndDpiChangedEventArgs).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        binder: null,
        [typeof(DpiScale), typeof(DpiScale), typeof(Rect)],
        modifiers: null);

    private static readonly MethodInfo? _changeDpiMethod = typeof(HwndSource).GetMethod(
        "ChangeDpi",
        BindingFlags.Instance | BindingFlags.NonPublic,
        binder: null,
        [typeof(HwndDpiChangedEventArgs)],
        modifiers: null);

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
    /// Attaches the DPI override before a window's placement is restored.
    /// </summary>
    /// <param name="window">The window being created.</param>
    public static void Attach(Window window)
    {
        window.SourceInitialized += OnWindowSourceInitialized;
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
        var transform = PresentationSource.FromVisual(visual)?.CompositionTarget?.TransformFromDevice;
        return transform?.Transform(point) ?? point;
    }

    /// <summary>
    /// Converts a point from WPF logical units to device pixels using the effective DPI.
    /// </summary>
    /// <param name="visual">The visual whose effective DPI is used.</param>
    /// <param name="point">The point in WPF logical units.</param>
    /// <returns>The point in device pixels.</returns>
    public static Point ToDevice(Visual visual, Point point)
    {
        var transform = PresentationSource.FromVisual(visual)?.CompositionTarget?.TransformToDevice;
        return transform?.Transform(point) ?? point;
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is Window window)
        {
            Apply(window);
        }
    }

    private static void OnWindowSourceInitialized(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            Apply(window);
        }
    }

    private static void Apply(Window window)
    {
        if (PresentationSource.FromVisual(window) is not HwndSource source ||
            source.CompositionTarget == null ||
            _dpiChangedEventArgsConstructor == null ||
            _changeDpiMethod == null)
        {
            return;
        }

        var current = GetDpi(source);
        var detected = _detectedDpi.GetValue(window, _ => new DetectedDpi(current)).Value;
        int dpiOverride = ConfigFactory.Root.Gui.DpiOverride;
        var effective = dpiOverride is >= MinimumDpi and <= MaximumDpi
            ? new DpiScale((double)dpiOverride / DefaultDpi, (double)dpiOverride / DefaultDpi)
            : detected;

        if (AreClose(current, effective) || !GetWindowRect(source.Handle, out NativeRect windowRect))
        {
            return;
        }

        var suggestedRect = new Rect(
            windowRect.Left,
            windowRect.Top,
            Math.Max(1, windowRect.Width * effective.DpiScaleX / current.DpiScaleX),
            Math.Max(1, windowRect.Height * effective.DpiScaleY / current.DpiScaleY));
        var eventArgs = _dpiChangedEventArgsConstructor.Invoke([current, effective, suggestedRect]);
        _changeDpiMethod.Invoke(source, [eventArgs]);
    }

    private static DpiScale GetDpi(HwndSource source)
    {
        var transform = source.CompositionTarget!.TransformToDevice;
        return new DpiScale(transform.M11, transform.M22);
    }

    private static bool AreClose(DpiScale left, DpiScale right)
    {
        const double tolerance = 0.000001;
        return Math.Abs(left.DpiScaleX - right.DpiScaleX) < tolerance &&
               Math.Abs(left.DpiScaleY - right.DpiScaleY) < tolerance;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeRect
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;

        public int Width => Right - Left;

        public int Height => Bottom - Top;
    }

    private sealed record DetectedDpi(DpiScale Value);
}
