using System;
using System.Collections.Generic;
using System.Text;

namespace MapVoteWithPreview
{
    internal static class LevelColorDictionary
    {
        private static readonly Dictionary<string, string> _dictionary = new()
        {
            { MapVote.VOTE_RANDOM_LABEL, "#21D710" },
            { "Level - Manor", "#E79F0E" },
            { "Level - Arctic", "#75DCD9" },
            { "Level - Wizard", "#CB11CE" },
            { "Level - Stronghold", "#A56695" },
            { "Level - Museum", "#915829" },
            { "Level - Bunker", "#D6C87E" },
            { "Level - MtHolly", "#508AE1" },
            { "Level - Garden", "#04A831" },
            { "Level - Facility", "#A3BA8C" },
            { "Level - Hospital", "#D53C35" },
            { "Level - Backrooms", "#B7C13D" },
            { "Level - DampMine", "#FA7902" }
        };

        public static string GetColor(string key) => _dictionary.TryGetValue(key, out var value) ? value : "#ffffff";
    }
}
