// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Views.MainPage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Notepads.Commands;
    using Notepads.Controls.Dialog;
    using Notepads.Controls.Print;
    using Notepads.Controls.TextEditor;
    using Notepads.Core;
    using Notepads.Extensions;
    using Notepads.Services;
    using Notepads.Settings;
    using Notepads.Utilities;
    using Notepads.Views.Settings;
    using Windows.ApplicationModel.Activation;
    using Windows.ApplicationModel.DataTransfer;
    using Windows.ApplicationModel.Resources;
    using Windows.Storage;
    using Windows.System;
    using Windows.UI.ViewManagement;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Media.Animation;
    using Windows.UI.Xaml.Navigation;
    using Windows.Graphics.Printing;

    public sealed partial class NotepadsMainPage : Page
    {
        private IReadOnlyList<IStorageItem> _appLaunchFiles;

        private string _appLaunchCmdDir;
        private string _appLaunchCmdArgs;
        private Uri _appLaunchUri;

        private readonly ResourceLoader _resourceLoader = ResourceLoader.GetForCurrentView();

        private bool _loaded = false;
        private bool _lastTabMovedToAnotherInstance = false;
        private bool _editingLockedForUpdate = false;

        private INotepadsCore _notepadsCore;

        private INotepadsCore NotepadsCore
        {
            get
            {
                if (_notepadsCore != null) return _notepadsCore;

                _notepadsCore = new NotepadsCore(Sets, new NotepadsExtensionProvider(), Dispatcher);
                _notepadsCore.StorageItemsDropped += OnStorageItemsDropped;
                _notepadsCore.TextEditorLoaded += OnTextEditorLoaded;
                _notepadsCore.TextEditorUnloaded += OnTextEditorUnloaded;
                _notepadsCore.TextEditorKeyDown += OnTextEditorKeyDown;
                _notepadsCore.TextEditorClosing += OnTextEditorClosing;
                _notepadsCore.TextEditorSaved += OnTextEditorSaved;
                _notepadsCore.TextEditorMovedToAnotherAppInstance += OnTextEditorMovedToAnotherAppInstance;
                _notepadsCore.TextEditorRenamed += (sender, editor) => { if (NotepadsCore.GetSelectedTextEditor() == editor) SetupStatusBar(editor); };
                _notepadsCore.TextEditorSelectionChanged += (sender, editor) => { if (NotepadsCore.GetSelectedTextEditor() == editor) { UpdateLineColumnIndicator(editor); ScheduleWordCountIndicatorUpdate(); } };
                _notepadsCore.TextEditorTextChanging += (sender, editor) => { if (NotepadsCore.GetSelectedTextEditor() == editor) ScheduleWordCountIndicatorUpdate(); };
                _notepadsCore.TextEditorFontZoomFactorChanged += (sender, editor) => { if (NotepadsCore.GetSelectedTextEditor() == editor) UpdateFontZoomIndicator(editor); };
                _notepadsCore.TextEditorEncodingChanged += (sender, editor) => { if (NotepadsCore.GetSelectedTextEditor() == editor) UpdateEncodingIndicator(editor.GetEncoding()); };
                _notepadsCore.TextEditorLineEndingChanged += (sender, editor) => { if (NotepadsCore.GetSelectedTextEditor() == editor) { UpdateLineEndingIndicator(editor.GetLineEnding()); UpdateLineColumnIndicator(editor); } };
                _notepadsCore.TextEditorEditorModificationStateChanged += (sender, editor) => { if (NotepadsCore.GetSelectedTextEditor() == editor) SetupStatusBar(editor); };
                _notepadsCore.TextEditorFileModificationStateChanged += (sender, editor) => { if (NotepadsCore.GetSelectedTextEditor() == editor) OnTextEditorFileModificationStateChanged(editor); };

                return _notepadsCore;
            }
        }

        private ICommandHandler<KeyRoutedEventArgs> _keyboardCommandHandler;

        private const string XBoxGameBarSessionFilePrefix = "XBoxGameBar-";

        private ISessionManager _sessionManager;

        private ISessionManager SessionManager => _sessionManager ?? (_sessionManager = SessionUtility.GetSessionManager(NotepadsCore, App.IsGameBarWidget ? XBoxGameBarSessionFilePrefix : null));

        private readonly string _defaultNewFileName;

        public NotepadsMainPage()
        {
            InitializeComponent();

            _defaultNewFileName = _resourceLoader.GetString("TextEditor_DefaultNewFileName");

            // Set custom title bar dragging area
            Window.Current.SetTitleBar(AppTitleBar);

            InitializeNotificationCenter();
            InitializeThemeSettings();
            InitializeStatusBar();
            InitializeControls();
            InitializeMainMenu();
            InitializeWorkspace();
            InitializeKeyboardShortcuts();

            // Session backup and restore toggle
            AppSettingsService.OnSessionBackupAndRestoreOptionChanged += OnSessionBackupAndRestoreOptionChanged;

            // Register for printing
            if (PrintManager.IsSupported())
            {
                PrintArgs.RegisterForPrinting(this);
            }

            // Register for content Sharing
            Windows.ApplicationModel.DataTransfer.DataTransferManager.GetForCurrentView().DataRequested += MainPage_DataRequested;
            Windows.UI.Core.Preview.SystemNavigationManagerPreview.GetForCurrentView().CloseRequested += MainPage_CloseRequested;

            if (App.IsGameBarWidget)
            {
                TitleBarReservedArea.Width = .0f;
            }
            else
            {
                Window.Current.SizeChanged += WindowSizeChanged;
                Window.Current.VisibilityChanged += WindowVisibilityChangedEventHandler;
            }
        }

        private void InitializeControls()
        {
            ToolTipService.SetToolTip(ExitCompactOverlayButton, _resourceLoader.GetString("App_ExitCompactOverlayMode_Text"));
            RootSplitView.PaneOpening += delegate { SettingsFrame.Navigate(typeof(SettingsPage), null, new SuppressNavigationTransitionInfo()); };
            RootSplitView.PaneClosed += delegate { NotepadsCore.FocusOnSelectedTextEditor(); };
            NewSetButton.Click += delegate { NotepadsCore.OpenNewTextEditor(_defaultNewFileName); };
        }

        private void InitializeKeyboardShortcuts()
        {
            _keyboardCommandHandler = new KeyboardCommandHandler(new List<IKeyboardCommand<KeyRoutedEventArgs>>()
            {
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.W, (args) => NotepadsCore.CloseTextEditor(NotepadsCore.GetSelectedTextEditor())),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.Tab, (args) => NotepadsCore.SwitchTo(true)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, true, VirtualKey.Tab, (args) => NotepadsCore.SwitchTo(false)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.N, (args) => NotepadsCore.OpenNewTextEditor(_defaultNewFileName)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.T, (args) => NotepadsCore.OpenNewTextEditor(_defaultNewFileName)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.O, async (args) => await OpenNewFilesAsync()),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.S, async (args) => await SaveAsync(NotepadsCore.GetSelectedTextEditor(), saveAs: false, ignoreUnmodifiedDocument: true)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, true, VirtualKey.S, async (args) => await SaveAsync(NotepadsCore.GetSelectedTextEditor(), saveAs: true)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.P, async (args) => await PrintAsync(NotepadsCore.GetSelectedTextEditor())),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, true, VirtualKey.P, async (args) => await PrintAllAsync(NotepadsCore.GetAllTextEditors())),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, true, VirtualKey.R, (args) => ReloadFileFromDiskAsync(this, new RoutedEventArgs())),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, true, VirtualKey.N, async (args) => await OpenNewAppInstanceAsync()),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.Number1, (args) => NotepadsCore.SwitchTo(0)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.Number2, (args) => NotepadsCore.SwitchTo(1)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.Number3, (args) => NotepadsCore.SwitchTo(2)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.Number4, (args) => NotepadsCore.SwitchTo(3)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.Number5, (args) => NotepadsCore.SwitchTo(4)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.Number6, (args) => NotepadsCore.SwitchTo(5)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.Number7, (args) => NotepadsCore.SwitchTo(6)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.Number8, (args) => NotepadsCore.SwitchTo(7)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.Number9, (args) => NotepadsCore.SwitchTo(8)),
                new KeyboardCommand<KeyRoutedEventArgs>(false, true, false, VirtualKey.F, (args) => ShowMainMenu()),
                new KeyboardCommand<KeyRoutedEventArgs>(false, true, false, VirtualKey.O, (args) => ShowOutline()),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, true, VirtualKey.O, (args) => ShowWorkspaceFileList()),
                new KeyboardCommand<KeyRoutedEventArgs>(false, true, false, VirtualKey.W, (args) => AppSettingsService.EditorTextWidth = TextWidthUtility.Next(AppSettingsService.EditorTextWidth)),
                new KeyboardCommand<KeyRoutedEventArgs>(VirtualKey.F11, (args) => EnterExitFullScreenMode()),
                new KeyboardCommand<KeyRoutedEventArgs>(VirtualKey.F12, (args) => EnterExitCompactOverlayMode()),
                new KeyboardCommand<KeyRoutedEventArgs>(VirtualKey.Escape, (args) => { if (RootSplitView.IsPaneOpen) RootSplitView.IsPaneOpen = false; }),
                new KeyboardCommand<KeyRoutedEventArgs>(VirtualKey.F1, (args) => { if (App.IsPrimaryInstance && !App.IsGameBarWidget) RootSplitView.IsPaneOpen = !RootSplitView.IsPaneOpen; }),
                new KeyboardCommand<KeyRoutedEventArgs>(VirtualKey.F2, async (args) => await RenameFileAsync(NotepadsCore.GetSelectedTextEditor())),
                new KeyboardCommand<KeyRoutedEventArgs>(true, true, true, VirtualKey.L, async (args) => { LoggingService.Flush(); await OpenFileAsync(LoggingService.GetLogFile(), rebuildOpenRecentItems: false); })
            });
        }

        private static async Task OpenNewAppInstanceAsync()
        {
            if (!await NotepadsProtocolService.LaunchProtocolAsync(NotepadsOperationProtocol.OpenNewInstance))
            {
            }
        }

        #region Application Life Cycle & Window management

        // Handles external links or cmd args activation before Sets loaded
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            switch (e.Parameter)
            {
                case null:
                    return;
                case FileActivatedEventArgs fileActivatedEventArgs:
                    _appLaunchFiles = fileActivatedEventArgs.Files;
                    break;
                case CommandLineActivatedEventArgs commandLineActivatedEventArgs:
                    _appLaunchCmdDir = commandLineActivatedEventArgs.Operation.CurrentDirectoryPath;
                    _appLaunchCmdArgs = commandLineActivatedEventArgs.Operation.Arguments;
                    break;
                case ProtocolActivatedEventArgs protocol:
                    _appLaunchUri = protocol.Uri;
                    break;
            }
        }

        // App should wait for Sets fully loaded before opening files requested by user (by click or from cmd)
        // Open files from external links or cmd args on Sets Loaded
        private async void Sets_Loaded(object sender, RoutedEventArgs e)
        {
            int loadedCount = 0;

            if (!_loaded && AppSettingsService.IsSessionSnapshotEnabled)
            {
                loadedCount = await LoadLastSessionAsync();
            }

            if (!_loaded)
            {
                // The session already reopens the folder's files, so only fall back to the folder's own list
                loadedCount += await ResumeWorkspaceAsync(restoreTabs: loadedCount == 0);
            }

            if (_appLaunchFiles != null && _appLaunchFiles.Count > 0)
            {
                loadedCount += await OpenFilesAsync(_appLaunchFiles);
                _appLaunchFiles = null;
            }
            else if (_appLaunchCmdDir != null)
            {
                var file = await FileSystemUtility.OpenFileFromCommandLineAsync(_appLaunchCmdDir, _appLaunchCmdArgs);
                if (file != null && await OpenFileAsync(file))
                {
                    loadedCount++;
                }
                _appLaunchCmdDir = null;
                _appLaunchCmdArgs = null;
            }
            else if (_appLaunchUri != null)
            {
                var operation = NotepadsProtocolService.GetOperationProtocol(_appLaunchUri, out var context);
                if (operation == NotepadsOperationProtocol.OpenNewInstance || operation == NotepadsOperationProtocol.Unrecognized)
                {
                    // Do nothing
                }
                _appLaunchUri = null;
            }

            if (!_loaded)
            {
                if (loadedCount == 0)
                {
                    NotepadsCore.OpenNewTextEditor(_defaultNewFileName);
                }
                _loaded = true;
            }

            if (AppSettingsService.IsSessionSnapshotEnabled)
            {
                SessionManager.IsBackupEnabled = true;
                SessionManager.StartSessionBackup();
            }

            await BuildOpenRecentButtonSubItemsAsync();

            UpdateService.SaveBeforeInstallAsync = SaveBeforeUpdateInstallAsync;
            UpdateService.InstallStopped = () => SetEditingLockedForUpdate(false);

            if (!App.IsGameBarWidget)
            {
                // An issue with the Game Bar extension model and Windows platform prevents the Notepads process from exiting cleanly
                // when more than one CoreWindow has been created, and NotepadsMainPage is the last to close. The common case for this
                // is to open Notepads in Game Bar, then open its settings, then close the settings and finally close Notepads.
                // This puts the process in a bad state where it will no longer open in Game Bar and the Notepads process is orphaned.
                // To work around this do not use the EnteredBackground event when running as a widget.
                // Microsoft is tracking this issue as VSO#25735260
                Application.Current.EnteredBackground -= App_EnteredBackground;
                Application.Current.EnteredBackground += App_EnteredBackground;

                Window.Current.CoreWindow.Activated -= CoreWindow_Activated;
                Window.Current.CoreWindow.Activated += CoreWindow_Activated;
            }
        }

        // Restores the tabs of the last session, and says which ones could not come back. Returns how many came back.
        private async Task<int> LoadLastSessionAsync()
        {
            int loadedCount = 0;

            try
            {
                loadedCount = await SessionManager.LoadLastSessionAsync();
            }
            catch (SessionDataCorruptedException ex)
            {
                LoggingService.LogError($"[{nameof(NotepadsMainPage)}] Failed to load last session: {ex}");

                // Last session data is corrupted, clear it first
                await SessionManager.ClearSessionDataAsync();

                // Recover backup files
                int numberOfRecoveredFiles = await SessionManager.RecoverBackupFilesAsync();

                LoggingService.LogInfo($"[{nameof(NotepadsMainPage)}] {numberOfRecoveredFiles} file(s) recovered from last session backup.");

                // Show session recovery dialog if there are any recovered files
                if (numberOfRecoveredFiles > 0)
                {
                    var sessionCorruptionErrorDialog = new SessionCorruptionErrorDialog(
                        recoveryAction: async () =>
                        {
                            await SessionManager.OpenSessionBackupFolderAsync();
                        });
                    await DialogManager.OpenDialogAsync(sessionCorruptionErrorDialog, awaitPreviousDialog: false);
                }
            }
            catch (Exception ex) // Catch all other exceptions
            {
                LoggingService.LogError($"[{nameof(NotepadsMainPage)}] Failed to load last session: {ex}");
            }

            if (SessionManager.TabsNotRestored.Count > 0)
            {
                var tabsNotRestoredDialog = new SessionCorruptionErrorDialog(
                    recoveryAction: async () => { await SessionManager.OpenSessionBackupFolderAsync(); },
                    content: string.Format(_resourceLoader.GetString("SessionCorruptionErrorDialog_TabsNotRestored"),
                        string.Join(", ", SessionManager.TabsNotRestored)));
                await DialogManager.OpenDialogAsync(tabsNotRestoredDialog, awaitPreviousDialog: false);
            }

            return loadedCount;
        }

        private bool _becomingPrimaryInstance;

        /// <summary>
        /// Called when this window is activated. When the main window has closed, this window takes over its
        /// session backup, Settings and folder memory. Before its first backup, it restores the last session:
        /// the closed window's unsaved tabs are only in that session, and the first save here would delete their backups.
        /// </summary>
        public async Task TryBecomePrimaryInstanceAsync()
        {
            if (!_loaded || _becomingPrimaryInstance || App.IsPrimaryInstance || App.IsGameBarWidget) return;
            if (!App.TryBecomePrimaryInstance()) return;

            _becomingPrimaryInstance = true;

            try
            {
                AppSettingsService.ReloadSessionSnapshotSetting();
                Workspace.StartRememberingFolder();
                UpdateShadowWindowIndicator();
                UpdateMainMenuForPrimaryInstance();

                if (!AppSettingsService.IsSessionSnapshotEnabled) return;

                var selectedEditor = NotepadsCore.GetSelectedTextEditor();
                var editorsBefore = NotepadsCore.GetAllTextEditors();

                int restoredCount = await LoadLastSessionAsync();

                if (restoredCount > 0)
                {
                    CloseRestoredCopiesOfOpenFiles(editorsBefore);

                    if (selectedEditor != null && NotepadsCore.GetAllTextEditors().Contains(selectedEditor))
                    {
                        NotepadsCore.SwitchTo(selectedEditor); // Stay on the tab the user was in
                    }

                    LoggingService.LogInfo($"[{nameof(NotepadsMainPage)}] {restoredCount} tab(s) of the closed main window restored here.");
                }

                SessionManager.IsBackupEnabled = true;
                SessionManager.StartSessionBackup(startImmediately: true);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[{nameof(NotepadsMainPage)}] Failed to take over as the main window: {ex}");
            }
            finally
            {
                _becomingPrimaryInstance = false;
            }
        }

        // Both windows can have one file open. A restored tab without unsaved changes is then only a second copy.
        private void CloseRestoredCopiesOfOpenFiles(ITextEditor[] editorsBefore)
        {
            var openPaths = new HashSet<string>(
                editorsBefore.Where(editor => editor.EditingFilePath != null).Select(editor => editor.EditingFilePath),
                StringComparer.OrdinalIgnoreCase);

            foreach (var editor in NotepadsCore.GetAllTextEditors())
            {
                if (editorsBefore.Contains(editor) || editor.IsModified || editor.EditingFilePath == null) continue;
                if (openPaths.Contains(editor.EditingFilePath))
                {
                    NotepadsCore.DeleteTextEditor(editor);
                }
            }
        }

        private async void App_EnteredBackground(object sender, Windows.ApplicationModel.EnteredBackgroundEventArgs e)
        {
            var deferral = e.GetDeferral();

            // The session snapshot keeps unsaved text, so it goes first; the workspace write can be slow on a network folder
            if (AppSettingsService.IsSessionSnapshotEnabled)
            {
                await SessionManager.SaveSessionAsync();
            }

            await Workspace.SaveAsync();

            deferral.Complete();
        }

        // An update install ends the app without a close request, so save what closing the window saves.
        // Returns false when unsaved text would be lost: a tab has unsaved changes and the session does not hold them.
        private async Task<bool> SaveBeforeUpdateInstallAsync()
        {
            // The install downloads first and then closes the app with no warning. Text typed in between
            // would be in neither the session nor the file, so the editors take no typing until then.
            SetEditingLockedForUpdate(true);
            bool keepLocked = false;

            try
            {
                await RefreshFileStatusOfUnmodifiedTabsAsync(NotepadsCore.GetAllTextEditors());

                bool sessionSaved = AppSettingsService.IsSessionSnapshotEnabled && await SessionManager.SaveSessionAsync();

                if (!sessionSaved && NotepadsCore.HaveUnsavedTextEditor())
                {
                    return false;
                }

                await Workspace.SaveAsync();
                keepLocked = true; // InstallStopped unlocks if the app is still running after the install
                return true;
            }
            finally
            {
                // Also on a throw: the install never starts then, so InstallStopped would never run
                if (!keepLocked) SetEditingLockedForUpdate(false);
            }
        }

        private void SetEditingLockedForUpdate(bool locked)
        {
            _editingLockedForUpdate = locked;
            foreach (ITextEditor textEditor in NotepadsCore.GetAllTextEditors())
            {
                textEditor.SetEditingLocked(locked);
            }
        }

        public void ExecuteProtocol(Uri uri)
        {
            LoggingService.LogInfo($"[{nameof(NotepadsMainPage)}] Executing protocol: {uri}", consoleOnly: true);
        }

        private void CoreWindow_Activated(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == Windows.UI.Core.CoreWindowActivationState.Deactivated)
            {
                LoggingService.LogInfo($"[{nameof(NotepadsMainPage)}] CoreWindow Deactivated.", consoleOnly: true);
                NotepadsCore.GetSelectedTextEditor()?.StopCheckingFileStatus();
                if (AppSettingsService.IsSessionSnapshotEnabled)
                {
                    SessionManager.StopSessionBackup();
                }
            }
            else if (args.WindowActivationState == Windows.UI.Core.CoreWindowActivationState.PointerActivated ||
                     args.WindowActivationState == Windows.UI.Core.CoreWindowActivationState.CodeActivated)
            {
                LoggingService.LogInfo($"[{nameof(NotepadsMainPage)}] CoreWindow Activated.", consoleOnly: true);
                Task.Run(() => ApplicationSettingsStore.Write(SettingsKey.ActiveInstanceIdStr, App.InstanceId.ToString()));

                // Another window may have changed settings since this one was last in front
                AppSettingsService.ReloadFromStore();
                ThemeSettingsService.ReloadFromStore();

                // The main window may have closed. TryBecomePrimaryInstanceAsync starts the backup itself, after the session restore.
                if (!App.IsPrimaryInstance)
                {
                    _ = TryBecomePrimaryInstanceAsync();
                }

                NotepadsCore.GetSelectedTextEditor()?.StartCheckingFileStatusPeriodically();
                if (AppSettingsService.IsSessionSnapshotEnabled && !_becomingPrimaryInstance)
                {
                    SessionManager.StartSessionBackup();
                }
            }
        }

        private void WindowVisibilityChangedEventHandler(System.Object sender, Windows.UI.Core.VisibilityChangedEventArgs e)
        {
            LoggingService.LogInfo($"[{nameof(NotepadsMainPage)}] Window Visibility Changed, Visible = {e.Visible}.", consoleOnly: true);
            // Perform operations that should take place when the application becomes visible rather than
            // when it is prelaunched, such as building a what's new feed
        }

        // Content sharing
        private void MainPage_DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            var textEditor = NotepadsCore.GetSelectedTextEditor();
            if (textEditor == null) return;

            if (NotepadsCore.TryGetSharingContent(textEditor, out var title, out var content))
            {
                args.Request.Data.Properties.Title = title;
                args.Request.Data.SetText(content);
            }
            else
            {
                args.Request.FailWithDisplayText(_resourceLoader.GetString("ContentSharing_FailureDisplayText"));
            }
        }

        private async void MainPage_CloseRequested(object sender, Windows.UI.Core.Preview.SystemNavigationCloseRequestedPreviewEventArgs e)
        {
            var deferral = e.GetDeferral();

            await RefreshFileStatusOfUnmodifiedTabsAsync(NotepadsCore.GetAllTextEditors());

            // Save session before app exit, then the workspace, which can be slow on a network folder.
            // If the session cannot be saved, ask about unsaved files as if session backup were off.
            if (AppSettingsService.IsSessionSnapshotEnabled &&
                await SessionManager.SaveSessionAsync(() => { SessionManager.IsBackupEnabled = false; }))
            {
                await Workspace.SaveAsync();
                App.InstanceHandlerMutex?.Dispose();
                deferral.Complete();
                return;
            }

            await Workspace.SaveAsync();

            if (!NotepadsCore.HaveUnsavedTextEditor())
            {
                App.InstanceHandlerMutex?.Dispose();
                deferral.Complete();
                return;
            }

            HideAllOpenFlyouts();

            var appCloseSaveReminderDialog = new AppCloseSaveReminderDialog(
                async () =>
                {
                    var count = NotepadsCore.GetNumberOfOpenedTextEditors();

                    foreach (var textEditor in NotepadsCore.GetAllTextEditors())
                    {
                        if (await SaveAsync(textEditor, saveAs: false, ignoreUnmodifiedDocument: true, rebuildOpenRecentItems: false))
                        {
                            NotepadsCore.DeleteTextEditor(textEditor);
                            count--;
                        }
                    }

                    // Prevent app from closing if there is any tab still opens
                    if (count > 0)
                    {
                        e.Handled = true;
                        await BuildOpenRecentButtonSubItemsAsync();
                    }
                    else
                    {
                        App.InstanceHandlerMutex?.Dispose();
                    }

                    deferral.Complete();
                },
                discardAndExitAction: () =>
                {
                    App.InstanceHandlerMutex?.Dispose();
                    deferral.Complete();
                },
                cancelAction: () =>
                {
                    e.Handled = true;
                    deferral.Complete();
                });

            var result = await DialogManager.OpenDialogAsync(appCloseSaveReminderDialog, awaitPreviousDialog: false);

            if (result == null)
            {
                e.Handled = true;
                deferral.Complete();
            }

            if (e.Handled && !appCloseSaveReminderDialog.IsAborted)
            {
                NotepadsCore.FocusOnSelectedTextEditor();
            }
        }

        // Only the selected tab checks its file on a timer. Before tabs close without asking, check whether
        // a file was deleted outside Quillpad: such a tab then counts as modified, so its text is asked about or backed up.
        private static async Task RefreshFileStatusOfUnmodifiedTabsAsync(IEnumerable<ITextEditor> textEditors)
        {
            await Task.WhenAll(textEditors
                .Where(textEditor => !textEditor.IsModified && textEditor.EditingFile != null)
                .Select(textEditor => textEditor.RefreshFileStatusAsync()));
        }

        private void HideAllOpenFlyouts()
        {
            // Hide TextEditor ContextFlyout if it is showing
            // Why we need to do this? Take a look here: https://github.com/microsoft/microsoft-ui-xaml/issues/2461
            var editorFlyout = NotepadsCore.GetSelectedTextEditor()?.GetContextFlyout();
            if (editorFlyout != null && editorFlyout.IsOpen)
            {
                editorFlyout.Hide();
            }
        }

        private async void OnSessionBackupAndRestoreOptionChanged(object sender, bool isSessionBackupAndRestoreEnabled)
        {
            await Dispatcher.CallOnUIThreadAsync(async () =>
            {
                if (isSessionBackupAndRestoreEnabled)
                {
                    SessionManager.IsBackupEnabled = true;
                    SessionManager.StartSessionBackup(startImmediately: true);
                }
                else
                {
                    SessionManager.IsBackupEnabled = false;
                    SessionManager.StopSessionBackup();
                    await SessionManager.ClearSessionDataAsync();
                }
            });
        }

        private void UpdateApplicationTitle(ITextEditor activeTextEditor)
        {
            if (!App.IsGameBarWidget)
            {
                var title = activeTextEditor.EditingFileName ?? activeTextEditor.FileNamePlaceholder;
                var folder = Workspace.Folder;
                ApplicationView.GetForCurrentView().Title = folder == null ? title : $"{title} - {folder.Name}";
            }
        }

        #endregion

        #region NotepadsCore Events

        private void OnTextEditorLoaded(object sender, ITextEditor textEditor)
        {
            if (_editingLockedForUpdate)
            {
                textEditor.SetEditingLocked(true); // A tab opened during the update download
            }

            if (NotepadsCore.GetSelectedTextEditor() == textEditor)
            {
                SetupStatusBar(textEditor);
                NotepadsCore.FocusOnSelectedTextEditor();
            }

            _ = LoadWordGoalAsync(textEditor);
        }

        private async void OnTextEditorUnloaded(object sender, ITextEditor textEditor)
        {
            if (NotepadsCore.GetNumberOfOpenedTextEditors() == 0)
            {
                bool exiting = _lastTabMovedToAnotherInstance || AppSettingsService.ExitWhenLastTabClosed;

                if (AppSettingsService.IsSessionSnapshotEnabled)
                {
                    // Stop backups only when the app is about to exit. Otherwise a new tab opens below,
                    // and its text has to keep going into the session.
                    if (exiting)
                    {
                        await SessionManager.SaveSessionAsync(() => { SessionManager.IsBackupEnabled = false; });
                    }
                    else
                    {
                        await SessionManager.SaveSessionAsync();
                    }
                }

                if (exiting)
                {
                    if (!await ApplicationView.GetForCurrentView().TryConsolidateAsync())
                    {
                    }

                    Application.Current.Exit();
                }
                else
                {
                    NotepadsCore.OpenNewTextEditor(_defaultNewFileName);
                }
            }
        }

        private async void OnTextEditorFileModificationStateChanged(ITextEditor textEditor)
        {
            // Another app changed the file. With no unsaved edits here, nothing can be lost, so take the new text.
            if (textEditor.FileModificationState == FileModificationState.Modified &&
                !textEditor.IsModified &&
                await TryReloadChangedFileAsync(textEditor))
            {
                return;
            }

            if (textEditor.FileModificationState == FileModificationState.Modified)
            {
                NotificationCenter.Instance.PostNotification(_resourceLoader.GetString("TextEditor_FileModifiedOutsideIndicator_ToolTip"), 3500);
            }
            else if (textEditor.FileModificationState == FileModificationState.RenamedMovedOrDeleted)
            {
                NotificationCenter.Instance.PostNotification(_resourceLoader.GetString("TextEditor_FileRenamedMovedOrDeletedIndicator_ToolTip"), 3500);
            }
            UpdateFileModificationStateIndicator(textEditor);
            UpdatePathIndicator(textEditor);
        }

        private async Task<bool> TryReloadChangedFileAsync(ITextEditor textEditor)
        {
            try
            {
                // The other app may still be writing. A reload now would take part of the file, and the next save would write that part back.
                if (!await WaitUntilFileStopsChangingAsync(textEditor.EditingFile)) return false;

                // Text typed during the wait is not in the file: keep it, and show the warning instead
                if (textEditor.IsModified || !NotepadsCore.GetAllTextEditors().Contains(textEditor)) return false;

                if (!await textEditor.ReloadFromEditingFileAsync(requireUnchangedFile: true)) return false;

                NotificationCenter.Instance.PostNotification(_resourceLoader.GetString("TextEditor_NotificationMsg_FileReloaded"), 1500);
                return true;
            }
            catch (Exception ex)
            {
                // Fall back to the "modified outside" warning, so the user can still reload by hand
                LoggingService.LogError($"[{nameof(NotepadsMainPage)}] Failed to reload a file changed outside the app: {ex.Message}");
                return false;
            }
        }

        private static readonly TimeSpan FileSettleCheckDelay = TimeSpan.FromMilliseconds(500);
        private const int FileSettleCheckAttempts = 20; // Up to 10 s, then the "changed outside" warning shows instead

        // True once the file's time and size stay the same between two looks
        private static async Task<bool> WaitUntilFileStopsChangingAsync(StorageFile file)
        {
            if (file == null) return false;

            var before = await file.GetBasicPropertiesAsync();

            for (int attempt = 0; attempt < FileSettleCheckAttempts; attempt++)
            {
                await Task.Delay(FileSettleCheckDelay);
                var after = await file.GetBasicPropertiesAsync();

                if (DiskChangeUtility.HasStoppedChanging(before.DateModified.ToFileTime(), before.Size, after.DateModified.ToFileTime(), after.Size))
                {
                    return true;
                }

                before = after;
            }

            LoggingService.LogInfo($"[{nameof(NotepadsMainPage)}] The file kept changing, so it was not reloaded: {file.Path}");
            return false;
        }

        private void OnTextEditorSaved(object sender, ITextEditor textEditor)
        {
            if (NotepadsCore.GetSelectedTextEditor() == textEditor)
            {
                SetupStatusBar(textEditor);
            }
            NotificationCenter.Instance.PostNotification(_resourceLoader.GetString("TextEditor_NotificationMsg_FileSaved"), 1500);
        }

        private void OnTextEditorMovedToAnotherAppInstance(object sender, ITextEditor textEditor)
        {
            if (NotepadsCore.GetNumberOfOpenedTextEditors() == 1)
            {
                _lastTabMovedToAnotherInstance = true;
            }
            NotepadsCore.DeleteTextEditor(textEditor);
        }

        private async void OnTextEditorClosing(object sender, ITextEditor textEditor)
        {
            if (!textEditor.IsModified && textEditor.EditingFile != null)
            {
                // A file deleted outside Quillpad leaves this tab with the only copy of the text. Then the tab
                // counts as modified, and closing it asks first.
                await textEditor.RefreshFileStatusAsync();
                if (!NotepadsCore.GetAllTextEditors().Contains(textEditor)) return;
            }

            if (!AppSettingsService.ExitWhenLastTabClosed &&
                NotepadsCore.GetNumberOfOpenedTextEditors() == 1 &&
                textEditor.IsModified == false &&
                textEditor.EditingFile == null)
            {
                // Do nothing if user doesn't want closing window when last tab closed
                // And if user is trying to close the last tab and the last tab is a new empty document
            }
            else if (!textEditor.IsModified)
            {
                NotepadsCore.DeleteTextEditor(textEditor);
            }
            else // Remind user to save uncommitted changes
            {
                var file = textEditor.EditingFilePath ?? textEditor.FileNamePlaceholder;

                var setCloseSaveReminderDialog = new SetCloseSaveReminderDialog(file,
                    saveAction: async () =>
                    {
                        if (NotepadsCore.GetAllTextEditors().Contains(textEditor) && await SaveAsync(textEditor, saveAs: false))
                        {
                            NotepadsCore.DeleteTextEditor(textEditor);
                        }
                    },
                    skipSavingAction: () =>
                    {
                        if (NotepadsCore.GetAllTextEditors().Contains(textEditor))
                        {
                            NotepadsCore.DeleteTextEditor(textEditor);
                        }
                    });

                setCloseSaveReminderDialog.Opened += (s, a) =>
                {
                    if (NotepadsCore.GetAllTextEditors().Contains(textEditor))
                    {
                        NotepadsCore.SwitchTo(textEditor);
                    }
                };

                await DialogManager.OpenDialogAsync(setCloseSaveReminderDialog, awaitPreviousDialog: true);

                if (!setCloseSaveReminderDialog.IsAborted)
                {
                    NotepadsCore.FocusOnSelectedTextEditor();
                }
            }
        }

        private void OnTextEditorKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!(sender is ITextEditor textEditor)) return;
            // ignoring key events coming from inactive text editors
            if (NotepadsCore.GetSelectedTextEditor() != textEditor) return;
            var result = _keyboardCommandHandler.Handle(e);
            if (result.ShouldHandle)
            {
                e.Handled = true;
            }
        }

        private async void OnStorageItemsDropped(object sender, IReadOnlyList<IStorageItem> storageItems)
        {
            foreach (var storageItem in storageItems)
            {
                if (storageItem is StorageFile file)
                {
                    await OpenFileAsync(file);
                }
            }
        }

        #endregion
    }
}