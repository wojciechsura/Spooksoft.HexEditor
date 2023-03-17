using HexEditor.Models;
using HexEditor.Types;
using HexEditor.Units;
using HexEditor.Geometry;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using HexEditor.Infrastructure;

namespace HexEditor.Controls
{
    public partial class HexEditorDisplay : FrameworkElement
    {
        // Private constants --------------------------------------------------

        private char[] hexChars = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

        private const double GHOST_SELECTION_THICKNESS = 1.0;
        private const int HEX_CHAR_COUNT = 2;
        private const int MAX_HEX_CHAR_INDEX = HEX_CHAR_COUNT - 1;
        private const int INSERT_CURSOR_DIVIDER = 3;
        private const int MAX_ASCII_CHAR = 127;

        // Private classes ----------------------------------------------------

        // *** Hit info ***

        private class BaseMouseHitInfo
        {

        }

        private abstract class DocumentMouseHitInfo : BaseMouseHitInfo
        {
            public DocumentMouseHitInfo(int offset)
            {
                Offset = offset;
            }

            public int Offset { get; }
        }

        private class HexMouseHitInfo : DocumentMouseHitInfo
        {
            public HexMouseHitInfo(int offset, int @char)
                : base(offset)
            {
                Char = @char;
            }

            public override string ToString() => $"Mouse hit hex at offset {Offset}, char {Char}";

            public int Char { get; }
        }

        private class CharMouseHitInfo : DocumentMouseHitInfo
        {
            public CharMouseHitInfo(int offset)
                : base(offset)
            {
                
            }

            public override string ToString() => $"Mouse hit char at offset {Offset}";
        }

        // *** Mouse mode ***

        private enum MouseMode
        {
            Idle,
            HexSelection,
            CharSelection
        }

        // *** Mouse data ***

        private class BaseMouseData
        {

        }

        private class HexSelectionMouseData : BaseMouseData
        {
            public HexSelectionMouseData(int originOffset, int originChar, bool leftOrigin)
            {
                OriginOffset = originOffset;
                OriginChar = originChar;
                LeftOrigin = leftOrigin;
            }

            public int OriginOffset { get; }
            public int OriginChar { get; }
            public bool LeftOrigin { get; set; }
        }

        private class CharSelectionMouseData : BaseMouseData
        {
            public CharSelectionMouseData(int originOffset, bool leftOrigin)
            {
                OriginOffset = originOffset;
                LeftOrigin = leftOrigin;
            }

            public int OriginOffset { get; }            
            public bool LeftOrigin { get; set; }
        }

        // *** Drawing text ***

        private class GlyphRunInfo
        {
            public GlyphRunInfo()
            {
                CurrentPosition = 0;
            }

            public void FillMissingAdvanceWidths()
            {
                while (AdvanceWidths.Count < GlyphIndexes.Count)
                    AdvanceWidths.Add(0);
            }

            public List<ushort> GlyphIndexes { get; } = new List<ushort>();
            public List<double> AdvanceWidths { get; } = new List<double>();
            public double CurrentPosition { get; set; }
            public double? StartPosition { get; set; }
        }

        // Private fields -----------------------------------------------------

        private readonly Metrics metrics;
        private Typeface typeface;
        private GlyphTypeface glyphTypeface;

        private byte[] dataBuffer;

        private BaseSelectionInfo selection;

        private MouseMode mouseMode;
        private BaseMouseData mouseData;

        private EnteringMode enteringMode;

        // Brushes

        private readonly Brush footerBrush = new SolidColorBrush(Color.FromRgb(214, 214, 214));
        private readonly Brush headerBrush = new SolidColorBrush(Color.FromRgb(214, 214, 214));
        private readonly Brush marginBrush = new SolidColorBrush(Color.FromRgb(230, 230, 230));

        // Private methods ----------------------------------------------------

        private void BuildTypeface()
        {
            typeface = new Typeface(FontFamily);
            if (!typeface.TryGetGlyphTypeface(out glyphTypeface))
            {
                typeface = null;
                glyphTypeface = null;
            }
        }

        private void UpdateScrollProperties()
        {
            ValidateMetrics();

            if (Document == null)
            {
                ScrollPosition = 0;
                ScrollMaximum = 0;
                ScrollLargeChange = 0;
            }
            else
            {           
                ScrollPosition = ScrollPosition.ClampTo(0, metrics.Scroll.Maximum);
                ScrollMaximum = metrics.Scroll.Maximum;
                ScrollLargeChange = metrics.Scroll.LargeChange;
            }
        }

        private void ResetSelection()
        {
            selection = null;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SetSelection(BaseSelectionInfo selection)
        {
            if (Document != null)
            {
                if (selection == null)
                {
                    this.selection = null;
                }
                else if (selection is HexCursorSelectionInfo hexSelection)
                {
                    // We allow selecting one byte after last byte of document
                    // thus > instead of >=
                    if (hexSelection.Offset.IsOutside(0, Document?.Size ?? 0))
                        throw new ArgumentException(nameof(selection));
                    if (hexSelection.Char.IsOutside(0, 1))
                        throw new ArgumentException(nameof(selection));

                    this.selection = hexSelection;
                }
                else if (selection is CharCursorSelectionInfo charSelection)
                {
                    // We allow selecting one byte after last byte of document
                    // thus > instead of >=
                    if (charSelection.Offset.IsOutside(0, Document?.Size ?? 0))
                        throw new ArgumentException(nameof(selection));

                    this.selection = charSelection;
                }
                else if (selection is RangeSelectionInfo rangeSelection)
                {
                    // On the other hand, range selection may not include
                    // the additional byte - user may select only real data
                    if (rangeSelection.SelectionStart.IsOutside(0, Document?.Size ?? 0))
                        throw new ArgumentException(nameof(selection));
                    if (rangeSelection.SelectionLength < 0 ||
                        (rangeSelection.SelectionStart + rangeSelection.SelectionLength).IsOutside(0, Document?.Size ?? 0))
                        throw new ArgumentException(nameof(selection));

                    this.selection = rangeSelection;
                }
                else
                {
                    throw new ArgumentException(nameof(selection));
                }

                // No need to invalidate metrics
                InvalidateVisual();
            }
            else
            {
                throw new InvalidOperationException("Cannot set selection without active document!");
            }

            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void EnsureCursorVisible()
        {
            if (Document != null)
            {
                // Metrics must be valid to evaluate scroll information correctly
                ValidateMetrics();

                int? offset = null;
                switch (selection)
                {
                    case BaseOffsetSelectionInfo offsetSelection:
                        {
                            offset = offsetSelection.Offset;
                            break;
                        }
                    case RangeSelectionInfo rangeSelection:
                        {
                            offset = rangeSelection.Cursor;
                            break;
                        }
                }

                if (offset != null)
                {
                    var line = offset.Value / Document.BytesPerRow;
                    var maxLine = Math.Min(metrics.Scroll.Maximum, line);
                    var minLine = Math.Max(0, line - metrics.Control.LinePositions.VisibleFullLineCount + 1);

                    ScrollPosition = ScrollPosition.ClampTo(minLine, maxLine);
                }
            }
        }

        private int? GetCursorOffset()
        {
            switch (selection)
            {
                case BaseOffsetSelectionInfo baseOffsetSelection:
                    return baseOffsetSelection.Offset;
                case RangeSelectionInfo rangeSelection:
                    return rangeSelection.Cursor;
                case null:
                    return null;
                default:
                    throw new InvalidOperationException("Invalid selection!");
            }
        }

        /// <summary>
        /// Removes range selection when data was change outside the control
        /// </summary>
        private void FixSelectionAfterChange()
        {
            if (Document != null)
            {
                switch (selection)
                {
                    case HexCursorSelectionInfo hexCursorSelection:
                        {
                            if (hexCursorSelection.Offset < 0 || hexCursorSelection.Offset > Document.Size)
                                Selection = new HexCursorSelectionInfo(hexCursorSelection.Offset.ClampTo(0, Document.Size), hexCursorSelection.Char);

                            break;
                        }
                    case CharCursorSelectionInfo charCursorSelection:
                        {
                            if (charCursorSelection.Offset < 0 || charCursorSelection.Offset > Document.Size)
                                Selection = new CharCursorSelectionInfo(charCursorSelection.Offset.ClampTo(0, Document.Size));

                            break;
                        }
                    case RangeSelectionInfo rangeSelection:
                        {
                            var offset = rangeSelection.Cursor.ClampTo(0, Document.Size);

                            switch (rangeSelection.Area)
                            {
                                case DataArea.Hex:
                                    {
                                        Selection = new HexCursorSelectionInfo(offset, 0);
                                        break;
                                    }
                                case DataArea.Char:
                                    {
                                        Selection = new CharCursorSelectionInfo(offset);
                                        break;
                                    }
                                default:
                                    throw new InvalidEnumArgumentException("Invalid area!");
                            }
                            break;
                        }
                }
            }
        }

        #region Metrics

        private void InvalidateMetrics()
        {
            metrics.Invalidate();
        }

        private void ValidateMetrics()
        {
            if (!metrics.Valid)
            {
                metrics.Validate();
                UpdateScrollProperties();
            }
        }

        #endregion

        #region Property change handlers

        private void HandleDocumentChanged(HexByteContainer oldDocument, HexByteContainer newDocument)
        {
            if (oldDocument != null)
            {
                oldDocument.Changed -= HandleDataChanged;
                oldDocument.BytesPerRowChanged -= HandleBytesPerRowChanged;
            }

            if (newDocument != null)
            {
                newDocument.Changed += HandleDataChanged;
                newDocument.BytesPerRowChanged += HandleBytesPerRowChanged;
            }

            ResetSelection();
            ScrollPosition = 0;
            mouseMode = MouseMode.Idle;
            mouseData = null;
            enteringMode = EnteringMode.Overwrite;

            metrics.Document = newDocument;

            InvalidateMetrics();
            InvalidateVisual();
        }

        private void HandleFontFamilyChanged()
        {
            BuildTypeface();
            metrics.Typeface = typeface;

            InvalidateVisual();
        }

        private void HandleScrollPositionChanged()
        {
            if (ScrollPosition < 0 || ScrollPosition > ScrollMaximum)
                throw new ArgumentOutOfRangeException(nameof(ScrollPosition));

            InvalidateMetrics();
            InvalidateVisual();
        }

        private object HandleScrollPositionCoerce(int scrollPosition)
        {
            return scrollPosition.ClampTo(0, ScrollMaximum);
        }

        private void HandleFontSizeChanged()
        {
            metrics.FontSize = FontSize;

            InvalidateVisual();
        }

        private void HandleBytesPerRowChanged(object sender, EventArgs e)
        {
            InvalidateMetrics();
            InvalidateVisual();
        }

        private void HandleDataChanged(object sender, DataChangeEventArgs e)
        {
            FixSelectionAfterChange();

            InvalidateMetrics();
            InvalidateVisual();
        }

        #endregion

        #region Drawing

        private void DrawFooter(DrawingContext drawingContext)
        {
            drawingContext.DrawRectangle(footerBrush, null, metrics.Control.FooterArea.Rectangle.ToRect());
        }

        private (double startY, double height) GetCursorVerticalData(EnteringMode enteringMode, int line)
        {
            double startY, height;

            if (enteringMode == EnteringMode.Overwrite)
            {
                startY = metrics.Control.LinePositions.Positions[line].TextStartY;
                height = metrics.Character.CharHeight;
            }
            else
            {
                startY = metrics.Control.LinePositions.Positions[line].TextStartY + (INSERT_CURSOR_DIVIDER - 1) * metrics.Character.CharHeight / INSERT_CURSOR_DIVIDER;
                height = metrics.Character.CharHeight / INSERT_CURSOR_DIVIDER;
            }

            return (startY, height);
        }

        private void DrawHexCursor(DrawingContext drawingContext, int ch, int line, int @char, Brush brush, Pen pen)
        {
            (double startY, double height) = GetCursorVerticalData(enteringMode, line);

            drawingContext.DrawRectangle(brush,
                pen,
                new Rect(metrics.Control.CharPositions.HexBytes[ch].Positions[@char].TextCharX,
                    startY,
                    metrics.Character.CharWidth,
                    height));
        }

        private void DrawGhostHexCursor(DrawingContext drawingContext, int ch, int line, Brush brush, Pen pen)
        {
            drawingContext.DrawRectangle(brush,
                pen,
                new Rect(metrics.Control.CharPositions.HexBytes[ch].Positions.First().StartX,
                    metrics.Control.LinePositions.Positions[line].TextStartY,
                    metrics.Control.CharPositions.HexBytes[ch].Positions.Last().EndX - metrics.Control.CharPositions.HexBytes[ch].Positions.First().StartX,
                    metrics.Character.CharHeight));
        }

        private void DrawCharCursor(DrawingContext drawingContext, int ch, int line, Brush brush, Pen pen)
        {
            (double startY, double height) = GetCursorVerticalData(enteringMode, line);
            
            drawingContext.DrawRectangle(brush,
                pen,
                new Rect(metrics.Control.CharPositions.CharBytes[ch].Position.StartX,
                    startY,
                    metrics.Character.CharWidth,
                    height));
        }

        private void DrawGhostCharCursor(DrawingContext drawingContext, int ch, int line, Brush brush, Pen pen)
        {
            drawingContext.DrawRectangle(brush,
                pen,
                new Rect(metrics.Control.CharPositions.CharBytes[ch].Position.StartX,
                    metrics.Control.LinePositions.Positions[line].TextStartY,
                    metrics.Control.CharPositions.CharBytes[ch].Position.EndX - metrics.Control.CharPositions.CharBytes[ch].Position.StartX,
                    metrics.Character.CharHeight));
        }

        private void AddGlyph(char c, double position, GlyphRunInfo info)
        {
            if (glyphTypeface.CharacterToGlyphMap.TryGetValue(c, out ushort glyphIndex))
            {
                info.GlyphIndexes.Add(glyphIndex);
                if (info.GlyphIndexes.Count > 1)
                    info.AdvanceWidths.Add(position - info.CurrentPosition);
                info.CurrentPosition = position;
                if (info.StartPosition == null)
                    info.StartPosition = info.CurrentPosition;
            }
        }

        private void DrawDataSelection(DrawingContext drawingContext, int line)
        {
            if (Document == null)
                throw new InvalidOperationException("Cannot draw data selection, document is null!");

            int offset = ScrollPosition * Document.BytesPerRow;
            int lineOffset = offset + line * Document.BytesPerRow;
            var linePositions = metrics.Control.LinePositions.Positions;
            
            if (selection is RangeSelectionInfo rangeSelectionInfo)
            {
                if (rangeSelectionInfo.SelectionStart <= lineOffset + Document.BytesPerRow - 1 &&
                        rangeSelectionInfo.SelectionEnd >= lineOffset)
                {
                    int startSelByteIndex = Math.Max(lineOffset, rangeSelectionInfo.SelectionStart) - lineOffset;
                    int endSelByteIndex = Math.Min(lineOffset + Document.BytesPerRow - 1, rangeSelectionInfo.SelectionEnd) - lineOffset;

                    // Hex chars selection

                    double startX = metrics.Control.CharPositions.HexBytes[startSelByteIndex].Positions.First().StartX;
                    double endX = metrics.Control.CharPositions.HexBytes[endSelByteIndex].Positions.Last().EndX;
                    double startY = linePositions[line].LineStartY;
                    double endY = linePositions[line].LineEndY;

                    drawingContext.DrawRectangle(SystemColors.HighlightBrush, null, new Rect(startX, startY, endX - startX + 1, endY - startY + 1));

                    // Chars selection

                    startX = metrics.Control.CharPositions.CharBytes[startSelByteIndex].Position.StartX;
                    endX = metrics.Control.CharPositions.CharBytes[endSelByteIndex].Position.EndX;

                    drawingContext.DrawRectangle(SystemColors.HighlightBrush, null, new Rect(startX, startY, endX - startX + 1, endY - startY + 1));
                }
            }
            else if (selection is HexCursorSelectionInfo hexCursorSelectionInfo)
            {
                if (hexCursorSelectionInfo.Offset.IsWithin(lineOffset, lineOffset + Document.BytesPerRow - 1))                    
                {
                    int charIndex = hexCursorSelectionInfo.Offset - lineOffset;

                    DrawHexCursor(drawingContext, charIndex, line, hexCursorSelectionInfo.Char, SystemColors.HighlightBrush, null);
                    DrawGhostCharCursor(drawingContext, charIndex, line, null, new Pen(SystemColors.HighlightBrush, GHOST_SELECTION_THICKNESS));
                }
            }
            else if (selection is CharCursorSelectionInfo charCursorSelectionInfo)
            {
                if (charCursorSelectionInfo.Offset.IsWithin(lineOffset, lineOffset + Document.BytesPerRow - 1))
                {

                    int charIndex = charCursorSelectionInfo.Offset - lineOffset;

                    DrawCharCursor(drawingContext, charIndex, line, SystemColors.HighlightBrush, null);
                    DrawGhostHexCursor(drawingContext, charIndex, line, null, new Pen(SystemColors.HighlightBrush, GHOST_SELECTION_THICKNESS));
                }
            }
        }

        private void DrawGlyphRun(DrawingContext drawingContext, GlyphRunInfo regularRun, Brush brush, double y, double pixelsPerDip)
        {
            if (regularRun.StartPosition != null)
            {
                var glyphRun = new GlyphRun(glyphTypeface,
                    bidiLevel: 0,
                    isSideways: false,
                    renderingEmSize: FontSize,
                    pixelsPerDip: (float)pixelsPerDip,
                    glyphIndices: regularRun.GlyphIndexes,
                    baselineOrigin: new Point(Math.Round(regularRun.StartPosition.Value),
                        Math.Round(glyphTypeface.Baseline * FontSize + y)),
                    advanceWidths: regularRun.AdvanceWidths,
                    glyphOffsets: null,
                    characters: null,
                    deviceFontName: null,
                    clusterMap: null,
                    caretStops: null,
                    language: null);

                drawingContext.DrawGlyphRun(brush, glyphRun);
            }
        }

        private void DrawData(DrawingContext drawingContext, double pixelsPerDip)
        {
            if (Document != null && glyphTypeface != null)
            {
                int maxBytesToDisplay = Document.BytesPerRow * metrics.Control.LinePositions.VisibleLineCount;

                // Grow data buffer according to needs to avoid frequent allocation of arrays
                if (dataBuffer == null || dataBuffer.Length < maxBytesToDisplay)
                    dataBuffer = new byte[maxBytesToDisplay];

                // Address of first byte
                int offset = ScrollPosition * Document.BytesPerRow;
                int availableBytes = Document.GetAvailableBytes(offset, maxBytesToDisplay, dataBuffer, 0);

                // Aliases
                var hexPositions = metrics.Control.CharPositions.HexBytes;
                var charPositions = metrics.Control.CharPositions.CharBytes;
                var linePositions = metrics.Control.LinePositions.Positions;

                // Drawing margin

                drawingContext.DrawRectangle(marginBrush, null, metrics.Control.MarginArea.Rectangle.ToRect());

                for (int line = 0; line < linePositions.Count; line++)
                {
                    int lineOffset = offset + line * Document.BytesPerRow;

                    // Drawing selection

                    DrawDataSelection(drawingContext, line);

                    // Drawing address on margin

                    var regularRun = new GlyphRunInfo();
                    var selectionRun = new GlyphRunInfo();

                    string address = string.Format(metrics.Control.MarginArea.OffsetStringFormat, lineOffset.ToString(metrics.Control.MarginArea.OffsetNumberFormat));

                    double pos = metrics.Control.MarginArea.TextOffsetX;
                    foreach (var ch in address)
                    {
                        AddGlyph(ch, pos, regularRun);
                        pos += glyphTypeface.AdvanceWidths[ch] * FontSize;
                    }

                    // Drawing bytes and characters

                    for (int ch = 0, index = line * Document.BytesPerRow; ch < charPositions.Count && index < availableBytes; ch++, index++)
                    {
                        byte drawnByte = dataBuffer[index];

                        // Generate glyphs for hex values
                        if (enteringMode == EnteringMode.Overwrite && (selection?.IsHexCharSelected(index + offset, 0) ?? false))
                            AddGlyph(hexChars[drawnByte / 16], hexPositions[ch].Positions[0].TextCharX, selectionRun);
                        else
                            AddGlyph(hexChars[drawnByte / 16], hexPositions[ch].Positions[0].TextCharX, regularRun);

                        if (enteringMode == EnteringMode.Overwrite && (selection?.IsHexCharSelected(index + offset, 1) ?? false))
                            AddGlyph(hexChars[drawnByte % 16], hexPositions[ch].Positions[1].TextCharX, selectionRun);
                        else
                            AddGlyph(hexChars[drawnByte % 16], hexPositions[ch].Positions[1].TextCharX, regularRun);
                    }

                    // Drawing additional byte after end of document
                    if (lineOffset <= Document.Size && lineOffset + Document.BytesPerRow > Document.Size)
                    {
                        int ch = Document.Size - lineOffset;

                        for (int i = 0; i < 2; i++)
                        {
                            if (enteringMode == EnteringMode.Overwrite && (selection?.IsHexCharSelected(Document.Size, i) ?? false))
                                AddGlyph('_', hexPositions[ch].Positions[i].TextCharX, selectionRun);
                            else
                                AddGlyph('_', hexPositions[ch].Positions[i].TextCharX, regularRun);
                        }
                    }

                    for (int ch = 0, index = line * Document.BytesPerRow; ch < charPositions.Count && index < availableBytes; ch++, index++)
                    {
                        byte drawnByte = dataBuffer[index];
                        char drawnChar = (drawnByte < 32 || drawnByte > 126) ? '.' : (char)drawnByte;

                        if (enteringMode == EnteringMode.Overwrite && (selection?.IsCharSelected(index + offset) ?? false))
                            AddGlyph(drawnChar, charPositions[ch].Position.TextCharX, selectionRun);
                        else
                            AddGlyph(drawnChar, charPositions[ch].Position.TextCharX, regularRun);
                    }

                    regularRun.FillMissingAdvanceWidths();
                    selectionRun.FillMissingAdvanceWidths();

                    DrawGlyphRun(drawingContext, regularRun, SystemColors.WindowTextBrush, linePositions[line].TextStartY, pixelsPerDip);
                    DrawGlyphRun(drawingContext, selectionRun, SystemColors.HighlightTextBrush, linePositions[line].TextStartY, pixelsPerDip);
                }
            }
        }

        private void DrawHeader(DrawingContext drawingContext, double pixelsPerDip)
        {
            drawingContext.DrawRectangle(headerBrush, null, metrics.Control.HeaderArea.Rectangle.ToRect());

            if (glyphTypeface != null)
            {
                var info = new GlyphRunInfo();

                for (int i = 0; i < metrics.Control.CharPositions.HexBytes.Count; i++)
                {
                    AddGlyph(hexChars[i / 16], metrics.Control.CharPositions.HexBytes[i].Positions[0].TextCharX, info);
                    AddGlyph(hexChars[i % 16], metrics.Control.CharPositions.HexBytes[i].Positions[1].TextCharX, info);
                }

                for (int i = 0; i < metrics.Control.CharPositions.CharBytes.Count; i++)
                {
                    AddGlyph(hexChars[i % 16], metrics.Control.CharPositions.CharBytes[i].Position.TextCharX, info);
                }

                info.FillMissingAdvanceWidths();
                DrawGlyphRun(drawingContext, info, SystemColors.WindowTextBrush, metrics.Control.HeaderArea.HeaderTextY, pixelsPerDip);
            }
        }

        #endregion

        #region Mouse handling

        private BaseMouseHitInfo GetMouseHit(PixelPoint point)
        {
            if (Document != null)
            {
                if (metrics.Control.HexDocumentArea.Rectangle.Contains(point))
                {
                    int line = metrics.Control.LinePositions.VisibleLineCount - 1;
                    while (line >= 0 && !metrics.Control.LinePositions.Positions[line].Contains(point.Y))
                        line--;

                    if (line >= 0)
                    {
                        int hexByte = metrics.Control.CharPositions.HexBytes.Count - 1;
                        int ch = metrics.Control.CharPositions.HexBytes[hexByte].Positions.Length - 1;

                        while (hexByte >= 0 && !metrics.Control.CharPositions.HexBytes[hexByte].Positions[ch].Contains(point.X))
                        {
                            ch--;
                            if (ch < 0)
                            {
                                hexByte--;
                                if (hexByte >= 0)
                                    ch = metrics.Control.CharPositions.HexBytes[hexByte].Positions.Length - 1;
                            }
                        }

                        if (hexByte >= 0 && ch >= 0)
                            return new HexMouseHitInfo((ScrollPosition + line) * Document.BytesPerRow + hexByte, ch);
                    }
                }
                else if (metrics.Control.CharDocumentArea.Rectangle.Contains(point))
                {
                    int line = metrics.Control.LinePositions.VisibleLineCount - 1;
                    while (line >= 0 && !metrics.Control.LinePositions.Positions[line].Contains(point.Y))
                        line--;

                    if (line >= 0)
                    {
                        int charByte = metrics.Control.CharPositions.CharBytes.Count - 1;
                        while (charByte >= 0 && !metrics.Control.CharPositions.CharBytes[charByte].Position.Contains(point.X))
                            charByte--;

                        if (charByte >= 0)
                            return new CharMouseHitInfo((ScrollPosition + line) * Document.BytesPerRow + charByte);
                    }
                }
            }

            return null;
        }

        private void HandleIdleMouseDown(Point point)
        {
            if (Document != null)
            {
                var mouseHit = GetMouseHit(new PixelPoint(point));

                if (mouseHit is HexMouseHitInfo hexMouseHit)
                {
                    if (hexMouseHit.Offset >= 0 && hexMouseHit.Offset <= Document.Size)
                    {
                        mouseMode = MouseMode.HexSelection;
                        mouseData = new HexSelectionMouseData(hexMouseHit.Offset, hexMouseHit.Char, false);

                        // Select hit char
                        Selection = new HexCursorSelectionInfo(hexMouseHit.Offset, hexMouseHit.Char);

                        InvalidateVisual();
                    }
                }
                else if (mouseHit is CharMouseHitInfo charMouseHit)
                {
                    if (charMouseHit.Offset >= 0 && charMouseHit.Offset <= Document.Size)
                    {
                        mouseMode = MouseMode.CharSelection;
                        mouseData = new CharSelectionMouseData(charMouseHit.Offset, false);

                        // Select hit char
                        Selection = new CharCursorSelectionInfo(charMouseHit.Offset);

                        InvalidateVisual();
                    }
                }
            }
        }

        private void HandleCharSelectionMouseMove(Point point)
        {
            if (Document != null)
            {
                var mouseHit = GetMouseHit(new PixelPoint(point));

                if (mouseHit is CharMouseHitInfo charMouseHit)
                {
                    var data = mouseData as CharSelectionMouseData;

                    if (!data.LeftOrigin && (data.OriginOffset != charMouseHit.Offset))
                        data.LeftOrigin = true;

                    if (data.LeftOrigin)
                    {
                        var originOffset = data.OriginOffset.ClampTo(0, Document.Size - 1);
                        var mouseHitOffset = charMouseHit.Offset.ClampTo(0, Document.Size - 1);

                        var start = Math.Min(originOffset, mouseHitOffset);
                        var end = Math.Max(originOffset, mouseHitOffset);
                        var cursorOnStart = start == mouseHitOffset;

                        if (start.IsWithin(0, Document.Size - 1) &&
                            end.IsWithin(0,Document.Size -1 ))
                        {
                            Selection = new RangeSelectionInfo(start, end, DataArea.Char, cursorOnStart);
                            InvalidateVisual();
                        }
                    }
                }
            }
        }

        private void HandleHexSelectionMouseMove(Point point)
        {
            if (Document != null)
            {
                var mouseHit = GetMouseHit(new PixelPoint(point));

                if (mouseHit is HexMouseHitInfo hexMouseHit)
                {
                    var data = mouseData as HexSelectionMouseData;

                    if (!data.LeftOrigin && (data.OriginOffset != hexMouseHit.Offset))
                        data.LeftOrigin = true;

                    if (data.LeftOrigin)
                    {
                        var originOffset = Math.Min(Document.Size - 1, data.OriginOffset);
                        var mouseHitOffset = Math.Min(Document.Size - 1, hexMouseHit.Offset);

                        var start = Math.Min(originOffset, mouseHitOffset);
                        var end = Math.Max(originOffset, mouseHitOffset);
                        var cursorOnStart = start == mouseHitOffset;

                        if (start.IsWithin(0, Document.Size - 1) &&
                            end.IsWithin(0, Document.Size - 1))
                        {
                            Selection = new RangeSelectionInfo(start, end, DataArea.Hex, cursorOnStart);
                            InvalidateVisual();
                        }
                    }
                }
            }
        }

        private void HandleCharSelectionMouseUp(Point point)
        {
            mouseMode = MouseMode.Idle;
            mouseData = null;

            InvalidateVisual();
        }

        private void HandleHexSelectionMouseUp(Point point)
        {
            mouseMode = MouseMode.Idle;
            mouseData = null;

            InvalidateVisual();
        }

        #endregion

        #region Selection handling

        private void MoveSelection(int delta)
        {
            if (Document != null && Document.Size > 0)
            {
                switch (selection)
                {
                    case HexCursorSelectionInfo hexCursorSelection:
                        {
                            int current = hexCursorSelection.Offset.ClampTo(0, Document.Size - 1);
                            int next = (current + delta).ClampTo(0, Document.Size - 1);

                            int selStart = Math.Min(current, next);
                            int selEnd = Math.Max(current, next);
                            bool cursorOnStart = selStart == next;

                            Selection = new RangeSelectionInfo(selStart, selEnd, DataArea.Hex, cursorOnStart);
                            break;
                        }

                    case CharCursorSelectionInfo charCursorSelection:
                        {
                            int current = charCursorSelection.Offset.ClampTo(0, Document.Size - 1);
                            int next = (current + delta).ClampTo(0, Document.Size - 1);

                            int selStart = Math.Min(current, next);
                            int selEnd = Math.Max(current, next);
                            bool cursorOnStart = selStart == next;

                            Selection = new RangeSelectionInfo(selStart, selEnd, DataArea.Char, cursorOnStart);
                            break;
                        }

                    case RangeSelectionInfo rangeSelection:
                        {
                            int cursor = (rangeSelection.Cursor + delta).ClampTo(0, Document.Size - 1);
                            int cursorOpposite = rangeSelection.CursorOpposite;

                            int selStart = Math.Min(cursor, cursorOpposite);
                            int selEnd = Math.Max(cursor, cursorOpposite);

                            bool cursorOnStart = selStart == cursor;

                            Selection = new RangeSelectionInfo(selStart, selEnd, rangeSelection.Area, cursorOnStart);
                            break;
                        }

                    case null:
                        {
                            if (delta > 0)
                            {
                                int cursor = delta.ClampTo(0, Document.Size - 1);
                                Selection = new RangeSelectionInfo(0, cursor, DataArea.Hex, false);
                            }
                            else if (delta < 0)
                            {
                                int cursor = (Document.Size + delta).ClampTo(0, Document.Size - 1);
                                Selection = new RangeSelectionInfo(cursor, Document.Size - 1, DataArea.Hex, true);
                            }

                            break;
                        }

                    default:
                        throw new InvalidOperationException("Invalid selection!");
                }

                InvalidateVisual();
                EnsureCursorVisible();
            }
        }

        private void MoveSelectionUp()
        {
            if (Document != null)
                MoveSelection(-Document.BytesPerRow);
        }

        private void MoveSelectionDown()
        {
            if (Document != null)
                MoveSelection(Document.BytesPerRow);
        }

        private void MoveSelectionPageUp()
        {
            if (Document != null)
                MoveSelection(-(Document.BytesPerRow * metrics.Control.LinePositions.VisibleFullLineCount));
        }

        private void MoveSelectionPageDown()
        {
            if (Document != null)
                MoveSelection(Document.BytesPerRow * metrics.Control.LinePositions.VisibleFullLineCount);
        }

        private void MoveSelectionBack()
        {
            if (Document != null)
                MoveSelection(-1);
        }

        private void MoveSelectionForward()
        {
            if (Document != null)
                MoveSelection(1);
        }

        private void MoveSelectionTo(int offset)
        {
            if (Document != null && Document.Size > 0)
            {
                offset = offset.ClampTo(0, Document.Size);

                switch (selection)
                {
                    case HexCursorSelectionInfo hexCursorSelection:
                        {
                            int current = hexCursorSelection.Offset.ClampTo(0, Document.Size - 1);
                            int next = offset;

                            int selStart = Math.Min(current, next);
                            int selEnd = Math.Max(current, next);
                            bool cursorOnStart = selStart == next;

                            Selection = new RangeSelectionInfo(selStart, selEnd, DataArea.Hex, cursorOnStart);
                            break;
                        }

                    case CharCursorSelectionInfo charCursorSelection:
                        {
                            int current = charCursorSelection.Offset.ClampTo(0, Document.Size);
                            int next = offset;

                            int selStart = Math.Min(current, next);
                            int selEnd = Math.Max(current, next);
                            bool cursorOnStart = selStart == next;

                            Selection = new RangeSelectionInfo(selStart, selEnd, DataArea.Char, cursorOnStart);
                            break;
                        }

                    case RangeSelectionInfo rangeSelection:
                        {
                            int cursor = offset;
                            int cursorOpposite = rangeSelection.CursorOpposite;

                            int selStart = Math.Min(cursor, cursorOpposite);
                            int selEnd = Math.Max(cursor, cursorOpposite);

                            bool cursorOnStart = selStart == cursor;

                            Selection = new RangeSelectionInfo(selStart, selEnd, rangeSelection.Area, cursorOnStart);
                            break;
                        }
                    case null:
                        Selection = new RangeSelectionInfo(0, offset, DataArea.Hex, false);
                        break;
                    default:
                        throw new InvalidOperationException("Invalid selection!");
                }

                InvalidateVisual();
                EnsureCursorVisible();
            }
        }

        private void MoveSelectionHome()
        {
            if (Document != null)
            {
                var offset = GetCursorOffset();
                if (offset != null)
                    MoveSelectionTo(offset.Value - offset.Value % Document.BytesPerRow);                
            }
        }

        private void MoveSelectionEnd()
        {
            if (Document != null)
            {
                var offset = GetCursorOffset();
                if (offset != null)
                    MoveSelectionTo(offset.Value - offset.Value % Document.BytesPerRow + Document.BytesPerRow - 1);
            }
        }

        private void MoveSelectionDataStart()
        {
            if (Document != null)
            {
                var offset = GetCursorOffset();
                if (offset != null)
                    MoveSelectionTo(0);
            }
        }

        private void MoveSelectionDataEnd()
        {
            if (Document != null)
            {
                var offset = GetCursorOffset();
                if (offset != null)
                    MoveSelectionTo(Document.Size - 1);
            }
        }

        #endregion

        #region Cursor handling

        private void MoveCursor(int delta, bool onlyCharsIfPossible)
        {
            if (Document != null)
            {
                switch (selection)
                {
                    case HexCursorSelectionInfo hexCursorSelection:
                        {
                            int current = hexCursorSelection.Offset * HEX_CHAR_COUNT + hexCursorSelection.Char;

                            if (onlyCharsIfPossible)
                                current = (current + delta).ClampTo(0, Document.Size * HEX_CHAR_COUNT + 1);
                            else
                                current = (current + delta * HEX_CHAR_COUNT).ClampTo(0, Document.Size * HEX_CHAR_COUNT);

                            Selection = new HexCursorSelectionInfo(current / HEX_CHAR_COUNT, current % HEX_CHAR_COUNT);
                            break;
                        }

                    case CharCursorSelectionInfo charCursorSelection:
                        {
                            Selection = new CharCursorSelectionInfo((charCursorSelection.Offset + delta).ClampTo(0, Document.Size));
                            break;
                        }
                    case RangeSelectionInfo rangeSelection:
                        {
                            if (rangeSelection.Area == DataArea.Hex)
                            {
                                Selection = new HexCursorSelectionInfo((rangeSelection.Cursor + delta).ClampTo(0, Document.Size), delta > 0 ? 0 : MAX_HEX_CHAR_INDEX);
                            }
                            else if (rangeSelection.Area == DataArea.Char)
                            {
                                Selection = new CharCursorSelectionInfo((rangeSelection.Cursor + delta).ClampTo(0, Document.Size));
                            }
                            else
                                throw new InvalidEnumArgumentException("Unsupported data area!");
                            break;
                        }
                    case null:
                        {
                            Selection = new CharCursorSelectionInfo(delta > 0 ? 0 : Document.Size);
                            break;
                        }
                    default:
                        throw new InvalidOperationException("Unsupported selection!");
                }

                InvalidateVisual();
                EnsureCursorVisible();
            }
        }

        private void MoveUp()
        {
            if (Document != null)
                MoveCursor(-Document.BytesPerRow, false);
        }

        private void MoveDown()
        {
            if (Document != null)
                MoveCursor(Document.BytesPerRow, false);
        }

        private void MovePageUp()
        {
            if (Document != null)
                MoveCursor(-(Document.BytesPerRow * metrics.Control.LinePositions.VisibleFullLineCount), false);
        }

        private void MovePageDown()
        {
            if (Document != null)
                MoveCursor(Document.BytesPerRow * metrics.Control.LinePositions.VisibleFullLineCount, false);
        }

        private void MoveBack()
        {
            if (Document != null)
                MoveCursor(-1, true);
        }

        private void MoveForward()
        {
            if (Document != null)
                MoveCursor(1, true);
        }

        private void MoveCursorTo(int offset, int? @char = null)
        {
            if (Document != null)
            {
                offset = offset.ClampTo(0, Document.Size);

                switch (selection)
                {
                    case HexCursorSelectionInfo hexCursorSelection:
                        {
                            Selection = new HexCursorSelectionInfo(offset, @char != null ? @char.Value : hexCursorSelection.Char);
                            break;
                        }
                    case CharCursorSelectionInfo charCursorSelection:
                        {
                            Selection = new CharCursorSelectionInfo(offset);
                            break;
                        }
                    case RangeSelectionInfo rangeSelection:
                        {
                            switch (rangeSelection.Area)
                            {
                                case DataArea.Hex:
                                    Selection = new HexCursorSelectionInfo(offset, @char != null ? @char.Value : 0);
                                    break;
                                case DataArea.Char:
                                    Selection = new CharCursorSelectionInfo(offset);
                                    break;
                                default:
                                    throw new InvalidEnumArgumentException("Unsupported area");
                            }
                            break;
                        }
                    case null:
                        {
                            Selection = new HexCursorSelectionInfo(offset, @char != null ? @char.Value : 0);
                            break;
                        }
                }

                InvalidateVisual();
                EnsureCursorVisible();
            }
        }

        private void MoveHome()
        {
            if (Document != null)
            {
                var offset = GetCursorOffset();
                if (offset != null)
                    MoveCursorTo(offset.Value - offset.Value % Document.BytesPerRow, 0);
                else
                    MoveCursorTo(0, 0);
            }
        }

        private void MoveEnd()
        {
            if (Document != null)
            {
                var offset = GetCursorOffset();
                if (offset != null)
                    MoveCursorTo(offset.Value - offset.Value % Document.BytesPerRow + Document.BytesPerRow - 1, 1);
                else
                    MoveCursorTo(Document.Size, 1);
            }
        }

        private void MoveDataStart()
        {
            if (Document != null)
            {
                MoveCursorTo(0, 0);
            }
        }

        private void MoveDataEnd()
        {
            if (Document != null)
            {
                MoveCursorTo(Document.Size, MAX_HEX_CHAR_INDEX);
            }
        }

        #endregion

        #region Clipboard and deleting

        private void DoBackspace()
        {
            if (Document != null)
            {
                switch (selection)
                {
                    case HexCursorSelectionInfo hexCursorSelection:
                        {
                            if (hexCursorSelection.Char > 0)
                            {
                                Selection = new HexCursorSelectionInfo(hexCursorSelection.Offset, 0);
                            }
                            else
                            {
                                if (hexCursorSelection.Offset > 0)
                                {
                                    Document.Remove(hexCursorSelection.Offset - 1, RemoveMode.Backspace);
                                    Selection = new HexCursorSelectionInfo(hexCursorSelection.Offset - 1, 0);
                                }
                            }

                            break;
                        }
                    case CharCursorSelectionInfo charCursorSelection:
                        {
                            if (charCursorSelection.Offset > 0)
                            {
                                Document.Remove(charCursorSelection.Offset - 1, RemoveMode.Backspace);
                                Selection = new CharCursorSelectionInfo(charCursorSelection.Offset - 1);
                            }

                            break;
                        }
                    case RangeSelectionInfo rangeSelection:
                        {
                            // Acts like delete in this case

                            DeleteRangeSelection(rangeSelection);

                            break;
                        }
                    case null:
                        {
                            // This is a valid case, just do nothing
                            break;
                        }
                    default:
                        throw new InvalidEnumArgumentException("Unsupported selection type!");
                }
            }
        }

        private void DeleteHexCursor(HexCursorSelectionInfo hexCursorSelection)
        {
            if (hexCursorSelection.Offset < Document.Size)
            {
                Document.Remove(hexCursorSelection.Offset, RemoveMode.Delete);
                Selection = new HexCursorSelectionInfo(hexCursorSelection.Offset, 0);
            }
        }

        private void DeleteCharCursor(CharCursorSelectionInfo charCursorSelection)
        {
            if (charCursorSelection.Offset < Document.Size)
            {
                Document.Remove(charCursorSelection.Offset, RemoveMode.Delete);
                Selection = new CharCursorSelectionInfo(charCursorSelection.Offset);
            }
        }

        private void DeleteRangeSelection(RangeSelectionInfo rangeSelection)
        {
            Document.Remove(rangeSelection.SelectionStart, rangeSelection.SelectionLength);
            switch (rangeSelection.Area)
            {
                case DataArea.Hex:
                    {
                        Selection = new HexCursorSelectionInfo(rangeSelection.SelectionStart, 0);
                        break;
                    }
                case DataArea.Char:
                    {
                        Selection = new CharCursorSelectionInfo(rangeSelection.SelectionStart);
                        break;
                    }
                default:
                    throw new InvalidEnumArgumentException("Unsupported area!");
            }
        }

        private void DoDelete()
        {
            if (Document != null)
            {
                switch (selection)
                {
                    case HexCursorSelectionInfo hexCursorSelection:
                        {
                            DeleteHexCursor(hexCursorSelection);

                            break;
                        }
                    case CharCursorSelectionInfo charCursorSelection:
                        {
                            DeleteCharCursor(charCursorSelection);

                            break;
                        }
                    case RangeSelectionInfo rangeSelection:
                        {
                            DeleteRangeSelection(rangeSelection);

                            break;
                        }
                    case null:
                        {
                            // This is a valid case, just do nothing
                            break;
                        }
                    default:
                        throw new InvalidEnumArgumentException("Unsupported selection type!");
                }
            }
        }

        private void CopyBytes(byte[] data)
        {
            var dataObj = new DataObject();
            dataObj.SetData(typeof(byte[]), data);
            Clipboard.SetDataObject(dataObj, true);
        }

        private void DoCopyToClipboard(bool delete)
        {
            if (Document != null)
            {
                switch (selection)
                {
                    case RangeSelectionInfo rangeSelection:
                        {
                            byte[] buffer = new byte[rangeSelection.SelectionLength];
                            var availableBytes = Document.GetAvailableBytes(rangeSelection.SelectionStart, rangeSelection.SelectionLength, buffer, 0);

                            if (availableBytes != rangeSelection.SelectionLength)
                                throw new InvalidOperationException("Can't get enough bytes from byte container!");

                            CopyBytes(buffer);

                            if (delete)
                            {
                                DeleteRangeSelection(rangeSelection);
                            }

                            break;
                        }
                    case HexCursorSelectionInfo hexCursorSelection:
                        {
                            if (hexCursorSelection.Offset.IsWithin(0, Document.Size - 1))
                            {
                                byte[] buffer = new byte[1];
                                buffer[0] = Document.GetByte(hexCursorSelection.Offset);

                                CopyBytes(buffer);

                                if (delete)
                                {
                                    DeleteHexCursor(hexCursorSelection);
                                }
                            }

                            break;
                        }
                    case CharCursorSelectionInfo charCursorSelection:
                        {
                            if (charCursorSelection.Offset.IsWithin(0, Document.Size - 1))
                            {
                                byte[] buffer = new byte[1];
                                buffer[0] = Document.GetByte(charCursorSelection.Offset);

                                CopyBytes(buffer);

                                if (delete)
                                {
                                    DeleteCharCursor(charCursorSelection);
                                }
                            }

                            break;
                        }
                }
            }
        }

        private void DoPaste(byte[] dataToPaste, int offset)
        {
            switch (enteringMode)
            {
                case EnteringMode.Insert:
                    {
                        Document.Insert(offset, dataToPaste, 0, dataToPaste.Length);
                        break;
                    }
                case EnteringMode.Overwrite:
                    {
                        Document.Replace(offset, dataToPaste, 0, dataToPaste.Length);
                        break;
                    }
                default:
                    break;
            }
        }

        private void PasteFromClipboard()
        {
            if (Document != null)
            {
                // Try to get data to paste

                DataObject retrievedData = Clipboard.GetDataObject() as DataObject;
                if (retrievedData == null || !retrievedData.GetDataPresent(typeof(byte[])))
                    return;

                var dataToPaste = retrievedData.GetData(typeof(byte[])) as byte[];
                if (dataToPaste == null || dataToPaste.Length == 0)
                    return;

                // Check, where to paste and how

                switch (selection)
                {
                    case RangeSelectionInfo rangeSelection:
                        {
                            int offset = rangeSelection.SelectionStart;
                            DeleteRangeSelection(rangeSelection);

                            DoPaste(dataToPaste, offset);

                            Selection = new HexCursorSelectionInfo(offset + dataToPaste.Length, 0);
                            EnsureCursorVisible();

                            break;
                        }
                    case HexCursorSelectionInfo hexCursorSelection:
                        {
                            int offset = hexCursorSelection.Offset;

                            DoPaste(dataToPaste, offset);

                            Selection = new HexCursorSelectionInfo(offset + dataToPaste.Length, 0);
                            EnsureCursorVisible();

                            break;
                        }
                    case CharCursorSelectionInfo charCursorSelection:
                        {
                            int offset = charCursorSelection.Offset;

                            DoPaste(dataToPaste, offset);

                            Selection = new CharCursorSelectionInfo(offset + dataToPaste.Length);
                            EnsureCursorVisible();

                            break;
                        }
                    case null:
                        {
                            // Nowhere to paste
                            return;
                        }
                    default:
                        throw new InvalidOperationException("Unsupported selection type!");
                }
            }
        }

        private void CutToClipboard()
        {
            DoCopyToClipboard(true);
        }

        private void CopyToClipboard()
        {
            DoCopyToClipboard(false);
        }

        #endregion

        #region Entering text handling

        private void HandleCharTextInput(string text)
        {
            if (Document != null)
            {
                foreach (var ch in text)
                {
                    if (ch > MAX_ASCII_CHAR)
                        continue;

                    int offset;
                    if (selection is RangeSelectionInfo rangeSelection)
                    {
                        offset = rangeSelection.SelectionStart;

                        Document.Remove(rangeSelection.SelectionStart, rangeSelection.SelectionLength);
                    }
                    else if (selection is CharCursorSelectionInfo charCursorSelection)
                    {
                        offset = charCursorSelection.Offset;
                    }
                    else
                        throw new InvalidOperationException("Invalid selection!");

                    switch (enteringMode)
                    {
                        case EnteringMode.Insert:
                            {
                                Document.Insert(offset, (byte)ch);
                                break;
                            }
                        case EnteringMode.Overwrite:
                            {
                                if (offset == Document.Size)
                                    Document.Insert(offset, (byte)ch);
                                else
                                    Document.Replace(offset, (byte)ch);

                                break;
                            }
                        default:
                            throw new InvalidEnumArgumentException("Unsupported entering mode!");
                    }

                    offset++;
                    Selection = new CharCursorSelectionInfo(offset);
                }

                InvalidateMetrics();
                EnsureCursorVisible();
            }
        }

        private void HandleHexTextInput(string text)
        {
            if (Document != null)
            {
                foreach (var ch in text)
                {
                    var upperChar = char.ToUpperInvariant(ch);

                    int hexValue;
                    if (upperChar >= '0' && upperChar <= '9')
                        hexValue = upperChar - '0';
                    else if (upperChar >= 'A' && upperChar <= 'F')
                        hexValue = upperChar - 'A' + 10;
                    else
                        continue;

                    int offset, @char;

                    if (selection is RangeSelectionInfo rangeSelection)
                    {
                        offset = rangeSelection.SelectionStart;
                        @char = 0;

                        Document.Remove(rangeSelection.SelectionStart, rangeSelection.SelectionLength);
                    }
                    else if (selection is HexCursorSelectionInfo hexCursorSelection)
                    {
                        offset = hexCursorSelection.Offset;
                        @char = hexCursorSelection.Char;
                    }
                    else
                        throw new InvalidOperationException("Invalid selection type!");

                    byte multiplier = 1;
                    for (int i = 0; i < MAX_HEX_CHAR_INDEX - @char; i++)
                        multiplier *= 16;

                    if (enteringMode == EnteringMode.Insert || offset == Document.Size)
                    {
                        if (@char == 0)
                        {
                            byte b = (byte)(multiplier * hexValue);
                            Document.Insert(offset, b);
                        }
                        else
                        {
                            byte current;
                            if (offset < Document.Size)
                                current = Document.GetByte(offset);
                            else
                                current = 0;

                            current = (byte)(current + (hexValue - ((current / multiplier) % 16)) * multiplier);

                            if (offset < Document.Size)
                                Document.Replace(offset, current);
                            else
                                Document.Insert(offset, current);
                        }
                    }
                    else if (enteringMode == EnteringMode.Overwrite)
                    {
                        var current = Document.GetByte(offset);
                        current = (byte)(current + (hexValue - ((current / multiplier) % 16)) * multiplier);
                        Document.Replace(offset, current);
                    }
                    else
                        throw new InvalidEnumArgumentException("Unsupported entering mode!");

                    @char = @char + 1;
                    if (@char > MAX_HEX_CHAR_INDEX)
                    {
                        offset++;
                        @char = 0;
                    }
                    Selection = new HexCursorSelectionInfo(offset, @char);
                }

                InvalidateMetrics();
                EnsureCursorVisible();
            }
        }

        #endregion

        #region Undo history

        #endregion

        private static bool CheckShiftDown() => Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

        private static bool CheckControlDown() => Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

        // Protected methods --------------------------------------------------

        protected override void OnRender(DrawingContext drawingContext)
        {
            ValidateMetrics();

            drawingContext.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
            try
            {
                drawingContext.DrawRectangle(SystemColors.WindowBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));

                var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

                // Draw header
                DrawHeader(drawingContext, pixelsPerDip);

                // Draw data
                DrawData(drawingContext, pixelsPerDip);

                // Draw footer
                DrawFooter(drawingContext);
            }
            finally
            {
                drawingContext.Pop();
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            metrics.Width = sizeInfo.NewSize.Width;
            metrics.Height = sizeInfo.NewSize.Height;

            UpdateScrollProperties();
            InvalidateVisual();
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            CaptureMouse();

            if (e.ChangedButton == MouseButton.Left)
                Focus();

            var point = e.GetPosition(this);

            if (mouseMode == MouseMode.Idle && e.ChangedButton == MouseButton.Left)
            {
                HandleIdleMouseDown(point);
            }

            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var point = e.GetPosition(this);

            if (mouseMode == MouseMode.HexSelection)
            {
                HandleHexSelectionMouseMove(point);
            }
            else if (mouseMode == MouseMode.CharSelection)
            {
                HandleCharSelectionMouseMove(point);
            }

            e.Handled = true;
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            try
            {
                var point = e.GetPosition(this);

                if (mouseMode == MouseMode.HexSelection && e.ChangedButton == MouseButton.Left)
                {
                    HandleHexSelectionMouseUp(point);
                }
                else if (mouseMode == MouseMode.CharSelection && e.ChangedButton == MouseButton.Left)
                {
                    HandleCharSelectionMouseUp(point);
                }

                e.Handled = true;
            }
            finally
            {
                ReleaseMouseCapture();
            }
        }

        protected override void OnTextInput(TextCompositionEventArgs e)
        {
            if (selection is HexCursorSelectionInfo || (selection is RangeSelectionInfo && ((RangeSelectionInfo)selection).Area == DataArea.Hex))
                HandleHexTextInput(e.Text);
            else if (selection is CharCursorSelectionInfo || (selection is RangeSelectionInfo && ((RangeSelectionInfo)selection).Area == DataArea.Char))
                HandleCharTextInput(e.Text);

            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Left)
            {
                if (CheckShiftDown())
                    MoveSelectionBack();
                else
                    MoveBack();

                e.Handled = true;
            }
            else if (e.Key == Key.Right)
            {
                if (CheckShiftDown())
                    MoveSelectionForward();
                else
                    MoveForward();

                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (CheckShiftDown())
                    MoveSelectionUp();
                else
                    MoveUp();

                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                if (CheckShiftDown())
                    MoveSelectionDown();
                else
                    MoveDown();

                e.Handled = true;
            }
            else if (e.Key == Key.PageUp)
            {
                if (CheckShiftDown())
                    MoveSelectionPageUp();
                else
                    MovePageUp();

                e.Handled = true;
            }
            else if (e.Key == Key.PageDown)
            {
                if (CheckShiftDown())
                    MoveSelectionPageDown();
                else
                    MovePageDown();

                e.Handled = true;
            }
            else if (e.Key == Key.Home)
            {
                if (CheckShiftDown())
                {
                    if (CheckControlDown())
                        MoveSelectionDataStart();
                    else
                        MoveSelectionHome();
                }
                else
                {
                    if (CheckControlDown())
                        MoveDataStart();
                    else
                        MoveHome();
                }

                e.Handled = true;
            }
            else if (e.Key == Key.End)
            {
                if (CheckShiftDown())
                {
                    if (CheckControlDown())
                        MoveSelectionDataEnd();
                    else
                        MoveSelectionEnd();
                }
                else
                {
                    if (CheckControlDown())
                        MoveDataEnd();
                    else
                        MoveEnd();
                }

                e.Handled = true;
            }
            else if (e.Key == Key.Insert)
            {
                if (CheckControlDown() && !CheckShiftDown())
                {
                    CopyToClipboard();
                }
                else if (CheckShiftDown() && !CheckControlDown())
                {
                    PasteFromClipboard();
                }
                else
                {
                    if (enteringMode == EnteringMode.Insert)
                        enteringMode = EnteringMode.Overwrite;
                    else
                        enteringMode = EnteringMode.Insert;

                    InvalidateVisual();
                }

                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                if (CheckShiftDown())
                {
                    CutToClipboard();
                }
                else
                {
                    DoDelete();
                }

                e.Handled = true;
            }
            else if (e.Key == Key.Back)
            {
                DoBackspace();

                e.Handled = true;
            }
        }

        // Public methods -----------------------------------------------------

        public HexEditorDisplay()
        {
            Focusable = true;

            metrics = new Metrics();
            metrics.PixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            BuildTypeface();

            metrics.Typeface = typeface;
            metrics.Width = ActualWidth;
            metrics.Height = ActualHeight;
            metrics.Document = null;
            metrics.FontSize = FontSize;

            metrics.Invalidate();

            dataBuffer = null;

            mouseMode = MouseMode.Idle;
            enteringMode = EnteringMode.Overwrite;
        }

        public void Paste()
        {
            PasteFromClipboard();
        }

        public void Cut()
        {
            CutToClipboard();
        }

        public void Copy()
        {
            CopyToClipboard();
        }

        // Properties ---------------------------------------------------------

        public BaseSelectionInfo Selection
        {
            get => selection;
            set
            {
                SetSelection(value);
            }
        }

        public event EventHandler SelectionChanged;

        // Dependency properties ----------------------------------------------

        #region Document dependency property

        public HexByteContainer Document
        {
            get { return (HexByteContainer)GetValue(DocumentProperty); }
            set { SetValue(DocumentProperty, value); }
        }

        public static readonly DependencyProperty DocumentProperty =
            DependencyProperty.Register("Document", typeof(HexByteContainer), typeof(HexEditorDisplay), new PropertyMetadata(HandleDocumentPropertyChanged));

        private static void HandleDocumentPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorDisplay hexEditorDisplay)
            {
                hexEditorDisplay.HandleDocumentChanged(e.OldValue as HexByteContainer, e.NewValue as HexByteContainer);
            }
        }

        #endregion

        #region FontFamily dependency property

        public string FontFamily
        {
            get { return (string)GetValue(FontFamilyProperty); }
            set { SetValue(FontFamilyProperty, value); }
        }

        public static readonly DependencyProperty FontFamilyProperty =
            DependencyProperty.Register("FontFamily", typeof(string), typeof(HexEditorDisplay), new PropertyMetadata("Consolas", FontFamilyPropertyChanged));

        private static void FontFamilyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorDisplay hexEditorDisplay)
            {
                hexEditorDisplay.HandleFontFamilyChanged();
            }
        }

        #endregion

        #region FontSize dependency property

        public double FontSize
        {
            get { return (double)GetValue(FontSizeProperty); }
            set { SetValue(FontSizeProperty, value); }
        }

        public static readonly DependencyProperty FontSizeProperty =
            DependencyProperty.Register("FontSize", typeof(double), typeof(HexEditorDisplay), new PropertyMetadata(11.0, HandleFontSizePropertyChanged));

        private static void HandleFontSizePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorDisplay hexEditorDisplay)
            {
                hexEditorDisplay.HandleFontSizeChanged();
            }
        }

        #endregion

        #region ScrollPosition dependency property

        public int ScrollPosition
        {
            get { return (int)GetValue(ScrollPositionProperty); }
            set { SetValue(ScrollPositionProperty, value); }
        }

        public static readonly DependencyProperty ScrollPositionProperty =
            DependencyProperty.Register("ScrollPosition", typeof(int), typeof(HexEditorDisplay), new PropertyMetadata(0, HandleLineOffsetPropertyChanged, HandleLineOffsetPropertyCoerce));

        private static object HandleLineOffsetPropertyCoerce(DependencyObject d, object baseValue)
        {
            if (d is HexEditorDisplay hexEditorDisplay && baseValue is int intBaseValue)
            {
                return hexEditorDisplay.HandleScrollPositionCoerce(intBaseValue);
            }

            return baseValue;
        }

        private static void HandleLineOffsetPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditorDisplay hexEditorDisplay)
            {
                hexEditorDisplay.HandleScrollPositionChanged();
            }
        }

        #endregion

        #region ScrollMaximum dependency property

        private static readonly DependencyPropertyKey ScrollMaximumPropertyKey =
            DependencyProperty.RegisterReadOnly("ScrollMaximum", typeof(int), typeof(HexEditorDisplay), new PropertyMetadata(0));

        public static readonly DependencyProperty ScrollMaximumProperty = ScrollMaximumPropertyKey.DependencyProperty;

        public int ScrollMaximum
        {
            get => (int)GetValue(ScrollMaximumProperty);
            private set => SetValue(ScrollMaximumPropertyKey, value);
        }

        #endregion

        #region ScrollLargeChange dependency property

        private static readonly DependencyPropertyKey ScrollLargeChangePropertyKey =
                    DependencyProperty.RegisterReadOnly("ScrollLargeChange", typeof(int), typeof(HexEditorDisplay), new PropertyMetadata(0));

        public static readonly DependencyProperty ScrollLargeChangeProperty = ScrollLargeChangePropertyKey.DependencyProperty;

        public int ScrollLargeChange
        {
            get => (int)GetValue(ScrollLargeChangeProperty);
            private set => SetValue(ScrollLargeChangePropertyKey, value);
        }

        #endregion

        #region ScrollSmallChange dependency property

        private static readonly DependencyPropertyKey ScrollSmallChangePropertyKey =
                    DependencyProperty.RegisterReadOnly("ScrollSmallChange", typeof(int), typeof(HexEditorDisplay), new PropertyMetadata(1));

        public static readonly DependencyProperty ScrollSmallChangeProperty = ScrollSmallChangePropertyKey.DependencyProperty;

        public int ScrollSmallChange
        {
            get => (int)GetValue(ScrollSmallChangeProperty);
            private set => SetValue(ScrollSmallChangePropertyKey, value);
        }

        #endregion
    }
}
