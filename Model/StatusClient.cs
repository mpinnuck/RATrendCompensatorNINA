using System;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace RATrendCompensatorNINA.Model {

    /// <summary>
    /// Long-lived client for RA_TrendCompensator's status_server.py: connects to a
    /// single TCP host/port, reads newline-delimited JSON snapshots as they're
    /// pushed, and reconnects with a short backoff if the compensator app isn't
    /// running yet or the connection drops. There is nothing to send -- the
    /// socket is push-only by design on the Python side.
    /// </summary>
    public class StatusClient : IDisposable {
        private const int ReconnectDelayMs = 3000;
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions {
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
        };

        private readonly string host;
        private readonly int port;
        private readonly bool verboseLogging;
        private CancellationTokenSource cts;
        private Task runTask;

        public event EventHandler<RaTrendStatusSnapshot> SnapshotReceived;
        public event EventHandler<bool> ConnectionStateChanged;

        public bool IsConnected { get; private set; }

        private void Log(string message, bool verboseOnly = false) {
            if (verboseOnly && !verboseLogging) return;
            PluginLog.Write("StatusClient", message);
        }

        public StatusClient(string host, int port, bool verboseLogging) {
            this.host = host;
            this.port = port;
            this.verboseLogging = verboseLogging;
        }

        public void Start() {
            if (runTask != null) return;
            cts = new CancellationTokenSource();
            runTask = Task.Run(() => RunLoopAsync(cts.Token));
        }

        public void Stop() {
            cts?.Cancel();
            try {
                runTask?.Wait(TimeSpan.FromSeconds(2));
            } catch (AggregateException) {
                // expected on cancellation
            }
            runTask = null;
            SetConnected(false);
        }

        private async Task RunLoopAsync(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                try {
                    using var client = new TcpClient();
                    Log($"Connecting to {host}:{port}", verboseOnly: true);
                    var connectTask = client.ConnectAsync(host, port);
                    var completed = await Task.WhenAny(connectTask, Task.Delay(ReconnectDelayMs, token));
                    if (completed != connectTask || !client.Connected) {
                        SetConnected(false);
                        Log("Connect attempt timed out or was unsuccessful.", verboseOnly: true);
                        continue;
                    }

                    SetConnected(true);
                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream);

                    while (!token.IsCancellationRequested && client.Connected) {
                        var readTask = reader.ReadLineAsync();
                        var line = await readTask.WaitAsync(token).ConfigureAwait(false);
                        if (line == null) break; // remote closed the connection
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        try {
                            var snapshot = JsonSerializer.Deserialize<RaTrendStatusSnapshot>(line, JsonOptions);
                            if (snapshot != null) {
                                Log($"Snapshot received: running={snapshot.Running}, dry_run={snapshot.DryRun}, slope={snapshot.LastSlopeArcsecPerSec}, rms={snapshot.GuideRmsArcsec}", verboseOnly: true);
                                try {
                                    SnapshotReceived?.Invoke(this, snapshot);
                                } catch (Exception ex) {
                                    Log($"Snapshot handler threw: {ex.GetType().Name}: {ex.Message}");
                                }
                            }
                        } catch (JsonException) {
                            Log($"Snapshot parse failed. Raw line: {line}");
                            // A partial/corrupt line -- skip it and wait for the next one
                            // rather than tearing down the connection over it.
                        }
                    }
                } catch (OperationCanceledException) {
                    Log("Status client canceled.");
                    // Stop() was called
                } catch (SocketException) {
                    Log("Socket error while connecting/reading status stream.");
                    // RA_TrendCompensator isn't running or refused the connection -- retry
                } catch (IOException) {
                    Log("IO error while reading status stream.");
                    // connection dropped mid-read -- retry
                } finally {
                    SetConnected(false);
                }

                if (!token.IsCancellationRequested) {
                    try {
                        await Task.Delay(ReconnectDelayMs, token).ConfigureAwait(false);
                    } catch (OperationCanceledException) {
                        // Stop() was called during the backoff
                    }
                }
            }
        }

        private void SetConnected(bool connected) {
            if (IsConnected == connected) return;
            IsConnected = connected;
            Log(connected ? "Connected." : "Disconnected.");
            try {
                ConnectionStateChanged?.Invoke(this, connected);
            } catch (Exception ex) {
                Log($"ConnectionStateChanged handler threw: {ex.GetType().Name}: {ex.Message}");
            }
        }

        public void Dispose() {
            Stop();
            cts?.Dispose();
        }
    }
}
