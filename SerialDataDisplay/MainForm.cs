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
        private TextBox displayTextBox;
        private System.Windows.Forms.Timer displayTimer;
        private System.ComponentModel.IContainer components;
        private ToolStrip toolStrip;
        private ToolStripButton startButton;
        private ToolStripButton stopButton;
        private ToolStripButton clearButton;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel statusLabel;
        private string _lastDisplayData = "";

        public MainForm()
        {
            InitializeComponent(); 
            InitializeDataComponents();

            startButton.Click += (s, e) => StartReceiving();
            stopButton.Click += (s, e) => StopReceiving();
            clearButton.Click += (s, e) => displayTextBox.Clear();

            displayTimer.Interval = 100;
            displayTimer.Tick += DisplayTimer_Tick;
            displayTimer.Start();

            statusLabel.Text = "准备就绪"; 
        }

        private void DisplayTimer_Tick(object sender, EventArgs e)
        {
            lock (_displayLock)
            {
                if (!string.IsNullOrEmpty(_lastDisplayData))
                {
                    displayTextBox.AppendText(_lastDisplayData + Environment.NewLine);
                    _lastDisplayData = "";
                    displayTextBox.ScrollToCaret(); 
                }
            }
        }

        private void InitializeDataComponents()
        {
            _parser = new DataParser(UpdateDisplay);
            _serialReceiver = new SerialPortReceiver("COM4", 9600, _parser);
        }

        private void StartReceiving()
        {
            try
            {
                _parser.Start();
                _serialReceiver.Start();
                statusLabel.Text = "正在接收数据...";
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"启动失败: {ex.Message}";
            }
        }

        private void StopReceiving()
        {
            try
            {
                _serialReceiver.Stop();
                statusLabel.Text = "已停止接收";
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"停止失败: {ex.Message}";
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
            StopReceiving();

            int waitTime = 0;
            while (!_parser.IsQueueEmpty && waitTime < 3000)
            {
                Thread.Sleep(100);
                waitTime += 100;
            }

            _parser.Dispose();
            _serialReceiver.Dispose();
            displayTimer.Stop();
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.displayTextBox = new System.Windows.Forms.TextBox();
            this.displayTimer = new System.Windows.Forms.Timer(this.components);
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.startButton = new System.Windows.Forms.ToolStripButton();
            this.stopButton = new System.Windows.Forms.ToolStripButton();
            this.clearButton = new System.Windows.Forms.ToolStripButton();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStrip.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // displayTextBox
            // 
            this.displayTextBox.BackColor = System.Drawing.Color.Black;
            this.displayTextBox.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.displayTextBox.ForeColor = System.Drawing.Color.White;
            this.displayTextBox.Location = new System.Drawing.Point(0, 28);
            this.displayTextBox.Multiline = true;
            this.displayTextBox.Name = "displayTextBox";
            this.displayTextBox.ReadOnly = true;
            this.displayTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.displayTextBox.Size = new System.Drawing.Size(884, 508);
            this.displayTextBox.TabIndex = 0;
            // 
            // displayTimer
            // 
            this.displayTimer.Enabled = true;
            // 
            // toolStrip
            // 
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.startButton,
            this.stopButton,
            this.clearButton});
            this.toolStrip.Location = new System.Drawing.Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size(884, 25);
            this.toolStrip.Stretch = true;
            this.toolStrip.TabIndex = 1;
            this.toolStrip.Text = "toolStrip1";
            // 
            // startButton
            // 
            this.startButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.startButton.Image = ((System.Drawing.Image)(resources.GetObject("startButton.Image")));
            this.startButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.startButton.Name = "startButton";
            this.startButton.Size = new System.Drawing.Size(60, 22);
            this.startButton.Text = "开始接收";
            this.startButton.ToolTipText = "开始接收串口数据";
            // 
            // stopButton
            // 
            this.stopButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.stopButton.Image = ((System.Drawing.Image)(resources.GetObject("stopButton.Image")));
            this.stopButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.stopButton.Name = "stopButton";
            this.stopButton.Size = new System.Drawing.Size(60, 22);
            this.stopButton.Text = "停止接收";
            this.stopButton.ToolTipText = "停止接收串口数据";
            // 
            // clearButton
            // 
            this.clearButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.clearButton.Image = ((System.Drawing.Image)(resources.GetObject("clearButton.Image")));
            this.clearButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.clearButton.Name = "clearButton";
            this.clearButton.Size = new System.Drawing.Size(60, 22);
            this.clearButton.Text = "清空显示";
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusLabel});
            this.statusStrip1.Location = new System.Drawing.Point(0, 539);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(884, 22);
            this.statusStrip1.TabIndex = 2;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // statusLabel
            // 
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(56, 17);
            this.statusLabel.Text = "准备就绪";
            // 
            // MainForm
            // 
            this.ClientSize = new System.Drawing.Size(884, 561);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.toolStrip);
            this.Controls.Add(this.displayTextBox);
            this.Name = "MainForm";
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }
    }
}