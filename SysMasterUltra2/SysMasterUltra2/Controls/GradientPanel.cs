using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SysMasterUltra.Controls
{
    public class GradientPanel : Panel
    {
        private Color startColor = Color.FromArgb(30, 30, 40);
        private Color endColor = Color.FromArgb(50, 50, 60);
        private float angle = 45f;

        public GradientPanel()
        {
            this.DoubleBuffered = true;
        }

        public Color StartColor
        {
            get => startColor;
            set { startColor = value; Invalidate(); }
        }

        public Color EndColor
        {
            get => endColor;
            set { endColor = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (LinearGradientBrush brush = new LinearGradientBrush(
                this.ClientRectangle,
                startColor,
                endColor,
                angle))
            {
                e.Graphics.FillRectangle(brush, this.ClientRectangle);
            }
        }
    }
}