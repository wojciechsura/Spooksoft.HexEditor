using HexEditor.Infrastructure;
using HexEditor.Models;
using HexEditor.Units;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace HexEditor.Controls
{
    public partial class HexEditorDisplay
    {
        private class Metrics
        {
            // Private constants ----------------------------------------------

            private const double HEADER_HEIGHT_MULTIPLIER = 2.0; // * character height
            private const double FOOTER_HEIGHT_MULTIPLIER = 2.0; // * character width
            private const double LINE_MARGIN = 0.4; // * character height
            private const int DEFAULT_ADDRESS_CHARS = 8;
            private const string ADDRESS_PREFIX = "0x";
            private const double ADDRESS_MARGIN_MULTIPLIER = 2.0; // * character width
            private const int MIN_VISIBLE_LINES = 1;
            private const double FIRST_HEX_CHAR_MARGIN_MULTIPLIER = 1.0; // * character width
            private const double LAST_HEX_CHAR_MARGIN_MULTIPLIER = 1.0; // * character width
            private const double FIRST_CHAR_MARGIN_MULTIPLIER = 1.0; // * character width
            private const double LAST_CHAR_MARGIN_MULTIPLIER = 1.0; // * character width
            private const double SPACE_BETWEEN_BYTES_MULTIPLIER = 1.0; // * character width
            private const double SPACE_BETWEEN_CHARS_MULTIPLIER = 0.1; // * character width
            private const int DEFAULT_BYTES_PER_ROW = 16;            

            // Private types -------------------------------------------------

            public class ControlHeightInfos
            {
                public ControlHeightInfos(double headerHeight, double footerHeight, double totalLineHeight, double lineMargin, double requiredHeight, double documentAreaHeight)
                {
                    HeaderHeight = headerHeight;
                    FooterHeight = footerHeight;
                    TotalLineHeight = totalLineHeight;
                    LineMargin = lineMargin;
                    RequiredControlHeight = requiredHeight;
                    DocumentAreaHeight = documentAreaHeight;
                }

                public double HeaderHeight { get; }
                public double FooterHeight { get; }
                public double TotalLineHeight { get; }
                public double LineMargin { get; }
                public double RequiredControlHeight { get; }
                public double DocumentAreaHeight { get; }
            }

            private class ControlOffsetInfos
            {
                public ControlOffsetInfos(int visibleLineCount, 
                    int visibleFullLineCount, 
                    int maxOffset, 
                    int marginOffsetCharCount, 
                    string offsetNumberFormat, 
                    string offsetStringFormat, 
                    double marginWidth, 
                    double offsetTextX)
                {
                    VisibleLineCount = visibleLineCount;
                    VisibleFullLineCount = visibleFullLineCount;
                    MaxOffset = maxOffset;
                    MarginOffsetCharCount = marginOffsetCharCount;
                    OffsetNumberFormat = offsetNumberFormat;
                    OffsetStringFormat = offsetStringFormat;
                    MarginWidth = marginWidth;
                    OffsetTextX = offsetTextX;
                }

                public int VisibleLineCount { get; }
                public int VisibleFullLineCount { get; }
                public int MaxOffset { get; }
                public int MarginOffsetCharCount { get; }
                public string OffsetNumberFormat { get; }
                public string OffsetStringFormat { get; }
                public double MarginWidth { get; }
                public double OffsetTextX { get; }
            }

            private class LineCharInfos
            {
                public LineCharInfos(BytePositions charPositions, LinePositions linePositions)
                {
                    CharPositions = charPositions;
                    LinePositions = linePositions;
                }

                public BytePositions CharPositions { get; }

                public LinePositions LinePositions { get; }
            }

            // Public types --------------------------------------------------

            /* Models */

            public class CharPosition
            {
                public CharPosition(double textCharX, double startX, double endX)
                {
                    TextCharX = textCharX;
                    StartX = startX;
                    EndX = endX;
                }

                public bool Contains(double x) => x >= StartX && x <= EndX;

                public double TextCharX { get; }
                public double StartX { get; }
                public double EndX { get; }
            }

            public class HexBytePositions
            {
                public HexBytePositions(CharPosition[] positions)
                {
                    Positions = positions;
                }

                public CharPosition[] Positions { get; }
            }

            public class CharBytePositions
            {
                public CharBytePositions(CharPosition position)
                {
                    Position = position;
                }

                public CharPosition Position { get; }
            }

            public class BytePositions
            {
                public BytePositions(List<HexBytePositions> hexBytes, List<CharBytePositions> charBytes, double endOfHexArea, double endOfCharArea)
                {
                    HexBytes = hexBytes;
                    CharBytes = charBytes;
                    EndOfHexArea = endOfHexArea;
                    EndOfCharArea = endOfCharArea;
                }

                public List<HexBytePositions> HexBytes { get; }
                public List<CharBytePositions> CharBytes { get; }
                public double EndOfHexArea { get; }
                public double EndOfCharArea { get; }
            }

            public class LinePosition
            {
                public LinePosition(double textStartY, double lineStartY, double lineEndY)
                {
                    TextStartY = textStartY;
                    LineStartY = lineStartY;
                    LineEndY = lineEndY;
                }

                public bool Contains(double y) => y >= LineStartY && y <= LineEndY;

                public double TextStartY { get; }
                public double LineStartY { get; }
                public double LineEndY { get; }
            }

            public class LinePositions
            {
                public LinePositions(List<LinePosition> positions, double lineHeight, int visibleLineCount, int visibleFullLineCount)
                {
                    Positions = positions;
                    LineHeight = lineHeight;
                    VisibleLineCount = visibleLineCount;
                    VisibleFullLineCount = visibleFullLineCount;
                }

                public List<LinePosition> Positions { get; }

                public double LineHeight { get; }
                public int VisibleLineCount { get; }
                public int VisibleFullLineCount { get; }
            }

            /* Metrics */

            public class CharacterMetrics
            {
                public CharacterMetrics(int charWidth, int charHeight)
                {
                    CharWidth = charWidth;
                    CharHeight = charHeight;
                }

                public int CharWidth { get; }
                public int CharHeight { get; }
            }

            public class HeaderAreaInfo
            {
                public HeaderAreaInfo(PixelRectangle rectangle, double headerTextY)
                {
                    Rectangle = rectangle;
                    HeaderTextY = headerTextY;
                }

                public PixelRectangle Rectangle { get; }

                public double HeaderTextY { get; }
            }

            public class MarginAreaInfo
            {
                public MarginAreaInfo(PixelRectangle rectangle, string offsetNumberFormat, string offsetStringFormat, double textOffsetX)
                {
                    Rectangle = rectangle;
                    OffsetNumberFormat = offsetNumberFormat;
                    OffsetStringFormat = offsetStringFormat;
                    TextOffsetX = textOffsetX;
                }

                public PixelRectangle Rectangle { get; }
                public string OffsetNumberFormat { get; }
                public string OffsetStringFormat { get; }
                public double TextOffsetX { get; }
            }

            public class FooterAreaInfo
            {
                public FooterAreaInfo(PixelRectangle rectangle)
                {
                    Rectangle = rectangle;
                }

                public PixelRectangle Rectangle { get; }
            }

            public class HexDocumentAreaInfo
            {
                public HexDocumentAreaInfo(PixelRectangle rectangle)
                {
                    Rectangle = rectangle;
                }

                public PixelRectangle Rectangle { get; }
            }

            public class CharDocumentAreaInfo
            {
                public CharDocumentAreaInfo(PixelRectangle rectangle)
                {
                    Rectangle = rectangle;
                }

                public PixelRectangle Rectangle { get; }
            }

            public class ControlMetrics
            {
                public ControlMetrics(HeaderAreaInfo headerArea,
                    MarginAreaInfo marginArea,
                    HexDocumentAreaInfo hexDocumentArea,
                    CharDocumentAreaInfo charDocumentArea,
                    FooterAreaInfo footerArea,
                    BytePositions charPositions,
                    LinePositions linePositions)
                {
                    HeaderArea = headerArea;
                    MarginArea = marginArea;
                    HexDocumentArea = hexDocumentArea;
                    CharDocumentArea = charDocumentArea;
                    FooterArea = footerArea;
                    CharPositions = charPositions;
                    LinePositions = linePositions;                    
                }

                public HeaderAreaInfo HeaderArea { get; }
                public MarginAreaInfo MarginArea { get; }
                public HexDocumentAreaInfo HexDocumentArea { get; }
                public CharDocumentAreaInfo CharDocumentArea { get; }
                public FooterAreaInfo FooterArea { get; }
                public BytePositions CharPositions { get; }
                public LinePositions LinePositions { get; }                
            }

            public class ScrollMetrics
            {
                public ScrollMetrics(int maximum, int largeChange)
                {
                    Maximum = maximum;
                    LargeChange = largeChange;
                }

                public int Maximum { get; }
                public int LargeChange { get; }
            }

            // Private fields -------------------------------------------------

            private HexByteContainer document;
            private Typeface typeface;
            private double fontSize;
            private double pixelsPerDip;
            private double width;
            private double height;

            private CharacterMetrics characterMetrics;
            private bool characterMetricsValid;

            private ControlMetrics controlMetrics;
            private bool controlMetricsValid;

            private ScrollMetrics scrollMetrics;
            private bool scrollMetricsValid;

            // Private methods ------------------------------------------------

            private ControlHeightInfos EvalHeightInfos()
            {
                if (!characterMetricsValid)
                    throw new InvalidOperationException("Character metrics must be valid for this!");

                var headerHeight = characterMetrics.CharHeight * HEADER_HEIGHT_MULTIPLIER;
                var footerHeight = characterMetrics.CharHeight * FOOTER_HEIGHT_MULTIPLIER;
                var lineMargin = characterMetrics.CharHeight * LINE_MARGIN;
                var totalLineHeight = characterMetrics.CharHeight + lineMargin;
                var requiredHeight = Math.Max(headerHeight + totalLineHeight * MIN_VISIBLE_LINES + footerHeight, height);
                var documentAreaHeight = requiredHeight - headerHeight - footerHeight;

                return new ControlHeightInfos(headerHeight, footerHeight, totalLineHeight, lineMargin, requiredHeight, documentAreaHeight);
            }

            private ControlOffsetInfos EvalOffsetInfos(ControlHeightInfos heightInfos)
            {
                // We allow to set cursor on a byte after last data byte, thus Size + 1
                var maxOffset = document?.Size + 1 ?? 0;
                var visibleLineCount = (int)Math.Ceiling((heightInfos.DocumentAreaHeight - heightInfos.LineMargin) / heightInfos.TotalLineHeight);
                var visibleFullLineCount = (int)Math.Floor((heightInfos.DocumentAreaHeight - heightInfos.LineMargin) / heightInfos.TotalLineHeight);
                var offsetValueChars = Math.Max(DEFAULT_ADDRESS_CHARS, (int)Math.Log(maxOffset, 16));
                var offsetChars = ADDRESS_PREFIX.Length + offsetValueChars;
                var marginWidth = (offsetChars + ADDRESS_MARGIN_MULTIPLIER * 2) * characterMetrics.CharWidth;
                var offsetX = characterMetrics.CharWidth * ADDRESS_MARGIN_MULTIPLIER;
                string offsetFormat = $"X{offsetValueChars}";
                string offsetTemplate = $"{ADDRESS_PREFIX}{{0}}";

                return new ControlOffsetInfos(visibleLineCount, visibleFullLineCount, maxOffset, offsetChars, offsetFormat, offsetTemplate, marginWidth, offsetX);
            }

            private LineCharInfos EvalRowColInfos(ControlHeightInfos heightInfos, ControlOffsetInfos offsetInfos)
            {
                if (!characterMetricsValid)
                    throw new InvalidOperationException("Character metrics must be valid for this!");

                var bytesPerRow = document?.BytesPerRow ?? DEFAULT_BYTES_PER_ROW;

                var charCols = EvalCharColumns(offsetInfos, bytesPerRow);
                var lines = EvalLineRows(heightInfos, offsetInfos);

                return new LineCharInfos(charCols, lines);
            }

            private List<HexBytePositions> EvalHexBytePositions(int bytesPerRow, ref double current)
            {
                double firstHexCharOffset = characterMetrics.CharWidth * FIRST_HEX_CHAR_MARGIN_MULTIPLIER;
                double lastHexCharOffset = characterMetrics.CharWidth * LAST_HEX_CHAR_MARGIN_MULTIPLIER;
                double spaceBetweenBytesHalf = characterMetrics.CharWidth * SPACE_BETWEEN_BYTES_MULTIPLIER / 2.0;

                current += firstHexCharOffset;

                var hexCharPositions = new List<HexBytePositions>();

                for (int i = 0; i < bytesPerRow; i++)
                {
                    var char1Start = current;

                    if (i > 0)
                        current += spaceBetweenBytesHalf;

                    var char1Text = current;
                    current += characterMetrics.CharWidth;
                    var char2Text = current;
                    current += characterMetrics.CharWidth;

                    double char2End;

                    if (i < bytesPerRow - 1)
                    {
                        current += spaceBetweenBytesHalf;
                        char2End = current;
                    }
                    else
                    {
                        char2End = current;
                        current += lastHexCharOffset;
                    }

                    hexCharPositions.Add(new HexBytePositions(new[] {
                        new CharPosition(char1Text, char1Start, char2Text),
                        new CharPosition(char2Text, char2Text, char2End)
                    }));
                }

                return hexCharPositions;
            }

            private List<CharBytePositions> EvalCharBytePositions(int bytesPerRow, ref double current)
            {
                double firstCharOffset = characterMetrics.CharWidth * FIRST_CHAR_MARGIN_MULTIPLIER;
                double lastCharOffset = characterMetrics.CharWidth * LAST_CHAR_MARGIN_MULTIPLIER;
                double spaceBetweenCharsHalf = characterMetrics.CharWidth * SPACE_BETWEEN_CHARS_MULTIPLIER / 2.0;

                current += firstCharOffset;

                var charPositions = new List<CharBytePositions>();

                for (int i = 0; i < bytesPerRow; i++)
                {
                    var startPosition = current;

                    if (i > 0)
                        current += spaceBetweenCharsHalf;

                    var textPosition = current;
                    current += characterMetrics.CharWidth;

                    double endPosition;

                    if (i < bytesPerRow - 1)
                    {
                        current += spaceBetweenCharsHalf;
                        endPosition = current;
                    }
                    else
                    {
                        endPosition = current;
                        current += lastCharOffset;
                    }

                    charPositions.Add(new CharBytePositions(new CharPosition(textPosition, startPosition, endPosition)));
                }

                return charPositions;
            }

            private BytePositions EvalCharColumns(ControlOffsetInfos offsetInfos, int bytesPerRow)
            {
                if (!characterMetricsValid)
                    throw new InvalidOperationException("Character metrics must be valid for this!");

                // Evaluating hex char columns

                double documentAreaX = offsetInfos.MarginWidth + 1;

                double current = documentAreaX;

                List<HexBytePositions> hexCharPositions = EvalHexBytePositions(bytesPerRow, ref current);

                double endOfHexArea = current;

                // Evaluating regular char columns

                List<CharBytePositions> charPositions = EvalCharBytePositions(bytesPerRow, ref current);

                double endOfCharArea = current;

                return new BytePositions(hexCharPositions, charPositions, endOfHexArea, endOfCharArea);
            }

            private LinePositions EvalLineRows(ControlHeightInfos heightInfos, ControlOffsetInfos offsetInfos)
            {
                double documentAreaY = heightInfos.HeaderHeight + 1;

                var lines = new List<LinePosition>();

                var current = documentAreaY;

                for (int i = 0; i < offsetInfos.VisibleLineCount; i++)
                {
                    lines.Add(new LinePosition(current + heightInfos.LineMargin / 2.0, current, current + heightInfos.TotalLineHeight));
                    current += heightInfos.TotalLineHeight;
                }

                return new LinePositions(lines, heightInfos.TotalLineHeight, offsetInfos.VisibleLineCount, offsetInfos.VisibleFullLineCount);
            }

            // *** Invalidation ***

            private void InvalidateCharacterMetrics()
            {
                characterMetricsValid = false;
                characterMetrics = null;

                InvalidateControlMetrics();
            }

            private void InvalidateControlMetrics()
            {
                controlMetricsValid = false;
                controlMetrics = null;

                InvalidateScrollMetrics();
            }

            private void InvalidateScrollMetrics()
            {
                scrollMetricsValid = false;
            }

            // *** Validation ***

            private void ValidateCharacterMetrics()
            {
                if (characterMetricsValid)
                    return;

                FormattedText text = new FormattedText("W",
                    CultureInfo.InvariantCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    typeface,
                    fontSize,
                    Brushes.Black,
                    pixelsPerDip);

                characterMetrics = new CharacterMetrics((int)Math.Ceiling(text.Width), (int)Math.Ceiling(text.Height));
                characterMetricsValid = true;
            }

            private void ValidateControlMetrics()
            {
                if (controlMetricsValid)
                    return;

                ValidateCharacterMetrics();

                ControlHeightInfos heightInfos = EvalHeightInfos();
                ControlOffsetInfos offsetInfos = EvalOffsetInfos(heightInfos);
                LineCharInfos rowColInfos = EvalRowColInfos(heightInfos, offsetInfos);

                double right = Math.Max(rowColInfos.CharPositions.EndOfCharArea, width - 1);

                PixelRectangle headerArea = new PixelRectangle(0, 0, right, heightInfos.HeaderHeight);
                PixelRectangle footerArea = new PixelRectangle(0, heightInfos.RequiredControlHeight - heightInfos.FooterHeight, right, heightInfos.RequiredControlHeight);
                PixelRectangle addressMarginArea = new PixelRectangle(0, heightInfos.HeaderHeight + 1, offsetInfos.MarginWidth, heightInfos.RequiredControlHeight - heightInfos.FooterHeight - 1);
                PixelRectangle hexDocumentArea = new PixelRectangle(0, heightInfos.HeaderHeight + 1, rowColInfos.CharPositions.EndOfHexArea, heightInfos.RequiredControlHeight - heightInfos.FooterHeight - 1);
                PixelRectangle charDocumentArea = new PixelRectangle(rowColInfos.CharPositions.EndOfHexArea + 1, heightInfos.HeaderHeight + 1, rowColInfos.CharPositions.EndOfCharArea, heightInfos.RequiredControlHeight - heightInfos.FooterHeight - 1);

                controlMetrics = new ControlMetrics(new HeaderAreaInfo(headerArea, headerArea.Center.Y - characterMetrics.CharHeight / 2.0),                    
                    new MarginAreaInfo(addressMarginArea, offsetInfos.OffsetNumberFormat, offsetInfos.OffsetStringFormat, offsetInfos.OffsetTextX), 
                    new HexDocumentAreaInfo(hexDocumentArea), 
                    new CharDocumentAreaInfo(charDocumentArea),
                    new FooterAreaInfo(footerArea), 
                    rowColInfos.CharPositions, 
                    rowColInfos.LinePositions);
                controlMetricsValid = true;
            }

            private void ValidateScrollMetrics()
            {
                if (scrollMetricsValid)
                    return;

                ValidateControlMetrics();

                int maximum;
                int largeChange;

                // Document size + 1 to make place for one byte after last document byte
                if (document != null)
                {
                    maximum = Math.Max(0, (document.Size + 1) / document.BytesPerRow) - Math.Max(0, controlMetrics.LinePositions.Positions.Count - 2);
                    largeChange = controlMetrics.LinePositions.Positions.Count;
                }
                else
                {
                    maximum = 0;
                    largeChange = 0;
                }

                scrollMetrics = new ScrollMetrics(maximum, largeChange);
                scrollMetricsValid = true;
            }

            private void SetDocument(HexByteContainer value)
            {
                if (document != value)
                {
                    document = value;
                    Invalidate();
                }
            }

            private void SetTypface(Typeface value)
            {
                if (typeface != value)
                {
                    typeface = value;
                    Invalidate();
                }
            }

            private void SetFontSize(double value)
            {
                if (fontSize != value)
                {
                    fontSize = value;
                    Invalidate();
                }
            }

            private void SetPixelsPerDip(double value)
            {
                if (pixelsPerDip != value)
                {
                    pixelsPerDip = value;
                    Invalidate();
                }
            }

            private void SetWidth(double value)
            {
                if (width != value)
                {
                    width = value;
                    Invalidate();
                }
            }

            private void SetHeight(double value) 
            {
                if (height != value)
                {
                    height = value;
                    Invalidate();
                }
            }

            // Public methods -------------------------------------------------

            public Metrics()
            {
                Invalidate();
            }
           
            public void Invalidate()
            {
                InvalidateCharacterMetrics();
                InvalidateControlMetrics();
                InvalidateScrollMetrics();
            }

            public void Validate()
            {
                if (!characterMetricsValid)
                    ValidateCharacterMetrics();
                if (!controlMetricsValid)
                    ValidateControlMetrics();
                if (!scrollMetricsValid)
                    ValidateScrollMetrics();
            }

            // Public properties ----------------------------------------------

            public HexByteContainer Document
            {
                get => document;
                set
                {
                    SetDocument(value);
                }
            }

            public Typeface Typeface
            {
                get => typeface;
                set
                {
                    SetTypface(value);
                }
            }

            public double FontSize
            {
                get => fontSize;
                set
                {
                    SetFontSize(value);
                }
            }

            public double PixelsPerDip
            {
                get => pixelsPerDip;
                set
                {
                    SetPixelsPerDip(value);
                }
            }

            public double Width
            {
                get => width;
                set
                {
                    SetWidth(value);
                }
            }

            public double Height
            {
                get => height;
                set
                {
                    SetHeight(value);
                }
            }
            
            public bool Valid => characterMetricsValid && controlMetricsValid && scrollMetricsValid;

            public CharacterMetrics Character => characterMetrics;

            public ControlMetrics Control => controlMetrics;

            public ScrollMetrics Scroll => scrollMetrics;
        }
    }
}
