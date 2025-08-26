using System;
using System.Windows.Forms;
using System.Threading;

namespace SerialDataDisplay
{
    public partial class MainForm : Form
    {
        private DataParser _parser;
        private SerialPortReceiver _serialReceiver;
        private readonly object _displayLock = new object();
        private string _lastDisplayData = "";
        private System.Windows.Forms.Timer _displayTimer;
        private TextBox _displayTextBox;
        private StatusStrip _statusStrip;
        private ToolStrip _toolStrip;
        private ToolStripStatusLabel _statusLabel;

        public MainForm()
        {
            InitializeComponent();
            InitializeUI();
            InitializeDataComponents();
        }

        private void InitializeUI()
        {
            // 窗体设置
            this.Text = "串口数据实时显示";
            this.Size = new System.Drawing.Size(900, 600);

            // 主显示文本框
            _displayTextBox = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = new System.Drawing.Font("Consolas", 10),
                BackColor = System.Drawing.Color.Black,
                ForeColor = System.Drawing.Color.Lime
            };
            this.Controls.Add(_displayTextBox);

            // 底部状态栏
            _statusStrip = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel { Text = "准备就绪" };
            _statusStrip.Items.Add(_statusLabel);
            this.Controls.Add(_statusStrip);
            _statusStrip.Dock = DockStyle.Bottom;

            // 顶部工具栏
            _toolStrip = new ToolStrip();
            var startButton = new ToolStripButton("开始接收");
            var stopButton = new ToolStripButton("停止接收");
            var clearButton = new ToolStripButton("清空显示");

            startButton.Click += (s, e) => StartReceiving();
            stopButton.Click += (s, e) => StopReceiving();
            clearButton.Click += (s, e) => _displayTextBox.Clear();

            _toolStrip.Items.Add(startButton);
            _toolStrip.Items.Add(stopButton);
            _toolStrip.Items.Add(clearButton);
            this.Controls.Add(_toolStrip);
            _toolStrip.Dock = DockStyle.Top;

            // 显示更新定时器
            _displayTimer = new System.Windows.Forms.Timer { Interval = 100 };
            _displayTimer.Tick += DisplayTimer_Tick;
            _displayTimer.Start();
        }

        private void DisplayTimer_Tick(object sender, EventArgs e)
        {
            lock (_displayLock)
            {
                if (!string.IsNullOrEmpty(_lastDisplayData))
                {
                    _displayTextBox.AppendText(_lastDisplayData + Environment.NewLine);
                    _lastDisplayData = "";
                    // 自动滚动到最后
                    _displayTextBox.SelectionStart = _displayTextBox.Text.Length;
                    _displayTextBox.ScrollToCaret();
                }
            }
        }

        private void InitializeDataComponents()
        {
            // 初始化数据解析器和串口接收器
            _parser = new DataParser(UpdateDisplay);
            _serialReceiver = new SerialPortReceiver("COM4", 9600, _parser);
        }

        private void StartReceiving()
        {
            try
            {
                _parser.Start();
                _serialReceiver.Start();
                _statusLabel.Text = "正在接收数据...";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"启动失败: {ex.Message}";
            }
        }

        private void StopReceiving()
        {
            try
            {
                _serialReceiver.Stop();
                _statusLabel.Text = "已停止接收";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"停止失败: {ex.Message}";
            }
        }

        private void UpdateDisplay(string data)
        {
            lock (_displayLock)
            {
                _lastDisplayData = data;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            // 停止接收和处理
            StopReceiving();

            // 等待处理队列完成
            int waitTime = 0;
            while (!_parser.IsQueueEmpty && waitTime < 3000)
            {
                Thread.Sleep(100);
                waitTime += 100;
            }

            _parser.Dispose();
            _serialReceiver.Dispose();
            _displayTimer.Stop();
        }

        private void InitializeComponent()
        {
            // 空实现，已删除设计器生成的控件初始化
        }
    }
}