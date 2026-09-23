param([string]$Out = 'gui.png', [int]$Width = 0, [int]$Height = 0)
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32 {
  [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);
  [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool repaint);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@
# Without this the coordinates come back scaled down and the capture clips the window's right edge.
[void][Win32]::SetProcessDPIAware()
Add-Type -AssemblyName System.Drawing
$proc = Get-Process GitProfile -ErrorAction Stop | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
$hwnd = $proc.MainWindowHandle
if ($Width -gt 0 -and $Height -gt 0) {
  $r = New-Object Win32+Rect
  [void][Win32]::GetWindowRect($hwnd, [ref]$r)
  [void][Win32]::MoveWindow($hwnd, $r.Left, $r.Top, $Width, $Height, $true)
  Start-Sleep -Milliseconds 800
}
[void][Win32]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 500
$r = New-Object Win32+Rect
[void][Win32]::GetWindowRect($hwnd, [ref]$r)
$w = $r.Right - $r.Left
$h = $r.Bottom - $r.Top
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
$bmp.Save([System.IO.Path]::GetFullPath($Out))
$g.Dispose()
$bmp.Dispose()
Write-Output "captured $Out ${w}x${h}"
