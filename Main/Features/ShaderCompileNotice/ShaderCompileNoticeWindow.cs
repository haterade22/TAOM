using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TAOM.Features.ShaderCompileNotice;

/// <summary>
/// The notice itself: a borderless window that never takes the focus and stays out of the task bar,
/// owned by the game's window so it lies over the game and not over programs in front of it. Sized
/// from its own texts as this display renders them (fixed pixel sizes clip on a scaled display).
/// </summary>
internal sealed class ShaderCompileNoticeWindow : Form
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;

    private readonly Label _title = new();
    private readonly Label _detail = new();

    public ShaderCompileNoticeWindow(string widestTitle, string detail, IntPtr gameWindow)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(28, 24, 20);

        _title.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
        _title.ForeColor = Color.FromArgb(226, 200, 140);
        _title.Text = widestTitle;
        _detail.Font = new Font("Segoe UI", 9.5f);
        _detail.ForeColor = Color.FromArgb(205, 198, 186);
        _detail.Text = detail;

        Size title = _title.GetPreferredSize(Size.Empty);
        Size body = _detail.GetPreferredSize(Size.Empty);
        int pad = Math.Max(14, title.Height / 2);
        int width = Math.Max(title.Width, body.Width) + pad / 2;
        _title.SetBounds(pad, pad, width, title.Height);
        _detail.SetBounds(pad, pad + title.Height + pad / 3, width, body.Height);
        ClientSize = new Size(width + 2 * pad, _detail.Bottom + pad);
        Controls.Add(_title);
        Controls.Add(_detail);

        Rectangle area = Screen.PrimaryScreen.Bounds;
        if (gameWindow != IntPtr.Zero && GetWindowRect(gameWindow, out NativeRect r)
            && r.Right - r.Left > Width && r.Bottom - r.Top > Height)
        {
            area = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        }

        // Centred, below the middle: the loading symbol sits in the centre.
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (int)(area.Height * 0.62));
    }

    public string TitleText
    {
        set => _title.Text = value;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams p = base.CreateParams;
            p.ExStyle |= WsExNoActivate | WsExToolWindow;
            return p;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }
}

/// <summary>Lets <see cref="Form.Show(IWin32Window)"/> take the game's raw window handle as owner.</summary>
internal sealed class WindowHandleOwner : IWin32Window
{
    public WindowHandleOwner(IntPtr handle) => Handle = handle;

    public IntPtr Handle { get; }
}
