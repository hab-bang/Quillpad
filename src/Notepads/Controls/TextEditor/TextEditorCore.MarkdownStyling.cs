// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Controls.TextEditor
{
    using System;
    using System.Text;
    using Notepads.Services;
    using Notepads.Utilities;
    using Windows.System;
    using Windows.UI;
    using Windows.UI.Core;
    using Windows.UI.Input;
    using Windows.UI.Text;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Media;

    /// <summary>
    /// Shows markdown emphasis, headings, links and code in the editor itself, without the preview pane.
    /// Only the look of the text changes (weight, slant, colour, underline). The characters stay the same,
    /// so what is saved, copied or counted is exactly what the user typed.
    ///
    /// The RichEditBox puts every format change in its undo history, and it cannot pause that.
    /// So this code changes a character's format only when it is wrong, and Undo/Redo step over
    /// entries that change format only (see UndoSkippingFormatChanges and RedoSkippingFormatChanges).
    ///
    /// Each format change also costs more as the document grows. A freshly opened file is therefore
    /// styled in one step, by loading the same text as RTF (see TryRestyleMarkdownWithRtf), and files
    /// longer than MarkdownStylingMaxLength stay plain.
    /// </summary>
    public partial class TextEditorCore
    {
        private static readonly Color LightDimColor = Color.FromArgb(0xFF, 0x9A, 0x9A, 0x9A);
        private static readonly Color LightLinkColor = Color.FromArgb(0xFF, 0x05, 0x63, 0xC1);
        private static readonly Color LightCodeColor = Color.FromArgb(0xFF, 0xA3, 0x15, 0x15);
        private static readonly Color DarkDimColor = Color.FromArgb(0xFF, 0x7C, 0x7C, 0x7C);
        private static readonly Color DarkLinkColor = Color.FromArgb(0xFF, 0x5C, 0xA8, 0xFF);
        private static readonly Color DarkCodeColor = Color.FromArgb(0xFF, 0xD6, 0x9D, 0x85);

        private const int MarkdownStylingMaxLength = 100000; // About 17,000 words

        private bool _isMarkdownStylingEnabled;
        private bool _isMarkdownRestyleScheduled;
        private bool _isMarkdownFullRestyleRequested;
        private bool _isTextComposing;
        private string[] _markdownStyledLines; // Lines as they were at the last restyle
        private MarkdownLineContext[] _markdownStyledContexts;

        private struct MarkdownPalette
        {
            public Color Base;
            public Color Dim;
            public Color Link;
            public Color Code;
            public int BaseWeight;
            public int BoldWeight;
            public bool BaseItalic;
        }

        public bool IsMarkdownStylingEnabled
        {
            get => _isMarkdownStylingEnabled;
            set
            {
                if (_isMarkdownStylingEnabled == value) return;
                _isMarkdownStylingEnabled = value;

                if (value)
                {
                    RequestFullMarkdownRestyle();
                }
                else
                {
                    _markdownStyledLines = null;
                    _markdownStyledContexts = null;
                    ResetMarkdownStyling();
                }
            }
        }

        private void HookMarkdownStylingEvents()
        {
            FontSizeChanged += OnFontSizeChangedForMarkdown;
            ActualThemeChanged += OnActualThemeChangedForMarkdown;
            TextCompositionStarted += OnTextCompositionStartedForMarkdown;
            TextCompositionEnded += OnTextCompositionEndedForMarkdown;
            AddHandler(PointerReleasedEvent, new PointerEventHandler(OnPointerReleasedForMarkdownLinks), handledEventsToo: true);
        }

        private void UnhookMarkdownStylingEvents()
        {
            FontSizeChanged -= OnFontSizeChangedForMarkdown;
            ActualThemeChanged -= OnActualThemeChangedForMarkdown;
            TextCompositionStarted -= OnTextCompositionStartedForMarkdown;
            TextCompositionEnded -= OnTextCompositionEndedForMarkdown;
            RemoveHandler(PointerReleasedEvent, new PointerEventHandler(OnPointerReleasedForMarkdownLinks));
        }

        /// <summary>
        /// Call after the whole text was replaced: every character then has the same format,
        /// so the lines that did not change must be styled again too.
        /// </summary>
        private void RequestFullMarkdownRestyle()
        {
            if (!_isMarkdownStylingEnabled) return;
            _isMarkdownFullRestyleRequested = true;
            ScheduleMarkdownRestyle();
        }

        private void ScheduleMarkdownRestyle()
        {
            if (!_isMarkdownStylingEnabled || _isMarkdownRestyleScheduled) return;
            _isMarkdownRestyleScheduled = true;

            // Runs after the current edit is done, so a burst of changes (Replace All, paste) restyles once
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                _isMarkdownRestyleScheduled = false;
                if (_isTextComposing) return; // Restyled when the IME composition ends
                RestyleMarkdown();
            });
        }

        /// <summary>
        /// Paragraphs loaded from RTF keep their own line spacing, so zooming must set it on them too.
        /// </summary>
        private void OnFontSizeChangedForMarkdown(object sender, double fontSize)
        {
            if (!_markdownLoadedFromRtf) return;
            var length = GetText().Length;
            if (length == 0) return;
            Document.GetRange(0, length).ParagraphFormat.SetLineSpacing(LineSpacingRule.Exactly, (float)fontSize);
        }

        private bool _markdownLoadedFromRtf;

        private void OnActualThemeChangedForMarkdown(FrameworkElement sender, object args)
        {
            RequestFullMarkdownRestyle();
        }

        private void OnTextCompositionStartedForMarkdown(Windows.UI.Xaml.Controls.RichEditBox sender, Windows.UI.Xaml.Controls.TextCompositionStartedEventArgs args)
        {
            _isTextComposing = true;
        }

        private void OnTextCompositionEndedForMarkdown(Windows.UI.Xaml.Controls.RichEditBox sender, Windows.UI.Xaml.Controls.TextCompositionEndedEventArgs args)
        {
            _isTextComposing = false;
            ScheduleMarkdownRestyle();
        }

        private MarkdownPalette GetMarkdownPalette()
        {
            var dark = ActualTheme == ElementTheme.Dark;
            var baseWeight = FontWeight.Weight;

            return new MarkdownPalette
            {
                Base = (Foreground as SolidColorBrush)?.Color ?? (dark ? Colors.White : Colors.Black),
                Dim = dark ? DarkDimColor : LightDimColor,
                Link = dark ? DarkLinkColor : LightLinkColor,
                Code = dark ? DarkCodeColor : LightCodeColor,
                BaseWeight = baseWeight,
                BoldWeight = Math.Min(900, Math.Max(700, baseWeight + 300)),
                BaseItalic = FontStyle != FontStyle.Normal,
            };
        }

        private void RestyleMarkdown()
        {
            if (!_isMarkdownStylingEnabled) return;

            var text = GetText();

            // Each format change costs more as the document grows, so very long files stay plain
            if (text.Length > MarkdownStylingMaxLength)
            {
                if (_markdownStyledLines != null) ResetMarkdownStyling();
                _markdownStyledLines = null;
                _markdownStyledContexts = null;
                return;
            }

            var lines = text.Split(RichEditBoxDefaultLineEnding);
            var contexts = MarkdownLiveStyleParser.GetLineContexts(lines);
            var full = _isMarkdownFullRestyleRequested || _markdownStyledLines == null;
            _isMarkdownFullRestyleRequested = false;

            var first = 0;
            var last = lines.Length; // Exclusive

            if (!full)
            {
                var oldLines = _markdownStyledLines;
                var oldContexts = _markdownStyledContexts;
                var shortest = Math.Min(lines.Length, oldLines.Length);

                while (first < shortest && lines[first] == oldLines[first] && contexts[first] == oldContexts[first]) first++;

                var fromEnd = 0;
                while (fromEnd < shortest - first &&
                       lines[lines.Length - 1 - fromEnd] == oldLines[oldLines.Length - 1 - fromEnd] &&
                       contexts[lines.Length - 1 - fromEnd] == oldContexts[oldContexts.Length - 1 - fromEnd])
                {
                    fromEnd++;
                }

                last = lines.Length - fromEnd;
            }

            _markdownStyledLines = lines;
            _markdownStyledContexts = contexts;

            if (first >= last) return;

            // Styling a whole file one change at a time is slow, so load it as styled RTF in one step
            if (full && text.Length > 0 && TryRestyleMarkdownWithRtf(text, lines, contexts)) return;

            var lineStart = 0;
            for (var i = 0; i < first; i++) lineStart += lines[i].Length + 1;

            RunMarkdownFormatting(formatter =>
            {
                if (full && text.Length > 0)
                {
                    formatter.Apply(0, text.Length, 0, UnknownMarkdownStyle);
                }

                var styles = new byte[0];
                for (var i = first; i < last; i++)
                {
                    var line = lines[i];
                    if (styles.Length < line.Length) styles = new byte[Math.Max(line.Length, styles.Length * 2)];
                    MarkdownLiveStyleParser.StyleLine(line, contexts[i], styles);

                    // After a full reset every character is plain, so there is nothing to read back
                    var actual = full ? null : ReadMarkdownStyles(lineStart, line.Length, formatter.Palette);
                    formatter.ApplyRuns(lineStart, line.Length, styles, actual);

                    lineStart += line.Length + 1;
                }
            });
        }

        /// <summary>
        /// Move line, join lines and indenting set the whole text. That would reset every format and need a
        /// full restyle, so for styled markdown only the part that changed is replaced. It is still one undo step.
        /// </summary>
        private bool TryReplaceChangedTextOnly(string text)
        {
            if (!_isMarkdownStylingEnabled || _markdownStyledLines == null) return false;

            var old = GetText();
            if (old.Length == 0 || text.Length == 0) return false;

            var prefix = 0;
            var shortest = Math.Min(old.Length, text.Length);
            while (prefix < shortest && old[prefix] == text[prefix]) prefix++;

            var suffix = 0;
            while (suffix < shortest - prefix && old[old.Length - 1 - suffix] == text[text.Length - 1 - suffix]) suffix++;

            if (prefix == old.Length && prefix == text.Length) return true; // Same text: nothing to do

            // A large change (opening another file) is faster as a full restyle
            if (Math.Max(old.Length, text.Length) - prefix - suffix > 10000) return false;

            Document.GetRange(prefix, old.Length - suffix).SetText(TextSetOptions.None, text.Substring(prefix, text.Length - prefix - suffix));
            return true;
        }

        private bool _isMarkdownRtfUnavailable; // Set when an RTF load did not give back the exact text

        /// <summary>
        /// Replaces the document with the same text written as RTF, with the markdown styles in it.
        /// One SetText is much faster than thousands of format changes. The text is checked after the
        /// load: if it is not exactly the same, the plain text is put back and false is returned.
        /// </summary>
        private bool TryRestyleMarkdownWithRtf(string text, string[] lines, MarkdownLineContext[] contexts)
        {
            if (_isMarkdownRtfUnavailable) return false;

            // Only for a document with no undo history (a file that was just opened). When an RTF load
            // meets undo history, later undo work can hang the RichEditBox, so the slower path is used.
            if (Document.CanUndo() || Document.CanRedo()) return false;

            var palette = GetMarkdownPalette();

            // RTF can only say "bold" (700) or not (400), and cannot keep an italic base font
            if (palette.BaseWeight != 400 || palette.BoldWeight != 700 || palette.BaseItalic) return false;

            var rtf = BuildMarkdownRtf(lines, contexts, palette);

            var selectionStart = Document.Selection.StartPosition;
            var selectionEnd = Document.Selection.EndPosition;
            var horizontalOffset = _contentScrollViewer?.HorizontalOffset ?? 0;
            var verticalOffset = _contentScrollViewer?.VerticalOffset ?? 0;
            var defaultTabStop = Document.DefaultTabStop;

            // Undo stays off during the load so that it is never in the undo history
            var undoLimit = Document.UndoLimit;
            var undoDisabled = undoLimit > 0;
            if (undoDisabled) Document.UndoLimit = 0;

            var succeeded = false;
            Document.BatchDisplayUpdates();
            try
            {
                Document.SetText(TextSetOptions.FormatRtf, rtf);
                succeeded = ReadDocumentText() == text;
                _markdownLoadedFromRtf = succeeded;

                if (!succeeded)
                {
                    _isMarkdownRtfUnavailable = true;
                    LoggingService.LogError($"[{nameof(TextEditorCore)}] RTF markdown styling changed the text, falling back to plain styling");
                    Document.SetText(TextSetOptions.None, text);
                }

                Document.DefaultTabStop = defaultTabStop;
                SetDefaultTabStopAndLineSpacing(FontFamily, FontSize);
                Document.Selection.SetRange(selectionStart, selectionEnd);
            }
            catch (Exception ex)
            {
                _isMarkdownRtfUnavailable = true;
                LoggingService.LogError($"[{nameof(TextEditorCore)}] RTF markdown styling failed: {ex.Message}");
                if (ReadDocumentText() != text) Document.SetText(TextSetOptions.None, text);
                succeeded = false;
            }
            finally
            {
                Document.ApplyDisplayUpdates();
                if (undoDisabled) Document.UndoLimit = undoLimit;
            }

            _contentScrollViewer?.ChangeView(horizontalOffset, verticalOffset, zoomFactor: null, disableAnimation: true);
            return succeeded;
        }

        private string ReadDocumentText()
        {
            Document.GetText(TextGetOptions.None, out var text);
            return TrimRichEditBoxText(text);
        }

        private string BuildMarkdownRtf(string[] lines, MarkdownLineContext[] contexts, MarkdownPalette palette)
        {
            var fontName = (FontFamily?.Source ?? "Consolas").Split(',')[0].Trim().Replace("\\", "").Replace("{", "").Replace("}", "").Replace(";", "");
            var halfPoints = (int)Math.Round(FontSize * 72 / 96 * 2); // FontSize is in pixels, RTF uses half-points

            var rtf = new StringBuilder(lines.Length * 64);
            rtf.Append(@"{\rtf1\ansi\uc1\deff0{\fonttbl{\f0\fnil ").Append(fontName).Append(";}}");
            rtf.Append(@"{\colortbl;");
            AppendRtfColor(rtf, palette.Base); // \cf1
            AppendRtfColor(rtf, palette.Dim);  // \cf2
            AppendRtfColor(rtf, palette.Link); // \cf3
            AppendRtfColor(rtf, palette.Code); // \cf4
            rtf.Append(@"}\f0\fs").Append(halfPoints).Append(' ');

            var styles = new byte[0];
            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex];
                if (styles.Length < line.Length) styles = new byte[Math.Max(line.Length, styles.Length * 2)];
                MarkdownLiveStyleParser.StyleLine(line, contexts[lineIndex], styles);

                var i = 0;
                while (i < line.Length)
                {
                    var style = styles[i];
                    var j = i + 1;
                    while (j < line.Length && styles[j] == style) j++;

                    rtf.Append(@"{\cf").Append(((style & MarkdownLiveStyleParser.ColorMask) >> 4) + 1);
                    if ((style & MarkdownLiveStyleParser.Bold) != 0) rtf.Append(@"\b");
                    if ((style & MarkdownLiveStyleParser.Italic) != 0) rtf.Append(@"\i");
                    if ((style & MarkdownLiveStyleParser.Strikethrough) != 0) rtf.Append(@"\strike");
                    if ((style & MarkdownLiveStyleParser.Underline) != 0) rtf.Append(@"\ul");
                    rtf.Append(' ');

                    for (var k = i; k < j; k++)
                    {
                        var c = line[k];
                        if (c == '\\' || c == '{' || c == '}') rtf.Append('\\').Append(c);
                        else if (c == '\t') rtf.Append(@"\tab ");
                        else if (c < 0x20 || c > 0x7E) rtf.Append(@"\u").Append((short)c).Append('?');
                        else rtf.Append(c);
                    }

                    rtf.Append('}');
                    i = j;
                }

                if (lineIndex < lines.Length - 1) rtf.Append("\\par\r\n");
            }

            rtf.Append('}');
            return rtf.ToString();
        }

        private static void AppendRtfColor(StringBuilder rtf, Color color)
        {
            rtf.Append(@"\red").Append(color.R).Append(@"\green").Append(color.G).Append(@"\blue").Append(color.B).Append(';');
        }

        private void ResetMarkdownStyling()
        {
            var text = GetText();
            if (text.Length == 0) return;
            // After a whole-text replace the format is often plain already; then nothing needs to change
            var current = ToMarkdownStyle(Document.GetRange(0, text.Length).CharacterFormat, GetMarkdownPalette());
            if (current == 0) return;
            RunMarkdownFormatting(formatter => formatter.Apply(0, text.Length, 0, current));
        }

        private string _textAtLastTextChanged;
        private bool _isLineNumbersRenderingScheduled;

        /// <summary>
        /// The RichEditBox raises TextChanged for every format change too, after the change is done.
        /// Styling a large file makes thousands of them, so those must not redraw the line numbers each time.
        /// GetText() returns a new string only when the text changes, so comparing references is enough.
        /// </summary>
        private bool IsFormatOnlyTextChange()
        {
            var text = GetText();
            var formatOnly = ReferenceEquals(text, _textAtLastTextChanged);
            _textAtLastTextChanged = text;
            return formatOnly;
        }

        private void ScheduleLineNumbersRenderingAfterFormatChange()
        {
            if (_isLineNumbersRenderingScheduled) return;
            _isLineNumbersRenderingScheduled = true;

            // Bold text is wider, so wrapped lines can move: draw the line numbers once, after the burst
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                _isLineNumbersRenderingScheduled = false;
                UpdateLineNumbersRendering();
            });
        }

        private void RunMarkdownFormatting(Action<MarkdownFormatter> work)
        {
            // A file that was just opened has no history. Turning undo off while it is styled is faster
            // and leaves nothing for Ctrl+Z to undo. Otherwise the format changes go into one undo entry.
            var historyWasEmpty = !Document.CanUndo() && !Document.CanRedo();
            var undoLimit = Document.UndoLimit;
            var undoDisabled = historyWasEmpty && undoLimit > 0;
            if (undoDisabled) Document.UndoLimit = 0;

            var formatter = new MarkdownFormatter(this, GetMarkdownPalette(), useUndoGroup: !undoDisabled);
            Document.BatchDisplayUpdates();

            try
            {
                work(formatter);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[{nameof(TextEditorCore)}] Failed to style markdown: {ex.Message}");
            }
            finally
            {
                formatter.Finish();
                Document.ApplyDisplayUpdates();
                if (undoDisabled) Document.UndoLimit = undoLimit;
            }

        }

        /// <summary>
        /// Reads the current format of a line, one run of equal format at a time.
        /// </summary>
        private byte[] ReadMarkdownStyles(int start, int length, MarkdownPalette palette)
        {
            var actual = new byte[length];
            var end = start + length;
            var position = start;

            while (position < end)
            {
                var range = Document.GetRange(position, position + 1);
                range.EndOf(TextRangeUnit.CharacterFormat, true);
                var runEnd = Math.Min(Math.Max(range.EndPosition, position + 1), end);

                var style = ToMarkdownStyle(Document.GetRange(position, runEnd).CharacterFormat, palette);
                for (var i = position; i < runEnd; i++) actual[i - start] = style;

                position = runEnd;
            }

            return actual;
        }

        private const byte UnknownMarkdownStyle = 0xFF; // A format this code would never set, so it gets replaced

        private static byte ToMarkdownStyle(ITextCharacterFormat format, MarkdownPalette palette)
        {
            byte style = 0;

            if (format.Weight == palette.BoldWeight) style |= MarkdownLiveStyleParser.Bold;
            else if (format.Weight != palette.BaseWeight) return UnknownMarkdownStyle;

            var italic = format.Italic == FormatEffect.On;
            if (format.Italic == FormatEffect.Undefined) return UnknownMarkdownStyle;
            if (italic != palette.BaseItalic) style |= MarkdownLiveStyleParser.Italic;

            if (format.Strikethrough == FormatEffect.On) style |= MarkdownLiveStyleParser.Strikethrough;
            else if (format.Strikethrough != FormatEffect.Off) return UnknownMarkdownStyle;

            if (format.Underline == UnderlineType.Single) style |= MarkdownLiveStyleParser.Underline;
            else if (format.Underline != UnderlineType.None) return UnknownMarkdownStyle;

            var color = format.ForegroundColor;
            if (color == palette.Dim) style |= MarkdownLiveStyleParser.ColorDim;
            else if (color == palette.Link) style |= MarkdownLiveStyleParser.ColorLink;
            else if (color == palette.Code) style |= MarkdownLiveStyleParser.ColorCode;
            else if (color != palette.Base) return UnknownMarkdownStyle;

            return style;
        }

        /// <summary>
        /// Applies formats and groups all of them into one undo entry, opened only when something changes.
        /// It sets only the properties that differ from the current format, because each property set
        /// is a separate change for the RichEditBox and a large file can need thousands of them.
        /// </summary>
        private sealed class MarkdownFormatter
        {
            private readonly TextEditorCore _editor;
            private readonly bool _useUndoGroup;

            public MarkdownPalette Palette { get; }

            public bool Changed { get; private set; }

            public MarkdownFormatter(TextEditorCore editor, MarkdownPalette palette, bool useUndoGroup)
            {
                _editor = editor;
                _useUndoGroup = useUndoGroup;
                Palette = palette;
            }

            /// <summary>
            /// actual == null means every character of the line is plain right now.
            /// </summary>
            public void ApplyRuns(int lineStart, int length, byte[] desired, byte[] actual)
            {
                var i = 0;
                while (i < length)
                {
                    var want = desired[i];
                    var have = actual?[i] ?? (byte)0;
                    var j = i + 1;
                    while (j < length && desired[j] == want && (actual?[j] ?? (byte)0) == have) j++;

                    if (want != have) Apply(lineStart + i, lineStart + j, want, have);
                    i = j;
                }
            }

            public void Apply(int start, int end, byte want, byte have)
            {
                if (!Changed)
                {
                    Changed = true;
                    if (_useUndoGroup) _editor.Document.BeginUndoGroup();
                }

                var all = have == UnknownMarkdownStyle;
                var format = _editor.Document.GetRange(start, end).CharacterFormat;

                if (all || Differs(want, have, MarkdownLiveStyleParser.Bold))
                {
                    format.Weight = (want & MarkdownLiveStyleParser.Bold) != 0 ? Palette.BoldWeight : Palette.BaseWeight;
                }

                if (all || Differs(want, have, MarkdownLiveStyleParser.Italic))
                {
                    format.Italic = ((want & MarkdownLiveStyleParser.Italic) != 0) != Palette.BaseItalic ? FormatEffect.On : FormatEffect.Off;
                }

                if (all || Differs(want, have, MarkdownLiveStyleParser.Strikethrough))
                {
                    format.Strikethrough = (want & MarkdownLiveStyleParser.Strikethrough) != 0 ? FormatEffect.On : FormatEffect.Off;
                }

                if (all || Differs(want, have, MarkdownLiveStyleParser.Underline))
                {
                    format.Underline = (want & MarkdownLiveStyleParser.Underline) != 0 ? UnderlineType.Single : UnderlineType.None;
                }

                if (all || Differs(want, have, MarkdownLiveStyleParser.ColorMask))
                {
                    switch (want & MarkdownLiveStyleParser.ColorMask)
                    {
                        case MarkdownLiveStyleParser.ColorDim:
                            format.ForegroundColor = Palette.Dim;
                            break;
                        case MarkdownLiveStyleParser.ColorLink:
                            format.ForegroundColor = Palette.Link;
                            break;
                        case MarkdownLiveStyleParser.ColorCode:
                            format.ForegroundColor = Palette.Code;
                            break;
                        default:
                            format.ForegroundColor = Palette.Base;
                            break;
                    }
                }
            }

            private static bool Differs(byte want, byte have, byte mask)
            {
                return (want & mask) != (have & mask);
            }

            public void Finish()
            {
                if (Changed && _useUndoGroup) _editor.Document.EndUndoGroup();
            }
        }

        private void UndoSkippingFormatChanges()
        {
            Document.GetText(TextGetOptions.None, out var before);

            for (var step = 0; step < 1000 && Document.CanUndo(); step++)
            {
                Document.Undo();
                Document.GetText(TextGetOptions.None, out var after);
                if (after != before) break; // Undid a text change: done
            }
        }

        private void RedoSkippingFormatChanges()
        {
            Document.GetText(TextGetOptions.None, out var current);

            for (var step = 0; step < 1000 && Document.CanRedo(); step++)
            {
                Document.Redo();
                Document.GetText(TextGetOptions.None, out var after);
                if (after != current)
                {
                    current = after;
                    break;
                }
            }

            // Also redo the format entries that followed that text change, but not the next text change
            for (var step = 0; step < 1000 && Document.CanRedo(); step++)
            {
                Document.Redo();
                Document.GetText(TextGetOptions.None, out var after);
                if (after != current)
                {
                    Document.Undo();
                    break;
                }
            }
        }

        private async void OnPointerReleasedForMarkdownLinks(object sender, PointerRoutedEventArgs args)
        {
            if (!_isMarkdownStylingEnabled || _contentScrollViewer == null) return;
            if (args.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Pen) return;
            if (args.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonReleased) return;
            if (!Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down)) return;

            var point = args.GetCurrentPoint(_contentScrollViewer).Position;
            var range = Document.GetRangeFromPoint(
                new Windows.Foundation.Point(point.X + _contentScrollViewer.HorizontalOffset, point.Y + _contentScrollViewer.VerticalOffset),
                PointOptions.ClientCoordinates);

            var url = FindMarkdownLinkAt(range.StartPosition);
            if (url == null || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
            if (!MarkdownLiveStyleParser.IsOpenableLink(uri)) return;

            args.Handled = true;
            await Launcher.LaunchUriAsync(uri);
        }

        private string FindMarkdownLinkAt(int position)
        {
            var text = GetText();
            if (position < 0 || position > text.Length) return null;

            var lineStart = position == 0 ? 0 : text.LastIndexOf(RichEditBoxDefaultLineEnding, position - 1) + 1;
            var lineEnd = text.IndexOf(RichEditBoxDefaultLineEnding, position);
            if (lineEnd < 0) lineEnd = text.Length;

            var lineIndex = 0;
            for (var i = 0; i < lineStart; i++)
            {
                if (text[i] == RichEditBoxDefaultLineEnding) lineIndex++;
            }

            var contexts = _markdownStyledContexts;
            var context = contexts != null && lineIndex < contexts.Length ? contexts[lineIndex] : MarkdownLineContext.Normal;
            var line = text.Substring(lineStart, lineEnd - lineStart);

            foreach (var link in MarkdownLiveStyleParser.StyleLine(line, context, new byte[line.Length]))
            {
                var offset = position - lineStart;
                if (offset >= link.Start && offset < link.Start + link.Length) return link.Url;
            }

            return null;
        }
    }
}
