$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$sounds = Join-Path $root 'src\FunnyKeys\Assets\Sounds'
$icons = Join-Path $root 'src\FunnyKeys\Assets\Icons'
New-Item -ItemType Directory -Force $sounds, $icons | Out-Null
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
public static class FunnyAssets {
  public static void Generate(string sounds, string icons) {
    string[] names = { "Shotgun", "Air horn", "Fart", "Boing", "Metal pipe", "Punch", "Explosion", "Quack", "Error beep", "Keyboard click", "Arcade win", "Siren" };
    for (int type = 0; type < names.Length; type++) {
      double duration = type == 9 ? .085 : type == 6 ? 1.2 : type == 11 ? 1.4 : .55;
      int count = (int)(44100 * duration); var random = new Random(7919 + type); double phase = 0, noiseLow = 0;
      using (var stream = File.Create(Path.Combine(sounds, names[type] + ".wav"))) using (var w = new BinaryWriter(stream)) {
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + count * 2); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(44100); w.Write(88200); w.Write((short)2); w.Write((short)16); w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(count * 2);
        for (int i = 0; i < count; i++) {
          double t = i / 44100.0, noise = random.NextDouble() * 2 - 1; noiseLow = .9 * noiseLow + .1 * noise;
          double env = Math.Min(1, t * 600) * Math.Min(1, (duration - t) * 70), x = 0;
          switch (type) {
            case 0: x = (noise * .65 + Math.Sin(2 * Math.PI * 65 * t) * .35) * Math.Exp(-t * 13); break;
            case 1: x = (Math.Sin(2 * Math.PI * 233 * t) + .5 * Math.Sin(2 * Math.PI * 349 * t) + .3 * Math.Sin(2 * Math.PI * 466 * t)) / 1.8; break;
            case 2: phase += 2 * Math.PI * (75 + 25 * Math.Sin(t * 21)) / 44100; x = (Math.Sin(phase) + .4 * Math.Sin(phase * 3) + .7 * noiseLow) * Math.Exp(-t * 3) * (.65 + .35 * Math.Sin(t * 105)); break;
            case 3: phase += 2 * Math.PI * (180 + 570 * Math.Exp(-t * 9) + 60 * Math.Sin(t * 58)) / 44100; x = Math.Sin(phase) * Math.Exp(-t * 5); break;
            case 4: x = (.45 * Math.Sin(2 * Math.PI * 587 * t) + .3 * Math.Sin(2 * Math.PI * 1327 * t) + .2 * Math.Sin(2 * Math.PI * 2531 * t) + noise * .05) * Math.Exp(-t * 6); break;
            case 5: x = (noise * .5 + .5 * Math.Sin(2 * Math.PI * (100 * t - 40 * t * t))) * Math.Exp(-t * 22); break;
            case 6: x = (noiseLow * 2 + .25 * Math.Sin(t * 220)) * Math.Exp(-t * 4); break;
            case 7: phase += 2 * Math.PI * (500 - 300 * t / duration) / 44100; x = (Math.Sin(phase) + .5 * Math.Sin(phase * 3)) / 1.5 * Math.Exp(-t * 5) * (.7 + .3 * Math.Sin(t * 80)); break;
            case 8: x = Math.Sin(2 * Math.PI * (t < .22 ? 440 : 290) * t) * .65 * (t > .22 && t < .27 ? 0 : 1); break;
            case 9: x = (noise * .6 + .4 * Math.Sin(2 * Math.PI * 1300 * t)) * Math.Exp(-t * 85); break;
            case 10: { double f = t < .15 ? 523.25 : t < .3 ? 659.25 : 783.99; phase += 2 * Math.PI * f / 44100; x = Math.Sin(phase) * .7; break; }
            case 11: phase += 2 * Math.PI * (650 + 300 * Math.Sin(t * 10)) / 44100; x = Math.Sin(phase) * .65; break;
          }
          w.Write((short)(Math.Max(-1, Math.Min(1, x * env)) * 22000));
        }
      }
    }
    int[] sizes = {16, 24, 32, 48, 64, 128, 256}; byte[][] pngs = new byte[sizes.Length][];
    for (int n = 0; n < sizes.Length; n++) {
      int size = sizes[n];
      using (var bitmap = new Bitmap(size,size)) using (var g = Graphics.FromImage(bitmap)) {
        g.SmoothingMode = SmoothingMode.AntiAlias; g.ScaleTransform(size/256f,size/256f); g.Clear(Color.Transparent);
        using(var bg = new SolidBrush(Color.FromArgb(186,242,100))) g.FillRectangle(bg,12,12,232,232);
        using(var dark = new SolidBrush(Color.FromArgb(20,29,22))) g.FillRectangle(dark,43,66,102,126);
        using(var lime = new Pen(Color.FromArgb(186,242,100),12)) { g.DrawLine(lime,69,95,119,95); g.DrawLine(lime,69,94,69,165); g.DrawLine(lime,70,127,108,127); }
        using(var pen = new Pen(Color.FromArgb(20,29,22),12)) { pen.StartCap=LineCap.Round; pen.EndCap=LineCap.Round; g.DrawArc(pen,143,85,38,85,-65,130); g.DrawArc(pen,153,58,65,140,-62,124); }
        using(var ms = new MemoryStream()) { bitmap.Save(ms,ImageFormat.Png); pngs[n]=ms.ToArray(); }
        bitmap.Save(Path.Combine(icons,"FunnyKeys-"+size+".png"),ImageFormat.Png);
      }
    }
    using(var w = new BinaryWriter(File.Create(Path.Combine(icons,"FunnyKeys.ico")))) {
      w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length); int offset=6+16*sizes.Length;
      for(int i=0;i<sizes.Length;i++) { w.Write((byte)(sizes[i]==256?0:sizes[i])); w.Write((byte)(sizes[i]==256?0:sizes[i])); w.Write((short)0); w.Write((short)1); w.Write((short)32); w.Write(pngs[i].Length); w.Write(offset); offset+=pngs[i].Length; }
      foreach(var bytes in pngs) w.Write(bytes);
    }
  }
}
'@
[FunnyAssets]::Generate($sounds, $icons)
Write-Host 'Generated 12 original synthetic WAV effects and 7 icon sizes.'
