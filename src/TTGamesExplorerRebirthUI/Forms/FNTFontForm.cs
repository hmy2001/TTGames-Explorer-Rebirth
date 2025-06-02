using DarkUI.Forms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Drawing.Drawing2D;
using TTGamesExplorerRebirthLib.Formats;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Menu;
using Color = SixLabors.ImageSharp.Color;
using RectangleF = SixLabors.ImageSharp.RectangleF;

namespace TTGamesExplorerRebirthUI.Forms
{
    public partial class FNTFontForm : DarkForm
    {
        private readonly string _filePath;
        private readonly FNT _fontFile;

        private System.Drawing.Image _previewImage;
        private int _previewWidth;
        private int _previewHeight;
        private int _zoomVal = 100;

        public FNTFontForm(string filePath, byte[] fileBuffer)
        {
            InitializeComponent();

            _filePath = filePath;
            _fontFile = (fileBuffer != null) ? new(fileBuffer) : new(filePath);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            Helper.EnableDarkModeTitle(Handle);
        }

        private void FontForm_Load(object sender, EventArgs e)
        {
            toolStripStatusLabel1.Text = Path.GetFileName(_filePath);
            
            darkComboBox1.Items.Add("None");
            
            for (int i = 1; i <= _fontFile.UnicodeTables.Length; i++)
            {
                FNTUnicode unicode = _fontFile.UnicodeTables[i - 1];
                darkComboBox1.Items.Add($"Char #" + i + " [" + unicode.UnicodeChar + "]");
            }

            darkComboBox1.SelectedItem = darkComboBox1.Items[0];
            darkComboBox1.SelectedValueChanged += DarkComboBox1_SelectedValueChanged;

            darkCheckBox1.CheckedChanged += DarkCheckBox1_CheckedChanged;
            darkCheckBox1.Checked = true;
        }

        private void DarkCheckBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (darkCheckBox1.Checked)
            {
                darkComboBox1.Enabled = false;
                darkComboBox1.SelectedItem = null;

                SixLabors.ImageSharp.Image image = _fontFile.FontImage.Images[0].CloneAs<Rgba32>();

                for (int i = 0; i < _fontFile.UnicodeTables.Length; i++)
                {
                    uint indexChar = _fontFile.UnicodeTables[i].FontMappingIndex;
                    if(_fontFile.CharMappingTables.Length > indexChar){
                        FNTCharMapping charMapping = _fontFile.CharMappingTables[indexChar];
                        RectangleF rect = new()
                        {
                            X = charMapping.X,
                            Y = charMapping.Y,
                            Width = charMapping.Width,
                            Height = charMapping.Height,
                        };
                        
                        image.Mutate(x => x.Fill(Color.FromRgba(255, 0, 0, 120), rect));
                    }
                }

                using MemoryStream stream = new();

                image.Save(stream, PngFormat.Instance);

                _previewImage = new Bitmap(stream);
                _previewWidth = image.Width;
                _previewHeight = image.Height;

                pictureBox1.Image = PictureBoxZoom(_previewImage, new System.Drawing.Size(_previewHeight * _zoomVal / 100, _previewWidth * _zoomVal / 100));
            }
            else
            {
                darkComboBox1.Enabled = true;
                darkComboBox1.SelectedItem = darkComboBox1.Items[0];
            }
        }

        private void DarkComboBox1_SelectedValueChanged(object sender, EventArgs e)
        {
            if (!darkCheckBox1.Checked)
            {
                using MemoryStream stream = new();

                SixLabors.ImageSharp.Image image = _fontFile.FontImage.Images[0].CloneAs<Rgba32>();
                
                if (darkComboBox1.SelectedIndex != 0)
                {
                    int indexChar = _fontFile.UnicodeTables[darkComboBox1.SelectedIndex - 1].FontMappingIndex;

                    if(_fontFile.CharMappingTables.Length > indexChar){
                        RectangleF rect = new()
                        {
                            X = _fontFile.CharMappingTables[indexChar].X,
                            Y = _fontFile.CharMappingTables[indexChar].Y,
                            Width = _fontFile.CharMappingTables[indexChar].Width,
                            Height = _fontFile.CharMappingTables[indexChar].Height,
                        };

                        image.Mutate(x => x.Fill(Color.FromRgba(255, 0, 0, 120), rect));
                    }
                }
                
                image.Save(stream, PngFormat.Instance);

                _previewImage = new Bitmap(stream);
                _previewWidth = image.Width;
                _previewHeight = image.Height;
                
                pictureBox1.Image = PictureBoxZoom(_previewImage, new System.Drawing.Size(_previewHeight * _zoomVal / 100, _previewWidth * _zoomVal / 100));
            }
        }

        private void TrackBar1_Scroll(object sender, EventArgs e)
        {
            if (trackBar1.Value <= 0)
            {
                return;
            }

            _zoomVal = trackBar1.Value;

            darkLabel1.Text = $"{_zoomVal}%";

            pictureBox1.Image = PictureBoxZoom(_previewImage, new System.Drawing.Size(_previewHeight * _zoomVal / 100, _previewWidth * _zoomVal / 100));
        }

        public static System.Drawing.Image PictureBoxZoom(System.Drawing.Image img, System.Drawing.Size size)
        {
            Bitmap bitmap = new(img, size.Width <= 0 ? 1 : size.Width, size.Height <= 0 ? 1 : size.Height);

            Graphics.FromImage(bitmap).InterpolationMode = InterpolationMode.HighQualityBilinear;

            return bitmap;
        }

        private void DarkButton2_Click(object sender, EventArgs e)
        {
            saveFileDialog1.Filter = "DirectDraw Surface files (*.dds)|*.dds";
            saveFileDialog1.DefaultExt = "dds";
            saveFileDialog1.Title = "Save as DDS...";

            saveFileDialog1.FileName = $"{Path.GetFileNameWithoutExtension(_filePath)}_Image.dds";

            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                File.WriteAllBytes(saveFileDialog1.FileName, _fontFile.FontImage.Data);

                MessageBox.Show("File saved!", "Save as DDS...", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
