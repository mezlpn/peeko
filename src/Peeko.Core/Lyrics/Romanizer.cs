using System.Text;

namespace Peeko.Core.Lyrics;

/// <summary>
/// Romanisasi lirik tanpa internet: Hangul (Korea) dengan Romanisasi yang Disempurnakan, dan kana
/// Jepang (hiragana/katakana) dengan Hepburn. Kanji dan Hanzi dibiarkan (butuh kamus besar).
/// Aturan perubahan bunyi Korea yang paling umum (batchim sebelum vokal pindah ke suku berikutnya)
/// ikut diterapkan supaya enak dinyanyikan.
/// </summary>
public static class Romanizer
{
    private static readonly string[] Initials = ["g", "kk", "n", "d", "tt", "r", "m", "b", "pp", "s", "ss", "", "j", "jj", "ch", "k", "t", "p", "h"];
    private static readonly string[] Vowels = ["a", "ae", "ya", "yae", "eo", "e", "yeo", "ye", "o", "wa", "wae", "oe", "yo", "u", "wo", "we", "wi", "yu", "eu", "ui", "i"];
    // Batchim di akhir kata/sebelum konsonan.
    private static readonly string[] Finals = ["", "k", "k", "k", "n", "n", "n", "t", "l", "k", "m", "p", "l", "l", "p", "l", "m", "p", "p", "t", "t", "ng", "t", "t", "k", "t", "p", "t"];
    // Batchim yang pindah ke suku berikutnya bila suku itu diawali ㅇ (vokal): bunyi aslinya.
    private static readonly string[] FinalsLinked = ["", "g", "kk", "ks", "n", "nj", "nh", "d", "r", "lg", "lm", "lb", "ls", "lt", "lp", "lh", "m", "b", "ps", "s", "ss", "ng", "j", "ch", "k", "t", "p", "h"];

    public static bool HasHangul(string s) => s.Any(IsHangul);

    public static bool HasKana(string s) => s.Any(c => c is >= '぀' and <= 'ヿ');

    /// <summary>Teks yang perlu diromanisasi (ada Hangul atau kana)?</summary>
    public static bool Applies(string s) => HasHangul(s) || HasKana(s);

    public static string Romanize(string text)
    {
        if (!Applies(text)) return text;
        return Kana(Hangul(text));
    }

    private static bool IsHangul(char c) => c is >= '가' and <= '힣';

    private static string Hangul(string text)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (!IsHangul(c))
            {
                sb.Append(c);
                continue;
            }
            int code = c - 0xAC00;
            int initial = code / 588, vowel = code % 588 / 28, final = code % 28;
            // Suku pertama kata: tidak didahului batchim yang pindah.
            sb.Append(Initials[initial]).Append(Vowels[vowel]);
            bool nextIsVowel = i + 1 < text.Length && IsHangul(text[i + 1]) && (text[i + 1] - 0xAC00) / 588 == 11; // ㅇ
            if (final == 0) continue;
            if (nextIsVowel)
            {
                // 밥을 → ba-beul: batchim jadi awal suku berikutnya.
                sb.Append(FinalsLinked[final] == "ng" ? "ng" : FinalsLinked[final]);
                if (FinalsLinked[final] == "h") sb.Length--; // ㅎ sebelum vokal hampir tak terdengar
            }
            else
            {
                sb.Append(Finals[final]);
            }
        }
        return sb.ToString();
    }

    private static readonly Dictionary<string, string> KanaMap = BuildKana();

    private static Dictionary<string, string> BuildKana()
    {
        var m = new Dictionary<string, string>();
        void Row(string kana, params string[] roma)
        {
            for (int i = 0; i < kana.Length && i < roma.Length; i++) m[kana[i].ToString()] = roma[i];
        }
        Row("あいうえお", "a", "i", "u", "e", "o");
        Row("かきくけこ", "ka", "ki", "ku", "ke", "ko");
        Row("がぎぐげご", "ga", "gi", "gu", "ge", "go");
        Row("さしすせそ", "sa", "shi", "su", "se", "so");
        Row("ざじずぜぞ", "za", "ji", "zu", "ze", "zo");
        Row("たちつてと", "ta", "chi", "tsu", "te", "to");
        Row("だぢづでど", "da", "ji", "zu", "de", "do");
        Row("なにぬねの", "na", "ni", "nu", "ne", "no");
        Row("はひふへほ", "ha", "hi", "fu", "he", "ho");
        Row("ばびぶべぼ", "ba", "bi", "bu", "be", "bo");
        Row("ぱぴぷぺぽ", "pa", "pi", "pu", "pe", "po");
        Row("まみむめも", "ma", "mi", "mu", "me", "mo");
        Row("やゆよ", "ya", "yu", "yo");
        Row("らりるれろ", "ra", "ri", "ru", "re", "ro");
        Row("わをん", "wa", "o", "n");
        Row("ぁぃぅぇぉ", "a", "i", "u", "e", "o");
        Row("ゔ", "vu");
        // Gabungan kecil ゃゅょ: きゃ → kya, しゃ → sha, ちゃ → cha, じゃ → ja.
        foreach (var (k, r) in new[] { ("き", "ky"), ("ぎ", "gy"), ("し", "sh"), ("じ", "j"), ("ち", "ch"), ("に", "ny"), ("ひ", "hy"), ("び", "by"), ("ぴ", "py"), ("み", "my"), ("り", "ry") })
        {
            m[k + "ゃ"] = r + "a";
            m[k + "ゅ"] = r + "u";
            m[k + "ょ"] = r + "o";
        }
        // Katakana = hiragana + 0x60.
        foreach (var (k, v) in m.ToList())
            m[new string(k.Select(ch => ch is >= 'ぁ' and <= 'ゖ' ? (char)(ch + 0x60) : ch).ToArray())] = v;
        m["ー"] = "-";
        return m;
    }

    private static string Kana(string text)
    {
        if (!HasKana(text)) return text;
        var sb = new StringBuilder();
        bool doubleNext = false;
        bool afterN = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c is 'っ' or 'ッ')
            {
                doubleNext = true; // konsonan berikutnya digandakan: きって → kitte
                afterN = false;
                continue;
            }
            string? roma = null;
            if (i + 1 < text.Length && KanaMap.TryGetValue(text.Substring(i, 2), out var pair))
            {
                roma = pair;
                i++;
            }
            else if (KanaMap.TryGetValue(c.ToString(), out var single))
            {
                roma = single;
            }
            if (roma is null)
            {
                sb.Append(c);
                doubleNext = afterN = false;
                continue;
            }
            if (roma == "-")
            {
                if (sb.Length > 0 && "aiueo".Contains(sb[^1])) sb.Append(sb[^1]); // ラーメン → raamen
                continue;
            }
            // ん sebelum vokal/y diberi apostrof supaya tidak terbaca "na": きんえん → kin'en
            if (afterN && "aiueoy".Contains(roma[0])) sb.Append('\'');
            if (doubleNext && !"aiueon".Contains(roma[0])) sb.Append(roma.StartsWith("ch") ? 't' : roma[0]);
            doubleNext = false;
            afterN = c is 'ん' or 'ン';
            sb.Append(roma);
        }
        return sb.ToString();
    }
}
