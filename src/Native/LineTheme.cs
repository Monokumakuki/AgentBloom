using System;

namespace ScreenLine
{
    internal sealed class LineTheme
    {
        public readonly string Id, Name, Description;
        public readonly string RopeColor, RopeHighlight;
        public readonly double RopeWidth;
        public readonly string[] ClipColors;

        LineTheme(string id, string name, string description, string rope, string highlight, double width, params string[] colors)
        {
            Id = id; Name = name; Description = description;
            RopeColor = rope; RopeHighlight = highlight; RopeWidth = width; ClipColors = colors;
        }

        public static readonly LineTheme[] All = {
            new LineTheme("classic", "鸢尾初晴", "鸢尾紫 · 细碎柔光", "#A18ACC", "#EBE3F6", 1.6, "#D8C8EC"),
            new LineTheme("linen", "杏雨微光", "奶油杏 · 温柔暖意", "#D7A77F", "#FAEDDD", 2.3, "#EDC9A9"),
            new LineTheme("porcelain", "云絮月白", "雾凇蓝 · 月色轻盈", "#91B6C5", "#E9F4F7", 1.9, "#BEDAE4"),
            new LineTheme("color", "春笺花语", "草木绿 · 一点春色", "#96B49D", "#EDF5E9", 1.9, "#B8D0B1", "#E2B7C8", "#EBD6A1"),
            new LineTheme("graphite", "暮紫星河", "暮霭紫 · 静静发光", "#8C82B2", "#E6E2F2", 1.65, "#BDB2D7")
        };

        public static LineTheme FromId(string id)
        {
            return Array.Find(All, theme => theme.Id == id) ?? All[0];
        }
    }
}
