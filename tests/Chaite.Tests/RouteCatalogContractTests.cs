using Chaite.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Chaite.Tests
{
    /// <summary>
    /// Keeps the reviewed-route catalogs from drifting apart again.
    ///
    /// The same five routes are spelled out in several places: the C# enum, the
    /// runner's name-to-enum map, the runner's monitor allowlist, the launcher's
    /// argument validation and the probe's own allowlist. Admitting one route
    /// therefore needs five edits, and that has now gone wrong three times —
    /// most recently the Lilith route landed in the enum and the mapping table
    /// while the monitor allowlist kept rejecting it, and only the full package
    /// build noticed, long after the change.
    ///
    /// The same places also have to agree when a route leaves. The Empress
    /// wing and broom routes were withdrawn with their Boss, and the rain-gated
    /// Shrimpy Truffle route before them; each was reachable from every one of
    /// these copies, and the probe's copy is the one that was missed.
    ///
    /// The expected list below is deliberately duplicated: it is the one place a
    /// new route must be added on purpose, and every other copy is compared
    /// against it in both directions, so neither a missing route nor a route
    /// that was deleted from the enum can hide.
    /// </summary>
    internal static partial class Program
    {
        private static void ReviewedRouteCatalogsAgree()
        {
            var expected = new List<string>
            {
                "fishron-fairy-wing",
                "fishron-strong-wing",
                "fishron-trusty-chillet",
                "fishron-trusty-chillet-ignis",
                "fishron-lilith-wolf"
            };

            // The enum is the real source of truth for what exists, so the pinned
            // list must account for every member (None included).
            var enumNames = Enum.GetNames(typeof(FormulaRoute));
            True(expected.Count + 1 == enumNames.Length,
                "the pinned route list and FormulaRoute have drifted: " +
                expected.Count + " routes vs " + enumNames.Length + " members");

            var root = FindRepositoryRoot();
            var runner = File.ReadAllText(Path.Combine(root, "tools",
                "run-boss-validation.ps1"));
            var launcher = File.ReadAllText(Path.Combine(root, "tools",
                "start-isolated-test.ps1"));

            // The runner's name-to-enum map. Each expected route must be a key
            // and must resolve to a real enum member of the right Boss family;
            // otherwise a Fishron case would silently run an Empress route.
            var mapping = ExtractRouteMap(runner);
            foreach (var route in expected)
            {
                string value;
                if (!mapping.TryGetValue(route, out value))
                {
                    True(false, "runner route map is missing: " + route);
                    continue;
                }
                True(Array.IndexOf(enumNames, value) >= 0,
                    "runner maps " + route + " to a route that does not exist: " +
                    value);
                if (Array.IndexOf(enumNames, value) < 0) continue;
                var parsed = (FormulaRoute)Enum.Parse(typeof(FormulaRoute), value);
                // Duke Fishron is the only supported Boss, so every reviewed
                // route must belong to it; an Empress name reappearing here
                // would resolve to no enum member and be caught above.
                True(route.StartsWith("fishron-", StringComparison.Ordinal),
                    "the pinned list has a route outside the Fishron family: " +
                    route);
                True(FormulaRouteCatalog.BelongsToBoss(parsed, 370),
                    route + " is mapped to the wrong Boss family: " + value);
            }

            // The monitor allowlist and the launcher's argument validation must
            // list exactly the same set.
            AssertSameSet("run-boss-validation monitor allowlist",
                ExtractRouteArray(runner, "if ($phase -ceq 'monitor')"), expected);
            AssertSameSet("start-isolated-test route validation",
                ExtractRouteArray(launcher, "'-formularoute'"), expected);

            // The probe keeps a third copy of the same set, and it was the copy
            // nothing checked: the withdrawn rain route was still admitted there
            // after the other two had been corrected. It spells the names with
            // double quotes, so it needs its own extraction.
            var probe = File.ReadAllText(Path.Combine(root, "tools", "GameProbe.cs"));
            AssertSameSet("GameProbe reviewed-route allowlist",
                ExtractDoubleQuotedRoutes(probe,
                    "string[] allowed=scenario.Id==\"duke-fishron\""), expected);
        }

        /// <summary>
        /// The double-quoted route names in the probe's own allowlist.
        ///
        /// Only the contents of the <c>new[]{ ... }</c> arrays are read, not the
        /// whole ternary: the ternary also names the scenarios it branches on
        /// ("duke-fishron", "empress-night", "empress-day"), and those are
        /// fixture ids rather than routes. Reading the whole region reported
        /// "empress-night" as an unreviewed route.
        /// </summary>
        private static List<string> ExtractDoubleQuotedRoutes(string text, string marker)
        {
            var start = text.IndexOf(marker, StringComparison.Ordinal);
            True(start >= 0, "could not find the probe route allowlist marker");
            if (start < 0) return new List<string>();
            var end = text.IndexOf("new string[0]", start, StringComparison.Ordinal);
            True(end > start, "the probe route allowlist is unterminated");
            if (end <= start) end = Math.Min(text.Length, start + 512);
            var region = text.Substring(start, end - start);
            var names = new List<string>();
            foreach (Match array in Regex.Matches(region, @"new\[\]\{([^}]*)\}"))
            foreach (Match match in Regex.Matches(array.Groups[1].Value,
                "\"(?<name>(?:fishron|empress)-[a-z-]+)\""))
            {
                var name = match.Groups["name"].Value;
                if (!names.Contains(name)) names.Add(name);
            }
            True(names.Count > 0, "the probe route allowlist is empty");
            return names;
        }

        private static void AssertSameSet(string label, List<string> actual,
            List<string> expected)
        {
            foreach (var route in expected)
                True(actual.Contains(route), label + " is missing: " + route);
            foreach (var route in actual)
                True(expected.Contains(route),
                    label + " lists an unreviewed route: " + route);
        }

        /// <summary>
        /// The quoted route names in the first <c>-cnotin @( ... )</c> array that
        /// follows <paramref name="marker"/>.
        ///
        /// Scanned rather than matched with one regex because both arrays span
        /// several lines and sit inside nested blocks; a single pattern would
        /// have to encode each file's indentation, which is how this kind of
        /// check quietly stops checking anything.
        /// </summary>
        private static List<string> ExtractRouteArray(string text, string marker)
        {
            var start = text.IndexOf(marker, StringComparison.Ordinal);
            True(start >= 0, "could not find the route array marker: " + marker);
            if (start < 0) return new List<string>();
            var notIn = text.IndexOf("-cnotin @(", start, StringComparison.Ordinal);
            True(notIn >= 0, "no -cnotin array follows: " + marker);
            if (notIn < 0) return new List<string>();
            var open = text.IndexOf('(', notIn);
            var close = text.IndexOf("))", open, StringComparison.Ordinal);
            True(close > open, "the route array is unterminated: " + marker);
            if (close <= open) return new List<string>();
            var names = QuotedNames(text.Substring(open + 1, close - open - 1));
            True(names.Count > 0, "the route array is empty: " + marker);
            return names;
        }

        /// <summary>
        /// The runner's <c>'kebab-name'='EnumMember'</c> pairs. Only the mapping
        /// table has this shape, so matching the shape is enough.
        /// </summary>
        private static Dictionary<string, string> ExtractRouteMap(string runner)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(runner,
                @"'(?<name>(?:fishron|empress)-[a-z-]+)'\s*=\s*'(?<value>[A-Za-z]+)'"))
            {
                var name = match.Groups["name"].Value;
                if (!map.ContainsKey(name)) map[name] = match.Groups["value"].Value;
            }
            True(map.Count > 0, "the runner has no route mapping table.");
            return map;
        }

        private static List<string> QuotedNames(string text)
        {
            var names = new List<string>();
            foreach (Match match in Regex.Matches(text,
                @"'(?<name>(?:fishron|empress)-[a-z-]+)'"))
            {
                var name = match.Groups["name"].Value;
                if (!names.Contains(name)) names.Add(name);
            }
            return names;
        }

        private static string FindRepositoryRoot()
        {
            var directory = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(directory) &&
                !File.Exists(Path.Combine(directory, "Chaite.sln")))
                directory = Path.GetDirectoryName(directory);
            True(!string.IsNullOrEmpty(directory), "repository root not found");
            return directory;
        }
    }
}
