using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Forms;

namespace ImageDragApp
{
    // 定义布局信息类，用于序列化和反序列化
    [Serializable]
    public class LayoutInfo
    {
        public List<PictureBoxInfo> PictureBoxes { get; set; } = new List<PictureBoxInfo>();
        public string ImagesDirectory { get; set; }
    }

    // 存储单个图片框的信息
    [Serializable]
    public class PictureBoxInfo
    {
        public string ImagePath { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int LocationX { get; set; }
        public int LocationY { get; set; }
    }

    public partial class MainForm : Form
    {
        private Panel targetPanel;
        private Panel sourcePanel;
        private Button resetButton;
        private Button adaptiveButton;
        private List<PictureBox> sourcePictureBoxes = new List<PictureBox>();
        private List<PictureBox> targetPictureBoxes = new List<PictureBox>();
        private Dictionary<string, Image> loadedImages = new Dictionary<string, Image>();
        private Dictionary<PictureBox, Size> originalPictureSizes = new Dictionary<PictureBox, Size>();
        private FlowLayoutPanel targetFlowLayoutPanel;
        private FlowLayoutPanel sourceFlowLayoutPanel;
        private bool isAdaptiveMode = false;
        private Timer animationTimer;
        private DateTime animationStartTime;
        private const float ANIMATION_DURATION = 100;
        private TextBox imagesPathTextBox;

        // 布局文件路径
        private string layoutFilePath;
        private bool isUserClosing = true;
        public MainForm()
        {
            InitializeComponent();
            InitializeUI(); // 先初始化UI
            InitializeAnimationTimer();
            LoadSourceImages();

            // 设置布局文件路径
            layoutFilePath = Path.Combine(Application.StartupPath, "layout.dat");

            this.SizeChanged += MainForm_SizeChanged;
            this.FormClosing += MainForm_FormClosing; // 添加窗体关闭事件处理

            // 程序加载时尝试加载布局
            LoadLayout();
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "选择图片文件夹";
                folderDialog.SelectedPath = imagesPathTextBox.Text;

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    imagesPathTextBox.Text = folderDialog.SelectedPath;
                    LoadSourceImages();
                }
            }
        }

        private void InitializeUI()
        {
            // 设置窗体最小大小
            this.MinimumSize = new Size(600, 600);

            // 创建图片文件夹路径文本框
            // 修复：直接初始化类成员变量，移除局部变量声明
            imagesPathTextBox = new TextBox();
            imagesPathTextBox.Name = "imagesPathTextBox";
            imagesPathTextBox.Location = new Point(10, 315);
            imagesPathTextBox.Size = new Size(200, 25);
            imagesPathTextBox.ReadOnly = true;
            imagesPathTextBox.Text = Path.Combine(Application.StartupPath, "Images"); // 默认路径

            // 创建浏览按钮
            Button browseButton = new Button();
            browseButton.Name = "browseButton";
            browseButton.Text = "浏览...";
            browseButton.Location = new Point(220, 315);
            browseButton.Size = new Size(75, 25);
            browseButton.Click += BrowseButton_Click;

            // 添加到窗体
            this.Controls.Add(imagesPathTextBox);
            this.Controls.Add(browseButton);

            // 创建上部目标区域
            targetPanel = new Panel();
            targetPanel.Name = "targetPanel";
            targetPanel.Location = new Point(10, 10);
            targetPanel.Size = new Size(this.ClientSize.Width - 20, 300);
            targetPanel.BorderStyle = BorderStyle.FixedSingle;
            targetPanel.AllowDrop = true;
            targetPanel.BackColor = Color.WhiteSmoke;
            targetPanel.DragEnter += TargetPanel_DragEnter;
            targetPanel.DragDrop += TargetPanel_DragDrop;
            targetPanel.Paint += TargetPanel_Paint;
            targetPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; // 横向拉伸

            // 创建FlowLayoutPanel用于排列多个图片
            targetFlowLayoutPanel = new FlowLayoutPanel();
            targetFlowLayoutPanel.Name = "targetFlowLayoutPanel";
            targetFlowLayoutPanel.Dock = DockStyle.Fill;
            targetFlowLayoutPanel.FlowDirection = FlowDirection.LeftToRight;
            targetFlowLayoutPanel.WrapContents = true;
            targetFlowLayoutPanel.AutoScroll = true;

            targetPanel.Controls.Add(targetFlowLayoutPanel);

            // 创建保存布局按钮
            Button saveLayoutButton = new Button();
            saveLayoutButton.Name = "saveLayoutButton";
            saveLayoutButton.Text = "保存布局";
            saveLayoutButton.Location = new Point(this.ClientSize.Width - 280, 315);
            saveLayoutButton.Size = new Size(80, 30);
            saveLayoutButton.Click += SaveLayoutButton_Click;
            saveLayoutButton.Anchor = AnchorStyles.Top | AnchorStyles.Right; // 固定在右上角

            // 创建重置按钮
            resetButton = new Button();
            resetButton.Name = "resetButton";
            resetButton.Text = "重置";
            resetButton.Location = new Point(this.ClientSize.Width - 190, 315);
            resetButton.Size = new Size(80, 30);
            resetButton.Click += ResetButton_Click;
            resetButton.Anchor = AnchorStyles.Top | AnchorStyles.Right; // 固定在右上角

            // 创建自适应按钮
            adaptiveButton = new Button();
            adaptiveButton.Name = "adaptiveButton";
            adaptiveButton.Text = "自适应";
            adaptiveButton.Location = new Point(this.ClientSize.Width - 100, 315);
            adaptiveButton.Size = new Size(80, 30);
            adaptiveButton.BackColor = Color.Gray;
            adaptiveButton.Click += AdaptiveButton_Click;
            adaptiveButton.Anchor = AnchorStyles.Top | AnchorStyles.Right; // 固定在右上角

            // 创建下部源图片区域
            sourcePanel = new Panel();
            sourcePanel.Name = "sourcePanel";
            sourcePanel.Location = new Point(10, 350);
            sourcePanel.Size = new Size(this.ClientSize.Width - 20, 240); // 调整高度
            sourcePanel.BorderStyle = BorderStyle.FixedSingle;
            sourcePanel.BackColor = Color.LightGray;
            sourcePanel.AutoScroll = true;
            sourcePanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom; // 四个方向拉伸

            // 创建FlowLayoutPanel用于排列源图片
            sourceFlowLayoutPanel = new FlowLayoutPanel();
            sourceFlowLayoutPanel.Name = "sourceFlowLayoutPanel";
            sourceFlowLayoutPanel.Dock = DockStyle.Fill;
            sourceFlowLayoutPanel.FlowDirection = FlowDirection.LeftToRight;
            sourceFlowLayoutPanel.WrapContents = true;
            sourceFlowLayoutPanel.AutoScroll = true;

            sourcePanel.Controls.Add(sourceFlowLayoutPanel);

            // 添加到窗体
            this.Controls.Add(targetPanel);
            this.Controls.Add(saveLayoutButton);
            this.Controls.Add(resetButton);
            this.Controls.Add(adaptiveButton);
            this.Controls.Add(sourcePanel);
        }

        private void MainForm_SizeChanged(object sender, EventArgs e)
        {
            // 调整目标面板宽度
            targetPanel.Width = this.ClientSize.Width - 20;

            // 调整源面板位置和大小
            sourcePanel.Location = new Point(10, 350);
            sourcePanel.Width = this.ClientSize.Width - 20;
            sourcePanel.Height = this.ClientSize.Height - 360;

            // 调整按钮位置
            resetButton.Location = new Point(this.ClientSize.Width - 190, 315);
            adaptiveButton.Location = new Point(this.ClientSize.Width - 100, 315);

            // 添加保存布局按钮位置调整
            Button saveLayoutButton = this.Controls.OfType<Button>().FirstOrDefault(b => b.Name == "saveLayoutButton");
            if (saveLayoutButton != null)
            {
                saveLayoutButton.Location = new Point(this.ClientSize.Width - 280, 315);
            }

            // 刷新布局
            targetPanel.Invalidate();
            sourcePanel.Invalidate();
        }

        private void InitializeAnimationTimer()
        {
            animationTimer = new Timer();
            animationTimer.Interval = 10; // 保持适当的更新频率
            animationTimer.Tick += AnimationTimer_Tick;
        }

        private void AdaptiveButton_Click(object sender, EventArgs e)
        {
            isAdaptiveMode = !isAdaptiveMode;

            // 更改按钮外观
            adaptiveButton.BackColor = isAdaptiveMode ? Color.Black : Color.Gray;
            adaptiveButton.ForeColor = isAdaptiveMode ? Color.White : Color.Black;

            // 保存当前所有图片的原始尺寸
            foreach (PictureBox pb in targetPictureBoxes)
            {
                if (!originalPictureSizes.ContainsKey(pb))
                {
                    originalPictureSizes[pb] = pb.Size;
                }
            }

            // 记录动画开始时间
            animationStartTime = DateTime.Now;

            // 开始动画
            animationTimer.Start();
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            // 计算动画进度（0-1之间）
            float elapsed = (float)(DateTime.Now - animationStartTime).TotalMilliseconds;
            float progress = Math.Min(elapsed / ANIMATION_DURATION, 1.0f);

            foreach (PictureBox pb in targetPictureBoxes)
            {
                // 确保图片有原始尺寸记录
                if (!originalPictureSizes.ContainsKey(pb))
                {
                    originalPictureSizes[pb] = pb.Size;
                }

                Size startSize = isAdaptiveMode ? originalPictureSizes[pb] : new Size(120, 120);
                Size endSize = isAdaptiveMode ? new Size(120, 120) : originalPictureSizes[pb];

                // 线性插值计算当前尺寸
                int currentWidth = (int)(startSize.Width + (endSize.Width - startSize.Width) * progress);
                int currentHeight = (int)(startSize.Height + (endSize.Height - startSize.Height) * progress);

                pb.Size = new Size(currentWidth, currentHeight);
            }

            // 动画完成
            if (progress >= 1.0f)
            {
                animationTimer.Stop();
            }
        }

        private void TargetPanel_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("ImagePath"))
            {
                string imagePath = e.Data.GetData("ImagePath") as string;

                if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                {
                    try
                    {
                        // 加载并显示图片
                        Image image = LoadImage(imagePath);

                        if (image != null)
                        {
                            // 计算适合面板的图片大小
                            Size displaySize = isAdaptiveMode ?
                                new Size(120, 120) : // 自适应模式使用固定尺寸
                                CalculateDisplaySize(image.Size, targetPanel.Size); // 原始模式使用计算尺寸

                            PictureBox targetPictureBox = new PictureBox();
                            targetPictureBox.Name = "targetPictureBox_" + Guid.NewGuid().ToString();
                            targetPictureBox.Size = displaySize;
                            targetPictureBox.BorderStyle = BorderStyle.FixedSingle;
                            targetPictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
                            targetPictureBox.Image = image;
                            targetPictureBox.Tag = imagePath;

                            // 添加右键菜单删除功能
                            ContextMenuStrip contextMenu = new ContextMenuStrip();
                            ToolStripMenuItem deleteItem = new ToolStripMenuItem("删除");
                            deleteItem.Click += (s, args) => RemovePictureBox(targetPictureBox);
                            contextMenu.Items.Add(deleteItem);
                            targetPictureBox.ContextMenuStrip = contextMenu;

                            // 添加到FlowLayoutPanel
                            targetFlowLayoutPanel.Controls.Add(targetPictureBox);
                            targetPictureBoxes.Add(targetPictureBox);

                            // 保存原始尺寸
                            originalPictureSizes[targetPictureBox] = displaySize;

                            // 刷新面板
                            targetPanel.Invalidate();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"放置图片时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private Size CalculateDisplaySize(Size originalSize, Size containerSize)
        {
            // 计算适合面板的图片大小，保持纵横比
            float widthRatio = (float)containerSize.Width / originalSize.Width;
            float heightRatio = (float)containerSize.Height / originalSize.Height;
            float ratio = Math.Min(widthRatio, heightRatio);

            // 限制最大尺寸，避免过大
            ratio = Math.Min(ratio, 0.5f);

            return new Size(
                (int)(originalSize.Width * ratio),
                (int)(originalSize.Height * ratio));
        }

        private void RemovePictureBox(PictureBox pictureBox)
        {
            targetFlowLayoutPanel.Controls.Remove(pictureBox);
            targetPictureBoxes.Remove(pictureBox);
            originalPictureSizes.Remove(pictureBox); // 从字典中移除
            targetPanel.Invalidate();
        }
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 如果是用户主动关闭，并且目标面板中有图片，则询问是否保存
            if (isUserClosing && targetPictureBoxes.Count > 0)
            {
                DialogResult result = MessageBox.Show(
                    "是否保存当前布局？",
                    "退出确认",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                switch (result)
                {
                    case DialogResult.Yes:
                        SaveLayout();
                        break;
                    case DialogResult.Cancel:
                        e.Cancel = true; // 取消关闭操作
                        return;
                    case DialogResult.No:
                        // 不保存，直接退出
                        break;
                }
            }
        }
        private void ResetButton_Click(object sender, EventArgs e)
        {
            // 清除所有目标图片
            isUserClosing = false; // 标记不是用户主动关闭
            targetFlowLayoutPanel.Controls.Clear();
            targetPictureBoxes.Clear();
            originalPictureSizes.Clear(); // 清除保存的原始尺寸
            targetPanel.Invalidate();
            isUserClosing = true; // 恢复标记
        }

        private void LoadSourceImages()
        {

            string imagesDirectory = imagesPathTextBox.Text;

            // 清空现有图片
            sourceFlowLayoutPanel.Controls.Clear();
            sourcePictureBoxes.Clear();

            if (!Directory.Exists(imagesDirectory))
            {
                MessageBox.Show($"图片文件夹不存在: {imagesDirectory}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string[] imageFiles = Directory.GetFiles(imagesDirectory, "*.jpg")
                .Concat(Directory.GetFiles(imagesDirectory, "*.jpeg"))
                .Concat(Directory.GetFiles(imagesDirectory, "*.png"))
                .Concat(Directory.GetFiles(imagesDirectory, "*.bmp"))
                .ToArray();

            if (imageFiles.Length == 0)
            {
                MessageBox.Show($"在图片文件夹中未找到图片: {imagesDirectory}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            foreach (string imagePath in imageFiles)
            {
                try
                {
                    string imageName = Path.GetFileNameWithoutExtension(imagePath);

                    // 加载并缓存图像
                    Image image = LoadImage(imagePath);

                    if (image == null) continue;

                    // 创建包含图片和标签的面板
                    Panel imageContainer = new Panel();
                    imageContainer.Size = new Size(120, 150); // 图片+标签的总高度
                    imageContainer.BackColor = Color.Transparent;

                    // 创建图片控件
                    PictureBox pictureBox = new PictureBox();
                    pictureBox.Name = "pb_" + imageName;
                    pictureBox.Size = new Size(120, 120);
                    pictureBox.BorderStyle = BorderStyle.FixedSingle;
                    pictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
                    pictureBox.Cursor = Cursors.Hand;
                    pictureBox.Image = image;
                    pictureBox.Tag = imagePath;
                    pictureBox.Location = new Point(0, 0);

                    // 设置拖放事件
                    pictureBox.MouseDown += SourcePictureBox_MouseDown;

                    // 添加图片到容器
                    imageContainer.Controls.Add(pictureBox);

                    // 添加图片名称标签
                    Label label = new Label();
                    label.Text = imageName;
                    label.Location = new Point(0, 125);
                    label.Size = new Size(120, 20);
                    label.TextAlign = ContentAlignment.MiddleCenter;
                    imageContainer.Controls.Add(label);

                    // 添加容器到FlowLayoutPanel
                    sourceFlowLayoutPanel.Controls.Add(imageContainer);
                    sourcePictureBoxes.Add(pictureBox);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"加载图片时出错: {imagePath}\n{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private Image LoadImage(string imagePath)
        {
            if (loadedImages.ContainsKey(imagePath))
            {
                return loadedImages[imagePath];
            }

            try
            {
                // 使用流加载图像，避免锁定文件
                using (FileStream fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                {
                    Image image = Image.FromStream(fs);
                    loadedImages.Add(imagePath, image);
                    return image;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法加载图片: {imagePath}\n{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        private void SourcePictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && sender is PictureBox pictureBox)
            {
                string imagePath = pictureBox.Tag as string;
                if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                {
                    DataObject data = new DataObject();
                    data.SetData("ImagePath", imagePath);
                    pictureBox.DoDragDrop(data, DragDropEffects.Copy);
                }
            }
        }

        private void TargetPanel_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("ImagePath"))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void TargetPanel_Paint(object sender, PaintEventArgs e)
        {
            if (targetFlowLayoutPanel.Controls.Count == 0)
            {
                // 绘制提示文本
                string text = "拖放图片到此处";
                Font font = new Font("Arial", 14, FontStyle.Bold);
                Brush brush = new SolidBrush(Color.Gray);
                StringFormat format = new StringFormat();
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;

                e.Graphics.DrawString(text, font, brush, targetPanel.ClientRectangle, format);
            }
        }

        // 保存布局按钮点击事件
        private void SaveLayoutButton_Click(object sender, EventArgs e)
        {
            SaveLayout();
        }

        // 保存布局到文件
        private void SaveLayout()
        {
            try
            {
                LayoutInfo layoutInfo = new LayoutInfo();

                foreach (PictureBox pb in targetPictureBoxes)
                {
                    layoutInfo.PictureBoxes.Add(new PictureBoxInfo
                    {
                        ImagePath = pb.Tag as string,
                        Width = pb.Width,
                        Height = pb.Height,
                        LocationX = pb.Location.X,
                        LocationY = pb.Location.Y
                    });
                }

                using (FileStream fs = new FileStream(layoutFilePath, FileMode.Create))
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    formatter.Serialize(fs, layoutInfo);
                }

                MessageBox.Show("布局已保存", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存布局时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 从文件加载布局
        private void LoadLayout()
        {
            if (File.Exists(layoutFilePath))
            {
                try
                {
                    using (FileStream fs = new FileStream(layoutFilePath, FileMode.Open))
                    {
                        BinaryFormatter formatter = new BinaryFormatter();
                        LayoutInfo layoutInfo = (LayoutInfo)formatter.Deserialize(fs);

                        foreach (var info in layoutInfo.PictureBoxes)
                        {
                            if (File.Exists(info.ImagePath))
                            {
                                Image image = LoadImage(info.ImagePath);
                                if (image != null)
                                {
                                    PictureBox targetPictureBox = new PictureBox();
                                    targetPictureBox.Name = "targetPictureBox_" + Guid.NewGuid().ToString();
                                    targetPictureBox.Size = new Size(info.Width, info.Height);
                                    targetPictureBox.Location = new Point(info.LocationX, info.LocationY);
                                    targetPictureBox.BorderStyle = BorderStyle.FixedSingle;
                                    targetPictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
                                    targetPictureBox.Image = image;
                                    targetPictureBox.Tag = info.ImagePath;

                                    // 添加右键菜单删除功能
                                    ContextMenuStrip contextMenu = new ContextMenuStrip();
                                    ToolStripMenuItem deleteItem = new ToolStripMenuItem("删除");
                                    deleteItem.Click += (s, args) => RemovePictureBox(targetPictureBox);
                                    contextMenu.Items.Add(deleteItem);
                                    targetPictureBox.ContextMenuStrip = contextMenu;

                                    // 添加到FlowLayoutPanel
                                    targetFlowLayoutPanel.Controls.Add(targetPictureBox);
                                    targetPictureBoxes.Add(targetPictureBox);

                                    // 保存原始尺寸
                                    originalPictureSizes[targetPictureBox] = targetPictureBox.Size;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"加载布局时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    } // MainForm类结束
} // 命名空间结束