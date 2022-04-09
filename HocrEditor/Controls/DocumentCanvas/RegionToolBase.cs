﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using HocrEditor.Helpers;
using HocrEditor.ViewModels;
using Optional;
using Optional.Unsafe;
using SkiaSharp;
using SkiaSharp.Views.WPF;
using Rect = HocrEditor.Models.Rect;

namespace HocrEditor.Controls;

public abstract class RegionToolBase : ICanvasTool
{
    private const int KEYBOARD_MOVE_CTRL_MULTIPLIER = 5;
    private const int KEYBOARD_MOVE_CTRL_SHIFT_MULTIPLIER = 10;

    protected Option<DocumentCanvas> Canvas { get; private set; } = Option.None<DocumentCanvas>();

    protected RegionToolMouseState MouseMoveState;

    protected SKPoint DragStart;
    protected SKPoint OffsetStart;

    protected SKRectI DragLimit = SKRectI.Empty;

    private SKRectI resizeLimitInside = SKRectI.Empty;
    private SKRectI resizeLimitOutside = SKRectI.Empty;

    private Option<ResizeHandle> selectedResizeHandle = Option.None<ResizeHandle>();

    public virtual bool CanMount(HocrPageViewModel page) => true;

    public virtual void Mount(DocumentCanvas canvas)
    {
        Canvas = Option.Some(canvas);

        canvas.MouseDown += DocumentCanvasOnMouseDown;
        canvas.MouseUp += DocumentCanvasOnMouseUp;
        canvas.MouseMove += DocumentCanvasOnMouseMove;
        canvas.MouseWheel += DocumentCanvasOnMouseWheel;
        canvas.KeyDown += DocumentCanvasOnKeyDown;

        canvas.UpdateCanvasSelection();
    }

    public void Unmount()
    {
        var canvas = Canvas.ValueOrFailure();

        canvas.MouseDown -= DocumentCanvasOnMouseDown;
        canvas.MouseUp -= DocumentCanvasOnMouseUp;
        canvas.MouseMove -= DocumentCanvasOnMouseMove;
        canvas.MouseWheel -= DocumentCanvasOnMouseWheel;
        canvas.KeyDown -= DocumentCanvasOnKeyDown;

        Unmount(canvas);
    }

    protected virtual void Unmount(DocumentCanvas canvas)
    {
    }

    public abstract void Render(SKCanvas canvas);

    private void DocumentCanvasOnMouseDown(object sender, MouseButtonEventArgs e)
    {
        var canvas = (DocumentCanvas)sender;

        if (canvas.ViewModel?.Nodes is not { Count: > 0 })
        {
            return;
        }

        if (MouseMoveState != RegionToolMouseState.None)
        {
            return;
        }

        Mouse.Capture(canvas);

        var position = e.GetPosition(canvas).ToSKPoint();

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        e.Handled = true;

        DragStart = position;

        Keyboard.Focus(canvas);

        canvas.EndEditing();

        // Handle resize.
        if (canvas.CanvasSelection.ShouldShowCanvasSelection)
        {
            var selectedHandle = canvas.CanvasSelection.ResizeHandles
                .FirstOrDefault(handle => handle.GetRect(canvas.Transformation).Contains(position));

            if (selectedHandle != null)
            {
                MouseMoveState = RegionToolMouseState.Resizing;

                CaptureKeyDownEvents(canvas);

                BeginResize(canvas);

                selectedResizeHandle = Option.Some(selectedHandle);

                OffsetStart = selectedHandle.Center;

                canvas.Refresh();

                return;
            }
        }

        var normalizedPosition = SKPointI.Truncate(canvas.InverseTransformation.MapPoint(position));

        OnMouseDown(canvas, e, normalizedPosition);

        canvas.Refresh();
    }

    private void DocumentCanvasOnMouseUp(object sender, MouseButtonEventArgs e)
    {
        var canvas = (DocumentCanvas)sender;

        if (!canvas.SelectedItems.HasValue)
        {
            return;
        }

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        e.Handled = true;

        switch (MouseMoveState)
        {
            case RegionToolMouseState.None:
                break;
            case RegionToolMouseState.Selecting:
            {
                canvas.CanvasSelection.Bounds = canvas.CanvasSelection.Bounds.Standardized;

                canvas.SelectionBounds = Rect.FromSKRect(canvas.CanvasSelection.Bounds);

                break;
            }
            case RegionToolMouseState.Dragging:
            {
                canvas.SelectionBounds = Rect.FromSKRect(canvas.CanvasSelection.Bounds);

                break;
            }
            case RegionToolMouseState.Resizing:
            {
                canvas.CanvasSelection.EndResize();

                ReleaseKeyDownEvents(canvas);

                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(MouseMoveState));
        }

        var position = e.GetPosition(canvas).ToSKPoint();
        var normalizedPosition = SKPointI.Truncate(canvas.InverseTransformation.MapPoint(position));

        OnMouseUp(canvas, e, normalizedPosition);

        MouseMoveState = RegionToolMouseState.None;

        canvas.ReleaseMouseCapture();
        canvas.Refresh();
    }

    private void DocumentCanvasOnMouseMove(object sender, MouseEventArgs e)
    {
        var canvas = (DocumentCanvas)sender;

        var position = e.GetPosition(canvas).ToSKPoint();

        var delta = canvas.InverseScaleTransformation.MapPoint(position - DragStart);

        switch (MouseMoveState)
        {
            case RegionToolMouseState.None:
            {
                if (!canvas.CanvasSelection.ShouldShowCanvasSelection)
                {
                    return;
                }

                e.Handled = true;

                var resizeHandles = canvas.CanvasSelection.ResizeHandles;

                var hoveringOnSelection = false;

                foreach (var handle in resizeHandles)
                {
                    var handleRect = handle.GetRect(canvas.Transformation);

                    if (!handleRect.Contains(position))
                    {
                        continue;
                    }

                    canvas.Cursor = handle.Direction switch
                    {
                        CardinalDirections.NorthWest or CardinalDirections.SouthEast => Cursors.SizeNWSE,
                        CardinalDirections.North or CardinalDirections.South => Cursors.SizeNS,
                        CardinalDirections.NorthEast or CardinalDirections.SouthWest => Cursors.SizeNESW,
                        CardinalDirections.West or CardinalDirections.East => Cursors.SizeWE,
                        _ => throw new ArgumentOutOfRangeException(nameof(handle.Direction))
                    };

                    hoveringOnSelection = true;

                    break;
                }

                if (!hoveringOnSelection &&
                    canvas.Transformation.MapRect(canvas.CanvasSelection.Bounds).Contains(position))
                {
                    hoveringOnSelection = true;

                    canvas.Cursor = Cursors.SizeAll;
                }

                if (!hoveringOnSelection)
                {
                    canvas.Cursor = canvas.CurrentCursor;
                }

                // Skip refreshing.
                return;
            }
            case RegionToolMouseState.Resizing:
            {
                e.Handled = true;

                PerformResize(canvas, delta);

                break;
            }
            case RegionToolMouseState.Dragging:
            {
                e.Handled |= OnDragSelection(canvas, delta);
                break;
            }
            case RegionToolMouseState.Selecting:
            {
                e.Handled |= OnSelectSelection(canvas, delta);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(MouseMoveState));
        }

        canvas.Refresh();
    }

    private void DocumentCanvasOnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var canvas = (DocumentCanvas)sender;

        DragLimit = CalculateDragLimitBounds(canvas);
    }

    private void DocumentCanvasOnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Up or Key.Down or Key.Left or Key.Right))
        {
            return;
        }

        e.Handled = true;

        var delta = e.Key switch
        {
            Key.Up => new SKPointI(0, -1),
            Key.Down => new SKPointI(0, 1),
            Key.Left => new SKPointI(-1, 0),
            Key.Right => new SKPointI(1, 0),
            _ => throw new ArgumentOutOfRangeException()
        };

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            var multiplier = KEYBOARD_MOVE_CTRL_MULTIPLIER;

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                multiplier = KEYBOARD_MOVE_CTRL_SHIFT_MULTIPLIER;
            }

            delta.X *= multiplier;
            delta.Y *= multiplier;
        }

        var canvas = (DocumentCanvas)sender;

        BeginDrag(canvas);

        OnDragSelection(canvas, delta);

        UpdateNodes(canvas);

        MouseMoveState = RegionToolMouseState.None;

        canvas.Refresh();
    }


    protected virtual void OnMouseDown(DocumentCanvas canvas, MouseButtonEventArgs e, SKPointI normalizedPosition)
    {
    }

    protected virtual void OnMouseUp(DocumentCanvas canvas, MouseButtonEventArgs e, SKPoint normalizedPosition)
    {
    }

    protected abstract bool OnSelectSelection(DocumentCanvas canvas, SKPoint delta);

    protected abstract bool OnDragSelection(DocumentCanvas canvas, SKPoint delta);

    protected static void UpdateNodes(DocumentCanvas canvas)
    {
        var changes = new List<NodesChangedEventArgs.NodeChange>(canvas.SelectedElements.Count);

        foreach (var id in canvas.SelectedElements)
        {
            var (node, element) = canvas.Elements[id];

            changes.Add(new NodesChangedEventArgs.NodeChange(node, element.Bounds, node.BBox));
        }

        canvas.OnNodesChanged(changes);
    }

    private void ClearCanvasResizeLimit(DocumentCanvas canvas)
    {
        resizeLimitInside = SKRectI.Empty;
        resizeLimitOutside = canvas.RootCanvasElement.Bounds;
    }

    private void UpdateCanvasResizeLimits(DocumentCanvas canvas)
    {
        // TODO: Support for multiple selection.
        // If we have only one item selected, set its resize limits to within its parent and around its children.
        if (canvas.SelectedItems.Exists(items => items.Count == 1))
        {
            var node = canvas.SelectedItems.ValueOrFailure().First();

            var containedChildren = node.Children.Where(c => node.BBox.Contains(c.BBox));

            resizeLimitInside = NodeHelpers.CalculateUnionRect(containedChildren).ToSKRectI();

            // TODO: This fails when merging.
            // Debug.Assert(
            //     resizeLimitInside.IsEmpty || canvasSelection.Bounds.Contains(resizeLimitInside),
            //     "Expected inner resize limit to be contained in the canvas selection bounds."
            // );

            if (node.ParentId >= 0)
            {
                resizeLimitOutside = canvas.Elements[node.ParentId].Item2.Bounds;

                // TODO: This fails when merging.
                // Debug.Assert(
                //     resizeLimitOutside.Contains(canvasSelection.Bounds),
                //     "Expected outer resize limit to contain the canvas selection bounds."
                // );
            }
        }
        else
        {
            // No resize limit (allow any size within the page).

            ClearCanvasResizeLimit(canvas);
        }
    }

    protected abstract SKRectI CalculateDragLimitBounds(DocumentCanvas canvas);

    protected void BeginDrag(DocumentCanvas canvas)
    {
        MouseMoveState = RegionToolMouseState.Dragging;

        OffsetStart = canvas.Transformation.MapPoint(canvas.CanvasSelection.Bounds.Location);

        DragLimit = CalculateDragLimitBounds(canvas);
    }

    private void BeginResize(DocumentCanvas canvas)
    {
        canvas.CanvasSelection.BeginResize();

        UpdateCanvasResizeLimits(canvas);
    }

    private void PerformResize(DocumentCanvas canvas, SKPoint delta)
    {
        var newLocation = OffsetStart + delta;

        var resizeHandle = selectedResizeHandle.ValueOrFailure();

        var resizePivot = new SKPoint(
            canvas.CanvasSelection.InitialBounds.MidX,
            canvas.CanvasSelection.InitialBounds.MidY
        );

        // If more than one element selected, or exactly one element selected _and_ Ctrl is pressed, resize together with children.
        var resizeWithChildren = canvas.SelectedItems.Exists(items => items.Count > 1) ||
                                 Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        var resizeSymmetrical = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);

        // Reset the selection bounds so changing keyboard modifiers works off of the initial bounds.
        // This achieves a more "Photoshop-like" behavior for the selection.
        canvas.CanvasSelection.Bounds = canvas.CanvasSelection.InitialBounds;

        if (resizeHandle.Direction.HasFlag(CardinalDirections.West))
        {
            // Calculate next left position for bounds while clamping within the limits.
            var nextLeft = Math.Clamp(
                newLocation.X,
                resizeLimitOutside.Left,
                resizeLimitInside.IsEmpty || resizeWithChildren
                    ? resizeLimitOutside.Right
                    : resizeLimitInside.Left
            );

            if (resizeSymmetrical)
            {
                // Calculate the symmetrical offset based on delta from the pivot.
                var deltaX = resizePivot.X - nextLeft;

                // Calculate next right position from the expected left position, but clamp within the limits.
                canvas.CanvasSelection.Right = (int)Math.Clamp(
                    nextLeft + 2 * deltaX,
                    resizeLimitInside.IsEmpty || resizeWithChildren
                        ? resizeLimitOutside.Left
                        : resizeLimitInside.Right,
                    resizeLimitOutside.Right
                );

                // Calculate delta to pivot from the opposite side of the symmetry.
                deltaX = canvas.CanvasSelection.Right - resizePivot.X;

                // Recalculate left position, to achieve bounds that are clamped by the edge closest to the limits.
                nextLeft = resizePivot.X - deltaX;
            }
            else
            {
                resizePivot.X = canvas.CanvasSelection.InitialBounds.Right;
            }

            canvas.CanvasSelection.Left = (int)nextLeft;
        }

        if (resizeHandle.Direction.HasFlag(CardinalDirections.North))
        {
            var nextTop = Math.Clamp(
                newLocation.Y,
                resizeLimitOutside.Top,
                resizeLimitInside.IsEmpty || resizeWithChildren
                    ? resizeLimitOutside.Bottom
                    : resizeLimitInside.Top
            );

            if (resizeSymmetrical)
            {
                var deltaY = resizePivot.Y - nextTop;

                canvas.CanvasSelection.Bottom = (int)Math.Clamp(
                    nextTop + 2 * deltaY,
                    resizeLimitInside.IsEmpty || resizeWithChildren
                        ? resizeLimitOutside.Top
                        : resizeLimitInside.Bottom,
                    resizeLimitOutside.Bottom
                );

                deltaY = canvas.CanvasSelection.Bottom - resizePivot.Y;

                nextTop = resizePivot.Y - deltaY;
            }
            else
            {
                resizePivot.Y = canvas.CanvasSelection.InitialBounds.Bottom;
            }

            canvas.CanvasSelection.Top = (int)nextTop;
        }

        if (resizeHandle.Direction.HasFlag(CardinalDirections.East))
        {
            var nextRight = Math.Clamp(
                newLocation.X,
                resizeLimitInside.IsEmpty || resizeWithChildren
                    ? resizeLimitOutside.Left
                    : resizeLimitInside.Right,
                resizeLimitOutside.Right
            );

            if (resizeSymmetrical)
            {
                var deltaX = nextRight - resizePivot.X;

                canvas.CanvasSelection.Left = (int)Math.Clamp(
                    nextRight - 2 * deltaX,
                    resizeLimitOutside.Left,
                    resizeLimitInside.IsEmpty || resizeWithChildren
                        ? resizeLimitOutside.Right
                        : resizeLimitInside.Left
                );

                deltaX = resizePivot.X - canvas.CanvasSelection.Left;

                nextRight = resizePivot.X + deltaX;
            }
            else
            {
                resizePivot.X = canvas.CanvasSelection.InitialBounds.Left;
            }

            canvas.CanvasSelection.Right = (int)nextRight;
        }

        if (resizeHandle.Direction.HasFlag(CardinalDirections.South))
        {
            var nextBottom = Math.Clamp(
                newLocation.Y,
                resizeLimitInside.IsEmpty || resizeWithChildren
                    ? resizeLimitOutside.Top
                    : resizeLimitInside.Bottom,
                resizeLimitOutside.Bottom
            );

            if (resizeSymmetrical)
            {
                var deltaY = nextBottom - resizePivot.Y;

                canvas.CanvasSelection.Top = (int)Math.Clamp(
                    nextBottom - 2 * deltaY,
                    resizeLimitOutside.Top,
                    resizeLimitInside.IsEmpty || resizeWithChildren
                        ? resizeLimitOutside.Bottom
                        : resizeLimitInside.Top
                );

                deltaY = resizePivot.Y - canvas.CanvasSelection.Top;

                nextBottom = resizePivot.Y + deltaY;
            }
            else
            {
                resizePivot.Y = canvas.CanvasSelection.InitialBounds.Top;
            }

            canvas.CanvasSelection.Bottom = (int)nextBottom;
        }

        var ratio = canvas.CanvasSelection.ResizeRatio;

        var matrix = SKMatrix.CreateScale(ratio.X, ratio.Y, resizePivot.X, resizePivot.Y);

        foreach (var id in canvas.SelectedElements)
        {
            // Start with the initial value, so pressing and releasing Ctrl reverts to original size.
            var bounds = canvas.Elements[id].Item1.BBox.ToSKRectI();

            if (resizeWithChildren || canvas.SelectedItems.Exists(items => items.Any(node => node.Id == id)))
            {
                bounds = SKRectI.Truncate(matrix.MapRect(bounds));
                bounds.Clamp(canvas.CanvasSelection.Bounds);
            }

            canvas.Elements[id].Item2.Bounds = bounds;
        }
    }

    private void CaptureKeyDownEvents(DocumentCanvas canvas)
    {
        canvas.ParentWindow.KeyDown += WindowOnKeyChange;
        canvas.ParentWindow.KeyUp += WindowOnKeyChange;
    }

    private void ReleaseKeyDownEvents(DocumentCanvas canvas)
    {
        canvas.ParentWindow.KeyDown -= WindowOnKeyChange;
        canvas.ParentWindow.KeyUp -= WindowOnKeyChange;
    }

    private void WindowOnKeyChange(object sender, KeyEventArgs e)
    {
        var canvas = Canvas.ValueOrFailure();

        // TODO: Mac support?
        if (e.Key is not (Key.LeftCtrl or Key.RightCtrl) || MouseMoveState != RegionToolMouseState.Resizing)
        {
            return;
        }

        var position = Mouse.GetPosition(canvas).ToSKPoint();

        var delta = canvas.InverseScaleTransformation.MapPoint(position - DragStart);

        PerformResize(canvas, delta);

        canvas.Refresh();
    }
}
