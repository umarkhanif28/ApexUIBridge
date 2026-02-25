using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using ApexUIBridge.Core;

using Debug = System.Diagnostics.Debug;

namespace ApexUIBridge.ViewModels;

/// <summary>
/// View model for the startup / process-selection phase.
///
/// <para>
/// Maintains the list of top-level desktop windows (<see cref="FilteredProcesses"/>),
/// handles text/PID/elementId filtering, and provides two window-selection flows:
/// </para>
/// <list type="number">
///   <item><b>Grid selection</b> — user picks a row from the bound
///         <see cref="System.Windows.Forms.DataGridView"/>.</item>
///   <item><b>Pick mode</b> — <see cref="PickProcessAsync"/> installs a low-level
///         Win32 mouse hook (<c>WH_MOUSE_LL</c>), switches the cursor to a
///         cross-hair, draws a <see cref="Core.ElementOverlay"/> around the
///         window under the pointer, and captures the <c>WM_LBUTTONUP</c> HWND
///         to identify the target process.</item>
/// </list>
/// <para>
/// Background scanning runs on a dedicated STA thread (required by UIA3) via
/// <see cref="SyncProcessesAsync"/>, which diffs the discovered set against the
/// current <see cref="_processes"/> collection to minimise UI churn.
/// </para>
/// </summary>
public class StartupViewModel : ObservableObject {
    private const int WhMouseLl = 14;
    private const uint GaRoot = 2;

    private static IntPtr _mouseHook = IntPtr.Zero;
    private static LowLevelMouseProc? _mouseProc;
    private static readonly ElementIdGenerator _idGenerator = new();

    private readonly AutomationBase _defaultAutomation = new UIA3Automation();
    private ObservableCollection<ProcessWindowInfo> _processes = [];
    private ElementOverlay? _topWindowOverlay;
    private AutomationElement? _topWindowUnderCursor;

    public IEnumerable<ProcessWindowInfo> FilteredProcesses => _processes.Where(FilterProcesses);
    public IEnumerable<ProcessWindowInfo> Processes => _processes;

    public bool IsBusy {
        get => GetProperty<bool>();
        set => SetProperty(value);
    }

    public ProcessWindowInfo? SelectedProcess {
        get => GetProperty<ProcessWindowInfo>();
        set => SetProperty(value);
    }

    public string? FilterProcess {
        get => GetProperty<string?>();
        set {
            if (SetProperty(value)) {
                OnPropertyChanged(nameof(FilteredProcesses));
            }
        }
    }

    public bool IsWindowedOnly {
        get => GetProperty<bool>();
        set {
            if (SetProperty(value)) {
                _ = Init();
            }
        }
    }

    public async Task PickProcessAsync() {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        IntPtr hwnd = await WaitForMouseClickWindowAsync(cts.Token);
        SelectedProcess = _processes.FirstOrDefault(x => x.MainWindowHandle == hwnd);
        OnPropertyChanged(nameof(FilteredProcesses));
    }

    private bool FilterProcesses(ProcessWindowInfo p) {
        if (string.IsNullOrWhiteSpace(FilterProcess)) {
            return true;
        }

        return p.WindowTitle.Contains(FilterProcess, StringComparison.OrdinalIgnoreCase)
               || p.ProcessId.ToString().Contains(FilterProcess, StringComparison.OrdinalIgnoreCase)
               || p.ElementId.ToString().Contains(FilterProcess, StringComparison.OrdinalIgnoreCase);
    }

    private Task<IntPtr> WaitForMouseClickWindowAsync(CancellationToken ct) {
        TaskCompletionSource<IntPtr> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Cursor previousCursor = Cursor.Current;
        Cursor.Current = Cursors.Cross;

        _mouseProc = (nCode, wParam, lParam) => {
            const int WM_LBUTTONUP = 0x0202;

            if (nCode >= 0 && (wParam == (IntPtr)0x0200 || wParam == (IntPtr)WM_LBUTTONUP) && GetCursorPos(out POINT pt)) {
                IntPtr hwnd = WindowFromPoint(pt);
                IntPtr root = GetAncestor(hwnd, GaRoot);
                GetWindowThreadProcessId(root, out uint windowProcessId);

                if (windowProcessId != (uint)Process.GetCurrentProcess().Id) {
                    AutomationElement? topWindowUnderCursor = GetTopWindowUnderCursor();
                    if (_topWindowUnderCursor == null || !_topWindowUnderCursor.Equals(topWindowUnderCursor)) {
                        _topWindowOverlay?.Dispose();
                        try {
                            Rectangle rect = topWindowUnderCursor.Properties.BoundingRectangle.Value;
                            _topWindowOverlay = App.FlaUiAppOptions.PickOverlay();
                            _topWindowOverlay?.Show(rect);
                            _topWindowUnderCursor = topWindowUnderCursor;
                        } catch {
                        }
                    }
                } else {
                    _topWindowOverlay?.Dispose();
                    _topWindowUnderCursor = null;
                }

                if (wParam == (IntPtr)WM_LBUTTONUP) {
                    _topWindowOverlay?.Dispose();
                    _topWindowUnderCursor = null;
                    tcs.TrySetResult(windowProcessId != (uint)Process.GetCurrentProcess().Id ? root : IntPtr.Zero);
                }
            }

            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        };

        IntPtr hMod = GetModuleHandle(Process.GetCurrentProcess().MainModule?.ModuleName ?? string.Empty);
        _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, hMod, 0);

        ct.Register(() => tcs.TrySetCanceled());

        return tcs.Task.ContinueWith(t => {
            Cursor.Current = previousCursor;
            if (_mouseHook != IntPtr.Zero) {
                UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }
            return t.IsCompletedSuccessfully ? t.Result : IntPtr.Zero;
        });
    }

    public AutomationElement? GetTopWindowUnderCursor() {
        if (!GetCursorPos(out POINT pt)) return null;
        IntPtr hwnd = WindowFromPoint(pt);
        if (hwnd == IntPtr.Zero) return null;
        IntPtr rootHwnd = GetAncestor(hwnd, GaRoot);
        return rootHwnd == IntPtr.Zero ? null : _defaultAutomation.FromHandle(rootHwnd);
    }

    public async Task Init() {
        IsBusy = true;
        await Task.Delay(50);
        await SyncProcessesAsync();
        IsBusy = false;
    }

    public async Task<bool> SyncProcessesAsync() {
        int currentProcessId = Environment.ProcessId;
        var tcs = new TaskCompletionSource<Dictionary<IntPtr, ProcessWindowInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var scanThread = new Thread(() => {
            try {
                using var automation = new UIA3Automation();
                var found = new Dictionary<IntPtr, ProcessWindowInfo>();
                int skippedElements = 0;
                foreach (var x in GetChildren(automation.GetDesktop())) {
                    try {
                        if (string.IsNullOrEmpty(x.Name)) continue;
                        if (x.Properties.ProcessId == currentProcessId) continue;
                        var hwnd = x.Properties.NativeWindowHandle.Value;
                        if (found.ContainsKey(hwnd)) continue;
                        var hash = _idGenerator.GenerateElementHash(x, hwnd: hwnd, excludeName: true);
                        var elementId = _idGenerator.GenerateIdFromHash(hash);
                        found[hwnd] = new ProcessWindowInfo(x.Properties.ProcessId.Value, x.Name, hwnd, elementId);
                    } catch (Exception ex) when (
                        ex is System.Runtime.InteropServices.COMException ||
                        ex is UnauthorizedAccessException ||
                        ex is FlaUI.Core.Exceptions.PropertyNotSupportedException) {
                        skippedElements++;
                    }
                }

                if (skippedElements > 0) {
                    Debug.WriteLine($"[StartupViewModel.SyncProcessesAsync] Skipped {skippedElements} inaccessible UIA element(s) during scan.");
                }

                tcs.TrySetResult(found);
            } catch (Exception ex) when (
                ex is System.ComponentModel.Win32Exception ||
                ex is System.Runtime.InteropServices.COMException ||
                ex is UnauthorizedAccessException ||
                ex is FlaUI.Core.Exceptions.PropertyNotSupportedException) {
                Debug.WriteLine($"[StartupViewModel.SyncProcessesAsync] Scan failed: {ex.GetType().Name}: {ex.Message}");
                tcs.TrySetResult(new Dictionary<IntPtr, ProcessWindowInfo>());
            }
        }) { IsBackground = true };
        scanThread.SetApartmentState(ApartmentState.STA);
        scanThread.Start();
        var latest = await tcs.Task;

        bool changed = false;

        for (int i = _processes.Count - 1; i >= 0; i--) {
            ProcessWindowInfo existing = _processes[i];
            if (!latest.ContainsKey(existing.MainWindowHandle)) {
                _processes.RemoveAt(i);
                changed = true;
            }
        }

        foreach (ProcessWindowInfo candidate in latest.Values) {
            int index = _processes.ToList().FindIndex(x => x.MainWindowHandle == candidate.MainWindowHandle);
            if (index < 0) {
                _processes.Add(candidate);
                changed = true;
                continue;
            }

            if (!_processes[index].Equals(candidate)) {
                _processes[index] = candidate;
                changed = true;
            }
        }

        if (changed) {
            OnPropertyChanged(nameof(FilteredProcesses));
        }

        return changed;

        AutomationElement[] GetChildren(AutomationElement el) => IsWindowedOnly ? el.FindAllChildren(x => x.ByControlType(ControlType.Window)) : el.FindAllChildren();
    }

    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern IntPtr GetModuleHandle(string lpModuleName);

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X; public int Y; }
}

public record ProcessWindowInfo(int ProcessId, string WindowTitle, IntPtr MainWindowHandle, int ElementId);
