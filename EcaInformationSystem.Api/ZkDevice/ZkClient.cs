using System.Net.Sockets;

namespace EcaInformationSystem.Api.ZkDevice
{
    // Raw TCP client for ZKTeco's undocumented "pull SDK" protocol. Every
    // quirk here was confirmed against a live K14/ID device (firmware
    // Ver 6.60) by byte-diffing against pyzk — a mature, widely-used
    // reference implementation — since ZKTeco has never published an
    // official spec. See the comments inline for what each fix actually
    // corrects; several of these are NOT what community write-ups commonly
    // describe.
    public sealed class ZkClient : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly int _commKey;
        private readonly int _timeoutMs;

        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private ushort _sessionId;
        private ushort _replyId;

        public ZkClient(string host, int port, int commKey = 0, int timeoutMs = 15000)
        {
            _host = host;
            _port = port;
            _commKey = commKey;
            _timeoutMs = timeoutMs;
        }

        public async Task ConnectAsync(CancellationToken ct)
        {
            _tcpClient = new TcpClient { ReceiveTimeout = _timeoutMs, SendTimeout = _timeoutMs };

            // TcpClient.ConnectAsync has no timeout of its own — against a
            // wrong/unreachable IP (typo, device moved, off) the OS can take
            // far longer than _timeoutMs to give up (silently-dropped SYNs
            // can hang for minutes), which is what made a bad IP look like a
            // stuck "Connecting…" button instead of a prompt error. Bound it
            // explicitly, same as ReadExactAsync does for replies.
            using (var timeoutCts = new CancellationTokenSource(_timeoutMs))
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token))
            {
                try
                {
                    await _tcpClient.ConnectAsync(_host, _port, linkedCts.Token);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    throw new TimeoutException($"Could not reach {_host}:{_port} within {_timeoutMs}ms. Check the device IP address and that it's powered on.");
                }
            }

            _stream = _tcpClient.GetStream();

            // Deliberately NOT 0 — see SendCommandAsync: the checksum for a
            // packet is computed against the reply_id BEFORE it's incremented
            // and wrapped, so starting at 0xFFFE makes the first packet's
            // checksum-id 0xFFFE and its transmitted (post-increment,
            // wrapped) reply_id 0.
            _replyId = 0xFFFE;
            _sessionId = 0;

            var (command, payload) = await SendCommandAsync(ZkCommand.Connect, [], ct);

            if (command == ZkCommand.AckUnauth)
            {
                var authKey = MakeCommKey(_commKey, _sessionId, 50);
                var (authCommand, _) = await SendCommandAsync(ZkCommand.Auth, authKey, ct);
                if (authCommand != ZkCommand.AckOk)
                    throw new InvalidOperationException("Device rejected authentication — check the Comm Key configured under PC Connection.");
            }
            else if (command != ZkCommand.AckOk)
            {
                throw new InvalidOperationException($"Device rejected connect (reply command {command}).");
            }
        }

        public async Task DisableDeviceAsync(CancellationToken ct)
            => await SendCommandAsync(ZkCommand.DisableDevice, [], ct);

        public async Task EnableDeviceAsync(CancellationToken ct)
            => await SendCommandAsync(ZkCommand.EnableDevice, [], ct);

        public async Task DisconnectAsync(CancellationToken ct)
        {
            if (_stream is null)
                return;

            try
            {
                await SendCommandAsync(ZkCommand.Exit, [], ct);
            }
            catch
            {
                // best-effort — we're closing the socket regardless
            }
        }

        public async Task<List<ZkAttendanceRecord>> GetAttendanceLogsAsync(ILogger logger, CancellationToken ct)
        {
            // Matches pyzk's get_attendance(): check the record count via
            // CMD_GET_FREE_SIZES first and skip the pull entirely when
            // there's nothing to fetch — a records=0 read request just hangs
            // with no reply at all on this firmware.
            var (_, records) = await GetDeviceCountsAsync(ct);
            logger.LogInformation("Device reports {Records} attendance record(s)", records);
            if (records == 0)
                return [];

            var buffer = await ReadWithBufferAsync(ZkCommand.AttLogRrq, ct);
            return ParseAttendanceRecords(buffer, logger);
        }

        public async Task<List<ZkEnrolledUser>> GetEnrolledUsersAsync(ILogger logger, CancellationToken ct)
        {
            var (users, _) = await GetDeviceCountsAsync(ct);
            if (users == 0)
                return [];

            var buffer = await ReadWithBufferAsync(ZkCommand.UserTempRrq, ct, fct: ZkCommand.FctUser);
            return ParseUserRecords(buffer, logger);
        }

        // CMD_GET_FREE_SIZES reply is a run of 20 little-endian int32 fields;
        // users is field[4] (byte offset 16), records is field[8] (offset 32).
        // Cheap (~1 round trip) compared to actually pulling the data —
        // ZkSyncRunner calls this up front to decide whether a full pull is
        // even worth doing, since polling every minute makes redoing a full
        // 10k+-record pull when nothing changed wasteful.
        public async Task<(int Users, int Records)> GetDeviceCountsAsync(CancellationToken ct)
        {
            var (_, payload) = await SendCommandAsync(ZkCommand.GetFreeSizes, [], ct);
            if (payload.Length < 36)
                return (0, 0);

            var users = BitConverter.ToInt32(payload, 16);
            var records = BitConverter.ToInt32(payload, 32);
            return (users, records);
        }

        private async Task<byte[]> ReadWithBufferAsync(ushort command, CancellationToken ct, int fct = 0)
        {
            // sub-command(byte,1) + command(short,2) + fct(int,4) + ext(int,4) = 11 bytes
            var requestPayload = new byte[11];
            requestPayload[0] = 1;
            BitConverter.GetBytes(command).CopyTo(requestPayload, 1);
            BitConverter.GetBytes(fct).CopyTo(requestPayload, 3);

            var (replyCommand, replyPayload) = await SendCommandAsync(ZkCommand.DataWrrq, requestPayload, ct);

            if (replyCommand == ZkCommand.Data)
                return replyPayload;

            if (replyCommand is ZkCommand.PrepareData or ZkCommand.AckOk)
            {
                // Large transfers require the client to explicitly request
                // each chunk via command 1504 with (start, size); each
                // chunk's reply is itself a nested PrepareData-then-Data pair
                // followed by a separate ACK_OK confirming it — matches
                // pyzk's __read_chunk/__recieve_chunk exactly.
                // Confirmed against a live device (byte-diffed against
                // pyzk): a PrepareData(1500) reply carries the size at
                // payload offset 0, but an AckOk(2000) "buffered read
                // initiated" reply — which is what large transfers like the
                // full attendance log actually get — carries it at offset 1
                // (there's a leading status byte). Using offset 0 for both,
                // as this used to, reads garbage for the AckOk case and the
                // resulting bogus totalSize makes the chunk loop keep
                // requesting data long after the device's real data ends.
                var sizeOffset = replyCommand == ZkCommand.AckOk ? 1 : 0;
                if (replyPayload.Length < sizeOffset + 4)
                    return [];

                var totalSize = BitConverter.ToInt32(replyPayload, sizeOffset);
                if (totalSize <= 0)
                    return [];

                var data = new byte[totalSize];
                var start = 0;
                const int maxChunk = 0xFFC0; // 65472 — TCP chunk size, matches pyzk

                while (start < totalSize && !ct.IsCancellationRequested)
                {
                    var chunkSize = Math.Min(maxChunk, totalSize - start);
                    var chunkRequest = new byte[8];
                    BitConverter.GetBytes(start).CopyTo(chunkRequest, 0);
                    BitConverter.GetBytes(chunkSize).CopyTo(chunkRequest, 4);

                    var (chunkCommand, chunkPayload) = await SendCommandAsync(ZkCommand.ReadChunk, chunkRequest, ct);
                    var chunkData = await ReceiveBulkPayloadAsync(chunkCommand, chunkPayload, chunkSize, ct);

                    var copyLength = Math.Min(chunkData.Length, totalSize - start);
                    Array.Copy(chunkData, 0, data, start, copyLength);
                    start += copyLength;

                    var (ackCommand, _) = await ReceivePacketAsync(ct);
                    if (ackCommand != ZkCommand.AckOk)
                        throw new InvalidOperationException($"Expected ACK_OK after data chunk at offset {start}, got {ackCommand}.");
                }

                await SendCommandAsync(ZkCommand.FreeData, [], ct);
                return data;
            }

            throw new InvalidOperationException($"Unexpected reply command {replyCommand} while reading attendance data.");
        }

        // Given the first reply to a data-request command, resolves it into
        // the actual payload bytes — either it arrived immediately (Data) or
        // it's announced via a size prefix (PrepareData/AckOk) with the real
        // bytes following in a single subsequent CMD_DATA frame whose TCP
        // length covers the WHOLE chunk (pyzk's __recieve_tcp_data confirms
        // large chunks arrive as one frame, not split into many small ones —
        // the earlier hand-port assumed the latter, which is what caused the
        // stream desync). expectedSize both sanity-checks and lets us stop
        // as soon as we have enough, in case the frame is padded.
        private async Task<byte[]> ReceiveBulkPayloadAsync(ushort command, byte[] payload, int expectedSize, CancellationToken ct)
        {
            if (command == ZkCommand.Data)
                return payload;

            if (command is ZkCommand.PrepareData or ZkCommand.AckOk)
            {
                if (payload.Length < 4)
                    return [];

                var size = BitConverter.ToInt32(payload, 0);
                if (size <= 0)
                    return [];

                var buf = new byte[size];
                var offset = 0;
                while (offset < size && !ct.IsCancellationRequested)
                {
                    var (c, p) = await ReceivePacketAsync(ct);
                    if (c != ZkCommand.Data)
                        throw new InvalidOperationException($"Unexpected reply command {c} while accumulating bulk data at offset {offset} (expected {size} total).");

                    var n = Math.Min(p.Length, size - offset);
                    Array.Copy(p, 0, buf, offset, n);
                    offset += n;
                }
                return buf;
            }

            throw new InvalidOperationException($"Unexpected reply command {command} while reading bulk data (expected size {expectedSize}).");
        }

        private static List<ZkAttendanceRecord> ParseAttendanceRecords(byte[] buffer, ILogger logger)
        {
            var records = new List<ZkAttendanceRecord>();
            if (buffer.Length < 4)
                return records;

            var body = buffer[4..];

            int[] candidateSizes = [40, 16, 8];
            var recordSize = candidateSizes.FirstOrDefault(s => body.Length % s == 0 && body.Length / s > 0);

            if (recordSize == 0)
            {
                logger.LogWarning(
                    "Could not determine attendance record size for a {Length}-byte buffer (tried 40/16/8). First 32 bytes: {Hex}",
                    body.Length, Convert.ToHexString(body.Take(32).ToArray()));
                return records;
            }

            for (var offset = 0; offset + recordSize <= body.Length; offset += recordSize)
            {
                var record = body.AsSpan(offset, recordSize);
                try
                {
                    records.Add(recordSize switch
                    {
                        40 => ParseRecord40(record),
                        16 => ParseRecord16(record),
                        _ => ParseRecord8(record),
                    });
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse a {Size}-byte attendance record at offset {Offset}: {Hex}",
                        recordSize, offset, Convert.ToHexString(record));
                }
            }

            return records;
        }

        private static ZkAttendanceRecord ParseRecord16(ReadOnlySpan<byte> r) => new()
        {
            BiometricUserId = ExtractAsciiId(r.Slice(2, 8)),
            Status = r[10],
            PunchTime = DecodeTime(BitConverter.ToUInt32(r.Slice(11, 4))),
            VerifyMode = r[15]
        };

        // Confirmed against pyzk's own struct for this format: '<H24sB4sB8s'
        // = uid(2) + user_id(24) + status(1) + timestamp(4) + punch(1) +
        // reserved(8) = 40 bytes. The leading 2-byte uid field was missing
        // here before, shifting every other field left by 2 bytes — that's
        // what produced garbled dates and truncated user IDs.
        private static ZkAttendanceRecord ParseRecord40(ReadOnlySpan<byte> r) => new()
        {
            BiometricUserId = ExtractAsciiId(r.Slice(2, 24)),
            Status = r[26],
            PunchTime = DecodeTime(BitConverter.ToUInt32(r.Slice(27, 4))),
            VerifyMode = r.Length > 31 ? r[31] : 0
        };

        private static ZkAttendanceRecord ParseRecord8(ReadOnlySpan<byte> r) => new()
        {
            BiometricUserId = BitConverter.ToUInt16(r[..2]).ToString(),
            Status = r[2],
            PunchTime = DecodeTime(BitConverter.ToUInt32(r.Slice(3, 4))),
            VerifyMode = r.Length > 7 ? r[7] : 0
        };

        private static List<ZkEnrolledUser> ParseUserRecords(byte[] buffer, ILogger logger)
        {
            var users = new List<ZkEnrolledUser>();
            if (buffer.Length < 4)
                return users;

            var body = buffer[4..];
            int[] candidateSizes = [72, 28];
            var recordSize = candidateSizes.FirstOrDefault(s => body.Length % s == 0 && body.Length / s > 0);

            if (recordSize == 0)
            {
                logger.LogWarning(
                    "Could not determine user record size for a {Length}-byte buffer (tried 72/28). First 32 bytes: {Hex}",
                    body.Length, Convert.ToHexString(body.Take(32).ToArray()));
                return users;
            }

            for (var offset = 0; offset + recordSize <= body.Length; offset += recordSize)
            {
                var record = body.AsSpan(offset, recordSize);
                try
                {
                    users.Add(recordSize == 72 ? ParseUserRecord72(record) : ParseUserRecord28(record));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse a {Size}-byte user record at offset {Offset}: {Hex}",
                        recordSize, offset, Convert.ToHexString(record));
                }
            }

            return users;
        }

        // Confirmed against pyzk's own struct for this format: '<HB8s24sIx7sx24s'
        // = uid(2) + privilege(1) + password(8) + name(24) + card(4) + pad(1)
        // + group_id(7) + pad(1) + user_id(24) = 72 bytes. Name was reading
        // from the password field before (offset 3, usually blank — that's
        // why every enrolled user came back with an empty name); the real
        // name field starts at offset 11.
        private static ZkEnrolledUser ParseUserRecord72(ReadOnlySpan<byte> r) => new()
        {
            Privilege = r[2],
            CardNumber = BitConverter.ToUInt32(r.Slice(35, 4)) is var card && card != 0 ? card.ToString() : null,
            Name = ExtractAsciiId(r.Slice(11, 24)),
            BiometricUserId = ExtractAsciiId(r.Slice(48, 24))
        };

        private static ZkEnrolledUser ParseUserRecord28(ReadOnlySpan<byte> r) => new()
        {
            Privilege = r[2],
            CardNumber = BitConverter.ToUInt32(r.Slice(16, 4)) is var card && card != 0 ? card.ToString() : null,
            Name = ExtractAsciiId(r.Slice(8, 8)),
            BiometricUserId = BitConverter.ToUInt32(r.Slice(24, 4)).ToString()
        };

        private static string ExtractAsciiId(ReadOnlySpan<byte> bytes)
        {
            var nullIndex = bytes.IndexOf((byte)0);
            var slice = nullIndex >= 0 ? bytes[..nullIndex] : bytes;
            return System.Text.Encoding.ASCII.GetString(slice).Trim();
        }

        private static DateTime DecodeTime(uint packed)
        {
            var t = (long)packed;
            var second = t % 60; t /= 60;
            var minute = t % 60; t /= 60;
            var hour = t % 24; t /= 24;
            var day = t % 31 + 1; t /= 31;
            var month = t % 12 + 1; t /= 12;
            var year = t + 2000;
            return new DateTime((int)year, (int)month, (int)day, (int)hour, (int)minute, (int)second);
        }

        private async Task<(ushort Command, byte[] Payload)> SendCommandAsync(ushort command, byte[] payload, CancellationToken ct)
        {
            if (_stream is null)
                throw new InvalidOperationException("Not connected.");

            var checksumReplyId = _replyId;
            _replyId = (ushort)((_replyId + 1) % ushort.MaxValue);
            var header = CreateHeader(command, payload, _sessionId, checksumReplyId, _replyId);

            var tcpFramed = WrapTcp(header);
            await _stream.WriteAsync(tcpFramed, ct);

            return await ReceivePacketAsync(ct);
        }

        private async Task<(ushort Command, byte[] Payload)> ReceivePacketAsync(CancellationToken ct)
        {
            if (_stream is null)
                throw new InvalidOperationException("Not connected.");

            var tcpHeader = await ReadExactAsync(8, ct);
            var length = BitConverter.ToUInt32(tcpHeader, 4);

            if (length < 8)
                throw new InvalidOperationException("Malformed reply from device (frame too short).");

            var packet = await ReadExactAsync((int)length, ct);

            var command = BitConverter.ToUInt16(packet, 0);
            _sessionId = BitConverter.ToUInt16(packet, 4);
            var payload = packet.Length > 8 ? packet[8..] : [];

            return (command, payload);
        }

        private async Task<byte[]> ReadExactAsync(int count, CancellationToken ct)
        {
            using var timeoutCts = new CancellationTokenSource(_timeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            var buffer = new byte[count];
            var read = 0;
            try
            {
                while (read < count)
                {
                    var n = await _stream!.ReadAsync(buffer.AsMemory(read, count - read), linkedCts.Token);
                    if (n == 0)
                        throw new IOException("Device closed the connection while reading a reply.");
                    read += n;
                }
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                throw new TimeoutException($"Device did not reply within {_timeoutMs}ms.");
            }
            return buffer;
        }

        private static byte[] CreateHeader(ushort command, byte[] commandString, ushort sessionId, ushort checksumReplyId, ushort transmitReplyId)
        {
            var checksumBuf = new byte[8 + commandString.Length];
            BitConverter.GetBytes(command).CopyTo(checksumBuf, 0);
            BitConverter.GetBytes(sessionId).CopyTo(checksumBuf, 4);
            BitConverter.GetBytes(checksumReplyId).CopyTo(checksumBuf, 6);
            commandString.CopyTo(checksumBuf, 8);
            var checksum = CreateChecksum(checksumBuf);

            var buf = new byte[8 + commandString.Length];
            BitConverter.GetBytes(command).CopyTo(buf, 0);
            BitConverter.GetBytes(checksum).CopyTo(buf, 2);
            BitConverter.GetBytes(sessionId).CopyTo(buf, 4);
            BitConverter.GetBytes(transmitReplyId).CopyTo(buf, 6);
            commandString.CopyTo(buf, 8);
            return buf;
        }

        private static ushort CreateChecksum(byte[] data)
        {
            var checksum = 0;
            var i = 0;
            var remaining = data.Length;
            while (remaining > 1)
            {
                checksum += BitConverter.ToUInt16(data, i);
                if (checksum > 0xFFFF) checksum -= 0xFFFF;
                i += 2;
                remaining -= 2;
            }
            if (remaining == 1)
                checksum += data[i];

            while (checksum > 0xFFFF)
                checksum -= 0xFFFF;

            checksum = ~checksum;
            while (checksum < 0)
                checksum += 0xFFFF;

            return (ushort)checksum;
        }

        private static byte[] WrapTcp(byte[] packet)
        {
            var top = new byte[8 + packet.Length];
            top[0] = 0x50; top[1] = 0x50; top[2] = 0x82; top[3] = 0x7D;
            BitConverter.GetBytes((uint)packet.Length).CopyTo(top, 4);
            packet.CopyTo(top, 8);
            return top;
        }

        private static byte[] MakeCommKey(int key, ushort sessionId, int ticks)
        {
            uint k = 0;
            for (var i = 0; i < 32; i++)
                k = ((key & (1 << i)) != 0) ? (k << 1) | 1 : k << 1;
            k = unchecked(k + sessionId);

            var kb = BitConverter.GetBytes(k);
            kb[0] ^= (byte)'Z';
            kb[1] ^= (byte)'K';
            kb[2] ^= (byte)'S';
            kb[3] ^= (byte)'O';

            var h1 = BitConverter.ToUInt16(kb, 0);
            var h2 = BitConverter.ToUInt16(kb, 2);
            var swapped = new byte[4];
            BitConverter.GetBytes(h2).CopyTo(swapped, 0);
            BitConverter.GetBytes(h1).CopyTo(swapped, 2);

            var b = (byte)(ticks & 0xFF);
            var result = new byte[4];
            result[0] = (byte)(swapped[0] ^ b);
            result[1] = (byte)(swapped[1] ^ b);
            result[2] = b;
            result[3] = (byte)(swapped[3] ^ b);
            return result;
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _tcpClient?.Dispose();
        }
    }
}
