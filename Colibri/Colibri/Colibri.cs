using System;
using SdlDotNet.Graphics;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Colibri
{
    public partial class Colibri : Form
    {
        private Surface img;
        private bool reset;

        public Colibri()
        {
            InitializeComponent();
            map.Image = Properties.Resources.santa;
            Icon = Icon.FromHandle(Properties.Resources.bird.GetHicon());
            reset = true;
        }

        protected override void OnLoad(EventArgs e)
        {
            string[] args = Environment.GetCommandLineArgs();
            reset = false;
            if (args.Length == 1) return;

            if (args.Length == 2)
            {
                map.Image = Image.FromFile(args[1]);
                img = new Surface(args[1]);
            }

            if(args.Length > 2)
            {
                if (MessageBox.Show("The application cannot use multiple image files.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error) == DialogResult.OK)
                    Application.Exit();
            }
        }

        private void UsePassword(object sender, EventArgs e)
        {
            if (box.Enabled) box.Clear();
            box.Enabled = !box.Enabled;
        }

        private void SetZeros(Surface img, int i, int j)
        {
            Color color = img.GetPixel(new Point(i, j));
            int R, G, B;

            R = (color.R >> 2) << 2;
            G = color.G - (color.G & 1);
            B = color.B - (color.B & 1);

            color = Color.FromArgb(R, G, B);
            img.Draw(new Point(i, j), color);

            color = img.GetPixel(new Point(i + 1, j));

            R = (color.R >> 2) << 2;
            G = color.G - (color.G & 1);
            B = color.B - (color.B & 1);

            color = Color.FromArgb(R, G, B);
            img.Draw(new Point(i + 1, j), color);
        }

        private byte[] Encrypt(byte[] plain, byte[] key)
        {
            SHA256 hash = SHA256.Create();
            byte[] salt = hash.ComputeHash(key);

            Rfc2898DeriveBytes kdb = new Rfc2898DeriveBytes(key, salt, 5000);
            byte[] cipher = kdb.GetBytes(plain.Length);

            for (int k = 0; k < cipher.Length; k++)
                cipher[k] ^= plain[k];

            return cipher;
        }

        private byte[] Decrypt(byte[] cipher, byte[] key)
        { return Encrypt(cipher, key); }

        private void SetPixels(Surface img, byte val, int i, int j)
        {
            int t = val;
            Color color = img.GetPixel(new Point(i, j));
            int R, G, B;

            R = (color.R >> 2) << 2;
            R |= (t & 0x3);
            t >>= 2;

            G = color.G - (color.G & 1);
            G |= (t & 0x1);
            t >>= 1;

            B = color.B - (color.B & 1);
            B |= (t & 0x1);
            t >>= 1;

            color = Color.FromArgb(R, G, B);
            img.Draw(new Point(i, j), color);

            color = img.GetPixel(new Point(i + 1, j));
            R = (color.R >> 2) << 2;
            R |= (t & 0x3);
            t >>= 2;

            G = color.G - (color.G & 1);
            G |= (t & 0x1);
            t >>= 1;

            B = color.B - (color.B & 1);
            B |= (t & 0x1);
            t >>= 1;

            color = Color.FromArgb(R, G, B);
            img.Draw(new Point(i + 1, j), color);
        }

        private Surface HideMessage(Surface img, byte[] cipher)
        {
            Surface vdc = new Surface(img);
            int index = 0;

            for (int i = 0; i < img.Height; i++)
            {
                for(int j = 8; j < img.Width; j += 2)
                {
                    if (index == cipher.Length)
                    {
                        SetZeros(img, j, i);
                        SetZeros(img, j + 1, i);
                        goto result;
                    }
                    else
                        SetPixels(img, cipher[index++], j, i);
                }
            }

            result:
            return vdc;
        }

        private byte GetByte(Surface img, int i, int j)
        {
            Color color = img.GetPixel(new Point(i, j));
            int buf = color.R & 3;
            buf |= ((color.G & 1) << 2);
            buf |= ((color.B & 1) << 3);
            color = img.GetPixel(new Point(i + 1, j));
            buf |= ((color.R & 3) << 4);
            buf |= ((color.G & 1) << 6);
            buf |= ((color.B & 1) << 7);
            return (byte)buf;
        }

        private byte[] ExtractMessage(Surface img)
        {
            List<byte> list = new List<byte>();

            for (int i = 0; i < img.Height; i++)
            {
                for(int j = 8; j < img.Width; j += 2)
                {
                    byte val = GetByte(img, j, i);

                    if (val == 0)
                    {
                        byte sval = GetByte(img, j + 1, i);
                        if (sval == 0) goto result;
                    }
                    else
                        list.Add(val);
                }
            }

            result:
            return list.ToArray();
        }

        private async void hide_Click(object sender, EventArgs e)
        {
            panel.Focus();
            if (reset) return;

            await Task.Run(() =>
            {
                byte[] key = Encoding.ASCII.GetBytes(box.Text);
                byte[] text = Encoding.ASCII.GetBytes(area.Text);
                byte[] cipher = null;

                if(box.Enabled)
                    cipher = Encrypt(text, key);
                else
                {
                    cipher = new byte[text.Length];
                    Array.Copy(text, cipher, text.Length);
                }

                HideMessage(img, cipher);
                MessageBox.Show("Message was hidden successfully.", "Colibri", MessageBoxButtons.OK, MessageBoxIcon.Information);
                area.Clear();
            });
        }

        private async void extract_Click(object sender, EventArgs e)
        {
            area.Clear();
            panel.Focus();
            if (reset) return;

            await Task.Run(() =>
            {
                byte[] key = Encoding.ASCII.GetBytes(box.Text);
                byte[] cipher = ExtractMessage(img);
                byte[] plain = null;

                if(box.Enabled)
                plain = Decrypt(cipher, key);
                else
                {
                    plain = new byte[cipher.Length];
                    Array.Copy(cipher, plain, cipher.Length);
                }

                area.Text = Encoding.ASCII.GetString(plain);
            });
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog opf = new OpenFileDialog();
            opf.Title = "Open image";
            opf.DefaultExt = "*.bmp|*.png|*.jpg|*.gif|*.tga";
            opf.Filter = "BMP(*.bmp)|*.bmp|PNG(*.png)|*.png|JPEG(*.jpg)|*.jpg|GIF(*.gif)|*.gif|TGA(*.tga)|*.tga";

            if(opf.ShowDialog() == DialogResult.OK)
            {
                map.Image = Image.FromStream(opf.OpenFile());
                img = new Surface(opf.FileName);
                reset = false;
            }
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            area.Clear();
            if (reset) return;
            SaveFileDialog svf = new SaveFileDialog();
            svf.Title = "Save as";
            svf.DefaultExt = "*.bmp";
            svf.Filter = "BMP(*.bmp)|*.bmp";

            if (svf.ShowDialog() == DialogResult.OK)
            {
                img.SaveBmp(svf.FileName);
                map.Image = Properties.Resources.santa;
                reset = true;
            }
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (reset) return;
            map.Image = Properties.Resources.santa;
            reset = true;
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            About about = new About();
            about.Show();
        }
    }
}
