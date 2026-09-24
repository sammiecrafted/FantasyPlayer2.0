using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FantasyPlayer.Provider.Local
{
    /// <summary>
    /// Minimal async client for the MPD (Music Player Daemon) text protocol.
    /// MPD exposes status, playback control and playback queue over a plain TCP line protocol.
    /// </summary>
    public sealed class MpdClient : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string? _password;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        private TcpClient? _tcp;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private bool _connected;
        private string? _lastError;

        public MpdClient(string host, int port, string? password)
        {
            _host = host;
            _port = port;
            _password = string.IsNullOrEmpty(password) ? null : password;
        }

        public string? LastError => _lastError;

        public bool IsConnected => _connected;

        public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                Close();
                _tcp = new TcpClient();
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));
                await _tcp.ConnectAsync(_host, _port).WaitAsync(timeoutCts.Token);
                var stream = _tcp.GetStream();
                _reader = new StreamReader(stream, Encoding.UTF8);
                _writer = new StreamWriter(stream, Encoding.UTF8) { NewLine = "\n", AutoFlush = true };

                var greeting = await _reader.ReadLineAsync().WaitAsync(timeoutCts.Token);
                if (greeting == null || !greeting.StartsWith("OK", StringComparison.Ordinal))
                {
                    _lastError = $"Unexpected MPD greeting: {greeting}";
                    Close();
                    return false;
                }

                if (_password != null)
                {
                    await SendRawAsync($"password \"{_password.Replace("\"", "\\\"")}\"").WaitAsync(timeoutCts.Token);
                    if (_reader.ReadLineAsync().WaitAsync(timeoutCts.Token).Result == "ACK")
                    {
                        _lastError = "MPD rejected the password.";
                        Close();
                        return false;
                    }
                }

                _connected = true;
                _lastError = null;
                return true;
            }
            catch (OperationCanceledException)
            {
                _lastError = $"Timed out connecting to MPD at {_host}:{_port}.";
                Close();
                return false;
            }
            catch (Exception e)
            {
                _lastError = $"Could not connect to MPD at {_host}:{_port}: {e.Message}";
                Close();
                return false;
            }
        }

        /// <summary>
        /// Sends a single command and returns the raw response lines (excluding the final OK/ACK line).
        /// MPD processes one command at a time, so commands are serialized.
        /// </summary>
        public async Task<List<string>> SendAsync(string command, CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (!_connected || _writer == null || _reader == null)
                {
                    if (!await ConnectAsync(cancellationToken))
                    {
                        return new List<string>();
                    }
                }

                await SendRawAsync(command).WaitAsync(cancellationToken);
                var lines = new List<string>();
                var acked = false;
                while (true)
                {
                    var line = await _reader.ReadLineAsync().WaitAsync(cancellationToken);
                    if (line == null)
                    {
                        _connected = false;
                        break;
                    }

                    if (line == "OK")
                    {
                        break;
                    }

                    if (line.StartsWith("ACK", StringComparison.Ordinal))
                    {
                        _lastError = $"MPD error: {line}";
                        acked = true;
                        break;
                    }

                    lines.Add(line);
                }

                if (acked)
                {
                    return new List<string>();
                }

                _lastError = null;
                return lines;
            }
            catch (Exception e)
            {
                _connected = false;
                _lastError = $"MPD communication failed: {e.Message}";
                return new List<string>();
            }
            finally
            {
                _gate.Release();
            }
        }

        private Task SendRawAsync(string command)
        {
            if (_writer == null)
            {
                return Task.CompletedTask;
            }

            return _writer.WriteLineAsync(command);
        }

        public Dictionary<string, string> ParseEntries(List<string> lines)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines)
            {
                var idx = line.IndexOf(':');
                if (idx <= 0)
                {
                    continue;
                }

                var key = line.Substring(0, idx).Trim();
                var value = line.Substring(idx + 1).Trim();
                dict[key] = value;
            }

            return dict;
        }

        public IEnumerable<Dictionary<string, string>> ParseSections(List<string> lines)
        {
            var sections = new List<Dictionary<string, string>>();
            Dictionary<string, string>? current = null;
            foreach (var line in lines)
            {
                var idx = line.IndexOf(':');
                if (idx <= 0)
                {
                    continue;
                }

                var key = line.Substring(0, idx).Trim();
                var value = line.Substring(idx + 1).Trim();
                if (key == "file" && current != null && current.ContainsKey("file"))
                {
                    current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    sections.Add(current);
                }

                current ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                current[key] = value;
            }

            if (current != null && !sections.Contains(current))
            {
                sections.Add(current);
            }

            return sections;
        }

        public async Task<Dictionary<string, string>> StatusAsync(CancellationToken cancellationToken = default)
        {
            return ParseEntries(await SendAsync("status", cancellationToken));
        }

        public async Task<Dictionary<string, string>> CurrentSongAsync(CancellationToken cancellationToken = default)
        {
            return ParseEntries(await SendAsync("currentsong", cancellationToken));
        }

        public async Task<bool> CommandAsync(string command, CancellationToken cancellationToken = default)
        {
            var lines = await SendAsync(command, cancellationToken);
            return _connected;
        }

        public void Dispose()
        {
            Close();
            _gate.Dispose();
        }

        private void Close()
        {
            _connected = false;
            _reader?.Dispose();
            _writer?.Dispose();
            _tcp?.Close();
            _tcp = null;
            _reader = null;
            _writer = null;
        }
    }
}