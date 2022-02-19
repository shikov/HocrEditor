﻿using System;
using HocrEditor.Models;

namespace HocrEditor.Helpers;

public static class HocrNodeTypeHelper
{
    public static HocrNodeType? GetParentNodeType(HocrNodeType nodeType) => nodeType switch
    {
        HocrNodeType.Page => null,
        HocrNodeType.ContentArea => HocrNodeType.Page,
        HocrNodeType.Paragraph => HocrNodeType.ContentArea,
        HocrNodeType.Line => HocrNodeType.Paragraph,
        HocrNodeType.Header => HocrNodeType.Paragraph,
        HocrNodeType.Footer => HocrNodeType.Paragraph,
        HocrNodeType.TextFloat => HocrNodeType.ContentArea,
        HocrNodeType.Caption => HocrNodeType.ContentArea,
        HocrNodeType.Word => HocrNodeType.Line,
        HocrNodeType.Image => HocrNodeType.Page,
        _ => throw new ArgumentOutOfRangeException(nameof(nodeType), nodeType, null)
    };

    public static string GetIcon(HocrNodeType nodeType) => nodeType switch
    {
        HocrNodeType.Page => "/Icons/document.png",
        HocrNodeType.ContentArea => "/Icons/layers-group.png",
        HocrNodeType.Paragraph => "/Icons/edit-pilcrow.png",
        HocrNodeType.Line or HocrNodeType.TextFloat or HocrNodeType.Caption or HocrNodeType.Footer =>
            "/Icons/edit-lipsum.png",
        HocrNodeType.Header => "/Icons/edit-heading.png",
        HocrNodeType.Image => "/Icons/image.png",
        HocrNodeType.Word => "/Icons/edit-quotation.png",
        _ => throw new ArgumentOutOfRangeException()
    };
}
