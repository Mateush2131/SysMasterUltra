using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SysMasterUltra.Controls
{
    public class MaterialButton : Button
    {
        private Timer animationTimer;
        private float glowOpacity = 0f;
        private Color glowColor = Color.Cyan;
        private int borderRadius = 10;

        public MaterialButton()
        {
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.BackColor = Color.FromArgb(63, 81, 181);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            this.Cursor = Cursors.Hand;
            this.Size = new Size(150, 40);

            animationTimer = new Timer { Interval = 20 };
            animationTimer.Tick += (s, e) =>
            {
                glowOpacity = Math.Max(0, glowOpacity - 0.05f);
                this.Invalidate();
                if (glowOpacity <= 0) animationTimer.Stop();
            };

            this.MouseEnter += (s, e) => {
                glowOpacity = 0.5f;
                animationTimer.Stop();
                this.Invalidate();
            };

            this.MouseLeave += (s, e) => {
                animationTimer.Start();
            };
        }

        public Color GlowColor
        {
            get => glowColor;
            set { glowColor = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Фон
            using (var path = CreateRoundedPath(ClientRectangle, borderRadius))
            using (var brush = new SolidBrush(BackColor))
            {
                e.Graphics.FillPath(brush, path);
            }

            // Свечение
            if (glowOpacity > 0)
            {
                using (var path = CreateRoundedPath(ClientRectangle, borderRadius))
                using (var brush = new SolidBrush(Color.FromArgb((int)(glowOpacity * 255), glowColor)))
                {
                    e.Graphics.FillPath(brush, path);
                }
            }

            // Текст
            StringFormat sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;

            e.Graphics.DrawString(Text, Font, new SolidBrush(ForeColor),
                ClientRectangle, sf);
        }

        private GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
            path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
            path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}