using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace RATrendCompensatorNINA.Model {

    /// <summary>
    /// Owns the lifecycle of the external RA_TrendCompensator process (the
    /// Python app, or a PyInstaller-built .exe of it). Launches it when the
    /// plugin initializes and asks it to close gracefully on shutdown.
    ///
    /// Graceful shutdown relies on Process.CloseMainWindow(), which sends
    /// the same WM_CLOSE message any window gets when its X button is
    /// clicked. The Python app's main_window.py already binds
    /// WM_DELETE_WINDOW to a handler that calls view_model.shutdown()
    /// (stops PHD2, resets RightAscensionRate to zero, stops the status
    /// server) before destroying the window -- so this reuses an
    /// already-tested code path with zero new Python-side protocol needed.
    /// Falls back to a hard Kill() only if the app doesn't close within
    /// the timeout, since a hung/unresponsive process would otherwise
    /// block NINA's own shutdown indefinitely.
    /// </summary>
    public class AppLifecycleManager : IDisposable {
        private const int SwShow = 5;

        private readonly string executablePath;
        private readonly string arguments;
        private readonly Action<string> logger;
        private Process process;

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public AppLifecycleManager(string executablePath, string arguments, Action<string> logger) {
            this.executablePath = executablePath;
            this.arguments = arguments;
            this.logger = logger;
        }

        private void SafeLog(string message) {
            try {
                logger?.Invoke(message);
            } catch {
                // Best-effort logging only; never let logging failures break NINA/plugin startup.
            }
        }

        public void Launch() {
            if (string.IsNullOrWhiteSpace(executablePath)) {
                SafeLog("No RA_TrendCompensator executable path configured -- not launching it. " +
                        "Set one on this plugin's Options page, or launch it yourself; this panel " +
                        "will still connect to its status socket if it's already running.");
                return;
            }

            if (process != null && !process.HasExited) {
                ScheduleFocusHostWindow();
                SafeLog("RA_TrendCompensator already running -- not launching a second instance.");
                return;
            }

            if (TryAttachToRunningProcess(out var runningProcess)) {
                process = runningProcess;
                ScheduleFocusHostWindow();
                SafeLog("RA_TrendCompensator already running -- attached to existing process.");
                return;
            }

            try {
                var startInfo = new ProcessStartInfo {
                    FileName = executablePath,
                    Arguments = arguments ?? "",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal,
                };
                process = Process.Start(startInfo);
                if (process != null) {
                    ScheduleFocusHostWindowAfterLaunch(process);
                } else {
                    ScheduleFocusHostWindow();
                }
                SafeLog(process == null
                    ? $"WARNING: RA_TrendCompensator launch returned no process handle for '{executablePath}'."
                    : $"Launched RA_TrendCompensator: {executablePath} {arguments}");
            } catch (Exception e) {
                SafeLog($"WARNING: failed to launch RA_TrendCompensator at '{executablePath}': {e.Message}");
            }
        }

        private bool TryAttachToRunningProcess(out Process runningProcess) {
            runningProcess = null;

            string fullPath;
            try {
                fullPath = Path.GetFullPath(executablePath);
            } catch {
                return false;
            }

            var processName = Path.GetFileNameWithoutExtension(fullPath);
            if (string.IsNullOrWhiteSpace(processName)) {
                return false;
            }

            foreach (var candidate in Process.GetProcessesByName(processName)) {
                try {
                    if (candidate.HasExited) {
                        candidate.Dispose();
                        continue;
                    }

                    var candidatePath = candidate.MainModule?.FileName;
                    if (string.IsNullOrWhiteSpace(candidatePath)) {
                        candidate.Dispose();
                        continue;
                    }

                    if (string.Equals(Path.GetFullPath(candidatePath), fullPath, StringComparison.OrdinalIgnoreCase)) {
                        runningProcess = candidate;
                        return true;
                    }
                } catch {
                    candidate.Dispose();
                }
            }

            return false;
        }

        private bool TryFocusHostWindow() {
            var hostProcess = Process.GetCurrentProcess();

            try {
                hostProcess.Refresh();
                var handle = hostProcess.MainWindowHandle;
                if (handle != IntPtr.Zero) {
                    ShowWindow(handle, SwShow);
                    SetForegroundWindow(handle);
                    return true;
                }
            } catch {
            }

            return false;
        }

        private void ScheduleFocusHostWindow() {
            _ = Task.Run(async () => {
                for (var i = 0; i < 20; i++) {
                    try {
                        if (TryFocusHostWindow()) {
                            return;
                        }
                    } catch {
                    }

                    await Task.Delay(250).ConfigureAwait(false);
                }

                SafeLog("WARNING: unable to bring NINA to foreground after launch/attach.");
            });
        }

        private void ScheduleFocusHostWindowAfterLaunch(Process launchedProcess) {
            _ = Task.Run(async () => {
                for (var i = 0; i < 60; i++) {
                    try {
                        if (launchedProcess.HasExited) {
                            return;
                        }

                        launchedProcess.Refresh();
                        if (launchedProcess.MainWindowHandle != IntPtr.Zero) {
                            for (var j = 0; j < 16; j++) {
                                TryFocusHostWindow();
                                await Task.Delay(250).ConfigureAwait(false);
                            }
                            return;
                        }
                    } catch {
                    }

                    await Task.Delay(250).ConfigureAwait(false);
                }

                ScheduleFocusHostWindow();
            });
        }

        /// <summary>Requests a graceful close; falls back to a hard kill if it doesn't exit in time.</summary>
        public void Shutdown(TimeSpan timeout) {
            if (process == null || process.HasExited) return;

            try {
                SafeLog("Requesting RA_TrendCompensator to close...");
                process.CloseMainWindow();
                if (!process.WaitForExit((int)timeout.TotalMilliseconds)) {
                    SafeLog("RA_TrendCompensator did not close in time -- forcing termination.");
                    process.Kill();
                }
            } catch (Exception e) {
                SafeLog($"WARNING: error shutting down RA_TrendCompensator: {e.Message}");
            }
        }

        public void Dispose() {
            Shutdown(TimeSpan.FromSeconds(5));
            process?.Dispose();
        }
    }
}
