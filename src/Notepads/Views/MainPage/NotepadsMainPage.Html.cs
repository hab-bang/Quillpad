// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Views.MainPage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Windows.Storage;
    using Windows.Storage.Pickers;
    using Notepads.Controls.TextEditor;
    using Notepads.Services;
    using Notepads.Utilities;

    /// <summary>
    /// "Export as HTML": the chapter as an HTML file, made by the same converter as "Copy as HTML".
    /// </summary>
    public sealed partial class NotepadsMainPage
    {
        private async Task ExportAsHtmlAsync(ITextEditor textEditor)
        {
            if (textEditor == null) return;

            var name = Path.GetFileNameWithoutExtension(textEditor.EditingFileName ?? textEditor.FileNamePlaceholder);
            StorageFile file = null;

            try
            {
                var picker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    SuggestedFileName = name,
                };
                picker.FileTypeChoices.Add(_resourceLoader.GetString("ExportAsHtml_FileTypeName"), new List<string> { ".html" });
                file = await picker.PickSaveFileAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[{nameof(NotepadsMainPage)}] Failed to pick a file for the HTML export: {ex.Message}");
            }

            if (file == null)
            {
                NotepadsCore.FocusOnSelectedTextEditor();
                return;
            }

            try
            {
                var html = Ao3HtmlConverter.ToHtmlDocument(textEditor.GetText(), name).Replace("\n", "\r\n");
                await FileSystemUtility.WriteTextToFileAsync(file, html, new UTF8Encoding(false));
                NotificationCenter.Instance.PostNotification(_resourceLoader.GetString("TextEditor_NotificationMsg_HtmlExported"), 1500);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[{nameof(NotepadsMainPage)}] Failed to export as HTML: {ex.Message}");
                NotificationCenter.Instance.PostNotification(
                    string.Format(_resourceLoader.GetString("TextEditor_NotificationMsg_HtmlExportFailed"), ex.Message), 3500);
            }

            NotepadsCore.FocusOnSelectedTextEditor();
        }
    }
}
