using System.Buffers.Binary;

namespace GameEngine.Transport;

public class LengthPrefixedFramer : IMessageFramer
{
    private byte[] _buffer = Array.Empty<byte>();
    private int _offset = 0;

    // 4 bytes for length
    private const int HeaderSize = 4;

    public IEnumerable<byte[]> UnframeData(ReadOnlySpan<byte> data)
    {
        // Append new data to existing buffer
        int neededSize = _offset + data.Length;
        if (_buffer.Length < neededSize)
        {
            Array.Resize(ref _buffer, Math.Max(neededSize, _buffer.Length * 2));
        }

        data.CopyTo(_buffer.AsSpan(_offset));
        _offset += data.Length;

        var frames = new List<byte[]>();

        // Process buffer
        int readHead = 0;
        while (readHead + HeaderSize <= _offset)
        {
            // Read length
            int payloadLength = BinaryPrimitives.ReadInt32BigEndian(_buffer.AsSpan(readHead, HeaderSize));

            // Check if we have the full payload
            if (readHead + HeaderSize + payloadLength <= _offset)
            {
                // Extract payload
                byte[] frame = new byte[payloadLength];
                Array.Copy(_buffer, readHead + HeaderSize, frame, 0, payloadLength);
                frames.Add(frame);

                readHead += HeaderSize + payloadLength;
            }
            else
            {
                // Not enough data yet
                break;
            }
        }

        // Shift remaining data to start
        if (readHead > 0)
        {
            int remaining = _offset - readHead;
            if (remaining > 0)
            {
                Array.Copy(_buffer, readHead, _buffer, 0, remaining);
            }

            _offset = remaining;
        }

        return frames;
    }

    public byte[] FrameData(byte[] data)
    {
        byte[] frame = new byte[HeaderSize + data.Length];
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(0, HeaderSize), data.Length);
        data.CopyTo(frame, HeaderSize);
        return frame;
    }
}