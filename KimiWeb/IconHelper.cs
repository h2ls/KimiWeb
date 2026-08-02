using System.Drawing;
using System.Windows.Resources;

namespace KimiWeb;

public static class IconHelper
{
    /// <summary>从应用资源加载 app.ico，失败时退回系统默认图标。</summary>
    public static Icon LoadAppIcon()
    {
        try
        {
            StreamResourceInfo? info = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/app.ico"));
            if (info?.Stream is not null)
                return new Icon(info.Stream);
        }
        catch { /* 忽略，走默认图标 */ }
        return SystemIcons.Application;
    }
}
