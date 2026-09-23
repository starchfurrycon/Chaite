using System;
using System.Collections.Generic;

namespace Chaite.Manager
{
    /// <summary>
    /// The moment a joke line is being shown for.
    /// </summary>
    internal enum MemeMoment
    {
        Boot,
        Inspecting,
        Ready,
        Installed,
        Blocked,
        Busy,
        Footer,
        Scope
    }

    /// <summary>
    /// The console's meme register.
    ///
    /// Every line below is a VERBATIM quote from `docs/meme-register.md`, which is
    /// the single source of truth for this project's memes. The owner's instruction
    /// on 2026-09-23 was that the lines must be the memes themselves rather than
    /// sentences written around them: "你对'梗'进行了一些加工，尽量使用梗的原文".
    /// So there is no line here that the register does not already carry, and
    /// `MemeMoment.Scope` is character-for-character the table in section five of
    /// that file, which states it must match this file verbatim.
    ///
    /// The register's own inventory, by section:
    ///
    ///   section one (EzFic): "打这个设计失败的 boss 必须要有 300 颗" / "桑百颗" /
    ///     "星星炮大战骷髅王" / "设计失败的 boss" / "来吧，试一下米妮" /
    ///     "我没有史莱姆 ang 啊" / "亡了亡了" / "低级的拆特" /
    ///     "这个波斯可是超囊的对我来说" / "从来没试过哦"
    ///   section two (Kobe): "MAN" / "MANBA OUT"
    ///   section five (scope): the three lines in MemeMoment.Scope
    ///
    /// "正在看这个 BOSS 是不是设计失败的" is the register's own wording for the
    /// inspecting moment (section one, the 设计失败的波斯 row).
    ///
    /// The pools repeat across moments on purpose: the register calls this one
    /// header pool, and a moment with no registered line of its own draws from the
    /// register rather than inventing a new sentence.
    ///
    /// Text only. No third-party image, audio or clip is generated, downloaded or
    /// redistributed by this file or by this repository.
    ///
    /// Selection is a pure function of (moment, index) so that a given UI state
    /// always renders the same line. The smoke test compares layouts, and a timer
    /// that re-rolled the line between two layout passes would make it flaky.
    /// </summary>
    internal static class MemeVoice
    {
        private static readonly Dictionary<MemeMoment, string[]> Lines =
            new Dictionary<MemeMoment, string[]>
            {
                {
                    MemeMoment.Boot, new[]
                    {
                        "来吧，试一下米妮",
                        "星星炮大战骷髅王",
                        "桑百颗"
                    }
                },
                {
                    MemeMoment.Inspecting, new[]
                    {
                        "正在看这个 BOSS 是不是设计失败的",
                        "打这个设计失败的 boss 必须要有 300 颗",
                        "桑百颗"
                    }
                },
                {
                    MemeMoment.Ready, new[]
                    {
                        "来吧，试一下米妮",
                        "桑百颗",
                        "星星炮大战骷髅王"
                    }
                },
                {
                    MemeMoment.Installed, new[]
                    {
                        "来吧，试一下米妮",
                        "MAN",
                        "MANBA OUT"
                    }
                },
                {
                    MemeMoment.Blocked, new[]
                    {
                        "这个波斯可是超囊的对我来说",
                        "从来没试过哦",
                        "我没有史莱姆 ang 啊"
                    }
                },
                {
                    MemeMoment.Busy, new[]
                    {
                        "桑百颗",
                        "星星炮大战骷髅王",
                        "来吧，试一下米妮"
                    }
                },
                {
                    MemeMoment.Footer, new[]
                    {
                        "MANBA OUT",
                        "MAN",
                        "亡了亡了"
                    }
                },
                {
                    // Verbatim section five of docs/meme-register.md, which states
                    // these must match this file character for character.
                    MemeMoment.Scope, new[]
                    {
                        "只拆猪鲨一个；别的波斯都超囊",
                        "固定表只认一条公式：猪鲨",
                        "不搜路、不评分、不中途换装"
                    }
                }
            };

        internal static string Line(MemeMoment moment, int index)
        {
            string[] pool;
            if (!Lines.TryGetValue(moment, out pool) || pool.Length == 0)
                return string.Empty;
            // Guard the modulo: a negative index must not index from the end.
            var slot = ((index % pool.Length) + pool.Length) % pool.Length;
            return pool[slot];
        }

        internal static int Count(MemeMoment moment)
        {
            string[] pool;
            return Lines.TryGetValue(moment, out pool) ? pool.Length : 0;
        }

        /// <summary>
        /// The rotating counter the console uses to advance through a pool. One
        /// step per state change keeps every rendered state reproducible.
        /// </summary>
        internal static int Next(ref int counter, MemeMoment moment)
        {
            var count = Count(moment);
            if (count <= 0) return 0;
            var value = counter % count;
            counter = (counter + 1) % count;
            return value;
        }
    }
}
