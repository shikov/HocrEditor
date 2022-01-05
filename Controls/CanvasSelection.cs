﻿using System.Collections.Generic;
using SkiaSharp;

namespace HocrEditor.Controls;

internal class CanvasSelection
{
    private SKRect bounds;
    private readonly ResizeHandle[] resizeHandles;

    public CanvasSelection()
    {
        resizeHandles = new[]
        {
            // Clockwise from top-left.
            new ResizeHandle(Bounds.Location, CardinalDirections.NorthWest),
            new ResizeHandle(new SKPoint(Bounds.MidX, Bounds.Top), CardinalDirections.North),
            new ResizeHandle(new SKPoint(Bounds.Right, Bounds.Top), CardinalDirections.NorthEast),
            new ResizeHandle(new SKPoint(Bounds.Right, Bounds.MidY), CardinalDirections.East),
            new ResizeHandle(new SKPoint(Bounds.Right, Bounds.Bottom), CardinalDirections.SouthEast),
            new ResizeHandle(new SKPoint(Bounds.MidX, Bounds.Bottom), CardinalDirections.South),
            new ResizeHandle(new SKPoint(Bounds.Left, Bounds.Bottom), CardinalDirections.SouthWest),
            new ResizeHandle(new SKPoint(Bounds.Left, Bounds.MidY), CardinalDirections.West),
        };
    }

    public SKRect Bounds
    {
        get => bounds;
        set => bounds = value;
    }

    public IEnumerable<ResizeHandle> ResizeHandles
    {
        get
        {
            CalculateRectResizeHandles(bounds);

            return resizeHandles;
        }
    }

    public bool IsEmpty => Bounds.IsEmpty;

    public float Left
    {
        get => bounds.Left;
        set => bounds.Left = value;
    }

    public float Top
    {
        get => bounds.Top;
        set => bounds.Top = value;
    }

    public float Right
    {
        get => bounds.Right;
        set => bounds.Right = value;
    }

    public float Bottom
    {
        get => bounds.Bottom;
        set => bounds.Bottom = value;
    }

    private void CalculateRectResizeHandles(SKRect r)
    {
        resizeHandles[0].Center = r.Location;
        resizeHandles[1].Center = new SKPoint(r.MidX, r.Top);
        resizeHandles[2].Center = new SKPoint(r.Right, r.Top);
        resizeHandles[3].Center = new SKPoint(r.Right, r.MidY);
        resizeHandles[4].Center = new SKPoint(r.Right, r.Bottom);
        resizeHandles[5].Center = new SKPoint(r.MidX, r.Bottom);
        resizeHandles[6].Center = new SKPoint(r.Left, r.Bottom);
        resizeHandles[7].Center = new SKPoint(r.Left, r.MidY);
    }
}
