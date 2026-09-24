using Dalamud.Bindings.ImGui;
using System;
using System.Numerics;

namespace FantasyPlayer.Interface
{
    public static class InterfaceUtils
    {
        public static readonly Vector4 FantasyPlayerColor = new Vector4(0.60f, 0.59f, 0.92f, 1.00f);
        public static readonly Vector4 DarkenColor = new Vector4(1, 1, 1, 0.75f);
        public static readonly Vector4 DarkenButtonColor = new Vector4(1, 1, 1, 0.25f);
        public static readonly Vector4 TransparentColor = Vector4.Zero;
        
        public static void TextCentered(string text)
        {
            var textWidth = ImGui.CalcTextSize(text).X;
            var avail = ImGui.GetContentRegionAvail().X;
            ImGui.SetCursorPosX(MathF.Max(0f, (avail - textWidth) / 2));
            ImGui.Text(text);
            ImGui.Spacing();
        }
        
        public static bool ButtonCentered(string text)
        {
            var textWidth = ImGui.CalcTextSize(text).X + ImGui.GetStyle().FramePadding.X * 2;
            var avail = ImGui.GetContentRegionAvail().X;
            ImGui.SetCursorPosX(MathF.Max(0f, (avail - textWidth) / 2));
            return ImGui.Button(text);
        }
    }
}