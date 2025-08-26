using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace SerialDataDisplay
{
    // 原始数据类
    public class RawData
    {
        public byte[] Data { get; set; }
    }

    // 解析后的数据类
    public class ParsedData
    {
        public byte[] Header { get; set; } // 帧头
        public byte BulletNumber { get; set; } // 弹号
        public byte Command { get; set; } // 命令字  
        public byte DataLength { get; set; } // 数据长度
        public byte[] Data { get; set; } // 数据
        public byte CheckSum { get; set; } // 校验和
    }

    // 解析策略接口
    public interface IParseStrategy
    {
        ParsedData Parse(RawData rawData);
    }

    // 解析策略实现类
    public class ParseStrategy : IParseStrategy
    {
        public ParsedData Parse(RawData rawData)
        {
            var data = rawData.Data;
            ValidateDataLength(data);
            byte command = data[3];
            byte dataLength = data[4];

            ValidateCommand(command);
            ValidateSpecificDataLength(command, dataLength);

            var parsed = new ParsedData
            {
                Header = new[] { data[0], data[1] },
                BulletNumber = data[2],
                Command = command,
                DataLength = dataLength,
                Data = new byte[dataLength]
            };

            Array.Copy(data, 5, parsed.Data, 0, dataLength);
            CalculateChecksum(parsed);
            ValidateChecksum(parsed, rawData.Data);
            return parsed;
        }

        private void ValidateDataLength(byte[] data)
        {
            if (data.Length < 6)
            {
                throw new ArgumentException("数据长度不足");
            }
        }

        private void ValidateCommand(byte command)
        {
            switch (command)
            {
                case 0xF0:
                case 0xF1:
                case 0xF2:
                case 0xF4:
                    break;
                default:
                    throw new NotSupportedException($"未知命令: 0x{command:X2}");
            }
        }

        private void ValidateSpecificDataLength(byte command, byte dataLength)
        {
            switch (command)
            {
                case 0xF0:
                    if (dataLength != 5)
                        throw new ArgumentException("F0 数据长度应为 5 字节");
                    break;
                case 0xF1:
                    if (dataLength != 0x19)
                        throw new ArgumentException("F1 数据长度应为 25 字节");
                    break;
                case 0xF2:
                    if (dataLength != 8)
                        throw new ArgumentException("F2 数据长度应为 8 字节");
                    break;
                case 0xF4:
                    if (dataLength != 10)
                        throw new ArgumentException("F4 数据长度应为 10 字节");
                    break;
            }
        }

        private void CalculateChecksum(ParsedData data)
        {
            byte sum = 0;

            foreach (var b in data.Header)
                sum += b;

            sum += data.BulletNumber;
            sum += data.Command;
            sum += data.DataLength;

            foreach (var b in data.Data)
                sum += b;

            data.CheckSum = (byte)(sum & 0xFF);
        }

        private void ValidateChecksum(ParsedData parsed, byte[] rawData)
        {
            byte calculated = parsed.CheckSum;
            byte received = rawData[rawData.Length - 1];

            if (calculated != received)
            {
                throw new InvalidDataException(
                    $"校验和错误 (计算值:0x{calculated:X2}, 接收值:0x{received:X2})");
            }
        }
    }

    // 数据处理器接口
    public interface IDataProcessor
    {
        void Process(ParsedData data);
        void Dispose();
    }

    // 数据处理器实现类
    public class DataProcessor : IDataProcessor
    {
        private readonly Action<string> _displayCallback;
        private readonly object _lockObject = new object();

        public DataProcessor(Action<string> displayCallback)
        {
            _displayCallback = displayCallback;
        }

        public void Process(ParsedData data)
        {
            lock (_lockObject)
            {
                try
                {
                    string line = FormatData(data);
                    _displayCallback?.Invoke(line);
                }
                catch (Exception ex)
                {
                    _displayCallback?.Invoke($"数据处理错误: {ex.Message}");
                }
            }
        }

        // 大端序读取方法
        private static short ReadInt16BE(byte[] data, int offset)
        {
            if (offset + 2 > data.Length) throw new IndexOutOfRangeException();
            return (short)((data[offset] << 8) | data[offset + 1]);
        }

        private static int ReadInt32BE(byte[] data, int offset)
        {
            if (offset + 4 > data.Length) throw new IndexOutOfRangeException();
            return (data[offset] << 24) | (data[offset + 1] << 16)
                 | (data[offset + 2] << 8) | data[offset + 3];
        }

        private string FormatData(ParsedData data)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"帧头{BitConverter.ToString(data.Header).Replace("-", " ")} ");
            sb.Append($"弹号{data.BulletNumber:X2} ");
            sb.Append($"命令字{data.Command:X2} ");
            sb.Append($"数据长度{data.DataLength:D} ");

            switch (data.Command)
            {
                case 0xF0:
                    sb.Append("电压:");
                    float voltage = BitConverter.ToUInt16(data.Data, 0) / 100f;
                    sb.Append($"{voltage:F2}V ");

                    sb.Append("温度:");
                    float temperature = BitConverter.ToInt16(data.Data, 2) / 10f;
                    sb.Append($"{temperature:F1}℃ ");

                    sb.Append("状态:");
                    byte status0 = data.Data[4];
                    sb.Append($"定位:{(status0 & 0x03) switch { 0 => "无", 1 => "中", 2 => "好", _ => "异常" }} ");
                    sb.Append($"导引头:{(status0 >> 2 & 0x03) switch { 0 => "无", 1 => "正常", 2 => "异常", _ => "未知" }} ");
                    sb.Append($"类型:{((status0 >> 4 & 0x01) == 0 ? "70D" : "90D")} ");
                    sb.Append($"目标:{((status0 >> 5 & 0x01) == 0 ? "无人机" : "无人船")}");
                    break;

                case 0xF1:
                    sb.Append("飞行时间:");
                    ushort flightTime = (ushort)ReadInt16BE(data.Data, 0);
                    sb.Append($"{flightTime / 10.0:F1}s ");

                    sb.Append("目标经度:");
                    int longitude = ReadInt32BE(data.Data, 2);
                    sb.Append($"{(longitude / 10000000.0):F7}° ");

                    sb.Append("目标纬度:");
                    int latitude = ReadInt32BE(data.Data, 6);
                    sb.Append($"{(latitude / 10000000.0):F7}° ");

                    sb.Append("目标高度:");
                    short altitude = ReadInt16BE(data.Data, 10);
                    sb.Append($"{altitude / 10.0:F1}m ");

                    sb.Append("标志:");
                    byte flag = data.Data[12];
                    sb.Append(flag switch
                    {
                        0 => "无效",
                        1 => "使用GPS定位数据",
                        2 => "使用雷达定位数据",
                        3 => "弹目视线角",
                        _ => "未知"
                    });
                    sb.Append(" ");

                    sb.Append("状态:");
                    byte status1 = data.Data[13];
                    sb.Append(status1 switch
                    {
                        0 => "待机",
                        1 => "垂直爬升",
                        2 => "滚转瞄准",
                        3 => "程序转弯",
                        4 => "巡航飞行",
                        5 => "末制导",
                        6 => "仰头减速",
                        7 => "开伞降落",
                        _ => "未知"
                    });
                    break;

                case 0xF2:
                    sb.Append("二字节");
                    for (int i = 0; i < 2; i++)
                    {
                        sb.Append($"{data.Data[i]:X2} ");
                    }
                    sb.Append("四字节");
                    for (int i = 2; i < 6; i++)
                    {
                        sb.Append($"{data.Data[i]:X2} ");
                    }
                    sb.Append("二字节");
                    for (int i = 6; i < 8; i++)
                    {
                        sb.Append($"{data.Data[i]:X2} ");
                    }
                    break;

                case 0xF4:
                    sb.Append("二字节");
                    for (int i = 0; i < 2; i++)
                    {
                        sb.Append($"{data.Data[i]:X2} ");
                    }
                    sb.Append("四字节");
                    for (int i = 2; i < 6; i++)
                    {
                        sb.Append($"{data.Data[i]:X2} ");
                    }
                    sb.Append("四字节");
                    for (int i = 6; i < 10; i++)
                    {
                        sb.Append($"{data.Data[i]:X2} ");
                    }
                    break;

                default:
                    throw new NotSupportedException($"未知命令: 0x{data.Command:X2}");
            }

            sb.Append($" 校验和{data.CheckSum:X2}");
            return sb.ToString();
        }

        public void Dispose()
        {
            // 无需释放资源
        }
    }

    // 数据解析器接口
    public interface IDataParser
    {
        void Start();
        void Stop();
        void EnqueueData(byte[] data);
        bool IsQueueEmpty { get; }
    }

    public class DataParser : IDataParser
    {
        public bool IsQueueEmpty => _rawDataQueue.IsEmpty;
        private readonly ConcurrentQueue<RawData> _rawDataQueue;
        private readonly IDataProcessor _processor;
        private readonly Thread _processingThread;
        private bool _isRunning;
        private bool _disposed = false;

        public DataParser(Action<string> displayCallback)
        {
            _rawDataQueue = new ConcurrentQueue<RawData>();
            _processor = new DataProcessor(displayCallback);
            _processingThread = new Thread(ProcessingLoop) { IsBackground = true };
        }

        public void Start()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DataParser));

            _isRunning = true;
            _processingThread.Start();
        }

        public void Stop()
        {
            _isRunning = false;
            _processingThread.Join();
        }

        public void EnqueueData(byte[] data)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DataParser));

            _rawDataQueue.Enqueue(new RawData { Data = data });
        }

        private void ProcessingLoop()
        {
            while (_isRunning || !_rawDataQueue.IsEmpty)
            {
                if (_rawDataQueue.TryDequeue(out var rawData))
                {
                    try
                    {
                        var parsedData = Parse(rawData);
                        _processor.Process(parsedData);
                    }
                    catch (Exception ex)
                    {
                        _processor.Process(new ParsedData
                        {
                            Command = 0xFF,
                            Data = Encoding.UTF8.GetBytes($"解析错误: {ex.Message}")
                        });
                    }
                }
                else
                {
                    Thread.Sleep(50);
                }
            }
        }

        private ParsedData Parse(RawData rawData)
        {
            byte command = rawData.Data[3];
            IParseStrategy strategy = GetParseStrategy(command);
            return strategy.Parse(rawData);
        }

        private IParseStrategy GetParseStrategy(byte command)
        {
            switch (command)
            {
                case 0xF0:
                case 0xF1:
                case 0xF2:
                case 0xF4:
                    return new ParseStrategy();
                default:
                    throw new NotSupportedException($"未知命令: 0x{command:X2}");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _isRunning = false;
                    _processingThread?.Join();
                    _processor?.Dispose();
                }
                _disposed = true;
            }
        }
    }

    public class SerialPortReceiver : IDisposable
    {
        private readonly SerialPort _serialPort;
        private readonly IDataParser _dataParser;
        private bool _isReceiving;
        private readonly FrameBuffer _frameBuffer;
        private readonly object _lock = new object();

        public SerialPortReceiver(string portName, int baudRate, IDataParser dataParser)
        {
            if (string.IsNullOrWhiteSpace(portName))
                throw new ArgumentException("串口名称不能为空");
            if (baudRate < 1200 || baudRate > 115200)
                throw new ArgumentException("波特率无效");
            if (dataParser == null)
                throw new ArgumentNullException(nameof(dataParser));

            _serialPort = new SerialPort(portName, baudRate)
            {
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                ReadTimeout = 500,
                WriteTimeout = 500
            };

            _dataParser = dataParser;
            _frameBuffer = new FrameBuffer(new byte[] { 0xBB, 0xBF });
            _serialPort.DataReceived += SerialPort_DataReceived;
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_isReceiving) return;

                try
                {
                    if (!_serialPort.IsOpen)
                    {
                        _serialPort.Open();
                    }
                    _isReceiving = true;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"无法打开串口: {ex.Message}");
                }
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (!_isReceiving) return;

                _isReceiving = false;
                try
                {
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.Close();
                    }
                }
                catch (Exception)
                {
                    // 忽略关闭错误
                }
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (!_isReceiving || _serialPort.BytesToRead == 0)
                return;

            lock (_lock)
            {
                try
                {
                    byte[] buffer = new byte[_serialPort.BytesToRead];
                    int bytesRead = _serialPort.Read(buffer, 0, buffer.Length);

                    foreach (var frame in _frameBuffer.ProcessBytes(buffer))
                    {
                        _dataParser.EnqueueData(frame);
                    }
                }
                catch (TimeoutException)
                {
                    // 忽略超时
                }
                catch (Exception ex)
                {
                    _dataParser.EnqueueData(Encoding.UTF8.GetBytes($"串口错误: {ex.Message}"));
                }
            }
        }

        public void Dispose()
        {
            Stop();
            _serialPort?.Dispose();
        }
    }

    public class FrameBuffer
    {
        private readonly List<byte> _buffer = new List<byte>();
        private readonly byte[] _header;
        private readonly object _lock = new object();

        public FrameBuffer(byte[] header)
        {
            if (header == null || header.Length == 0)
                throw new ArgumentException("帧头不能为空");
            _header = header;
        }

        public IEnumerable<byte[]> ProcessBytes(byte[] newData)
        {
            lock (_lock)
            {
                _buffer.AddRange(newData);

                while (true)
                {
                    int frameStart = FindFrameStart();
                    if (frameStart < 0) break;

                    if (frameStart + 5 > _buffer.Count)
                    {
                        break;
                    }

                    byte dataLength = _buffer[frameStart + 4];
                    int expectedLength = 5 + dataLength + 1;

                    if (frameStart + expectedLength > _buffer.Count)
                    {
                        break;
                    }

                    byte[] frame = new byte[expectedLength];
                    _buffer.CopyTo(frameStart, frame, 0, expectedLength);

                    _buffer.RemoveRange(0, frameStart + expectedLength);
                    yield return frame;
                }

                int maxHeaderLength = _header.Length;
                int bytesToKeep = Math.Min(_buffer.Count, maxHeaderLength - 1);
                if (_buffer.Count > bytesToKeep)
                {
                    byte[] remaining = _buffer.Skip(_buffer.Count - bytesToKeep).ToArray();
                    _buffer.Clear();
                    _buffer.AddRange(remaining);
                }
            }
        }

        private int FindFrameStart()
        {
            for (int i = 0; i <= _buffer.Count - _header.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < _header.Length; j++)
                {
                    if (_buffer[i + j] != _header[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return i;
            }
            return -1;
        }
    }
}