namespace L2Points.Domain.Reports;

/// <summary>A player account a nick read from a screenshot can be matched to.</summary>
public readonly record struct NickCandidate(string UserId, string Nick);

public enum NickMatchKind
{
    /// <summary>Equal ignoring case and surrounding whitespace.</summary>
    Exact,

    /// <summary>Equal after folding look-alike characters, or within a small edit distance.</summary>
    Similar,
}

/// <summary>A nick read from a screenshot matched to a player.</summary>
public sealed record NickMatch(string ReadNick, string UserId, string Nick, NickMatchKind Kind);

/// <summary>Matches in read order and the read nicks that could not be matched, also in read order.</summary>
public sealed record NickMatchResult(IReadOnlyList<NickMatch> Matches, IReadOnlyList<string> Unknown);

/// <summary>
/// Matches nicks read from a game screenshot (OCR-like input, so 0/O, 1/l/I and similar are easily confused)
/// to player nicks. Pure logic, no I/O.
/// </summary>
public static class NickMatcher
{
    private const int ShortNickLength = 6;

    public static NickMatchResult Match(IEnumerable<string> readNicks, IEnumerable<NickCandidate> candidates)
    {
        var reads = DistinctReadNicks(readNicks);
        var pool = candidates.ToList();
        var claimed = new bool[pool.Count];
        var resolved = new NickMatch?[reads.Count];
        var done = new bool[reads.Count];

        // Pass 1: exact matches claim their candidates first, so a similar earlier nick cannot steal them.
        for (var i = 0; i < reads.Count; i++)
        {
            var key = Key(reads[i]);
            for (var c = 0; c < pool.Count; c++)
            {
                if (claimed[c] || Key(pool[c].Nick) != key) continue;
                claimed[c] = true;
                resolved[i] = new NickMatch(reads[i], pool[c].UserId, pool[c].Nick, NickMatchKind.Exact);
                done[i] = true;
                break;
            }
        }

        // Pass 2: fuzzy matches, the most certain (smallest distance) read nick first.
        while (true)
        {
            var bestRead = -1;
            var bestDistance = int.MaxValue;
            var bestCandidate = -1;
            var bestIsTie = false;

            for (var i = 0; i < reads.Count; i++)
            {
                if (done[i]) continue;

                var (distance, candidate, isTie) = FindBest(reads[i], pool, claimed);
                if (candidate < 0)
                {
                    done[i] = true; // nothing within the allowed distance
                    continue;
                }

                if (distance < bestDistance)
                {
                    bestRead = i;
                    bestDistance = distance;
                    bestCandidate = candidate;
                    bestIsTie = isTie;
                }
            }

            if (bestRead < 0) break;

            done[bestRead] = true;
            if (bestIsTie) continue; // ambiguous, stays unknown

            claimed[bestCandidate] = true;
            resolved[bestRead] = new NickMatch(
                reads[bestRead], pool[bestCandidate].UserId, pool[bestCandidate].Nick, NickMatchKind.Similar);
        }

        var matches = resolved.Where(m => m is not null).Select(m => m!).ToList();
        var unknown = reads.Where((_, i) => resolved[i] is null).ToList();
        return new NickMatchResult(matches, unknown);
    }

    private static List<string> DistinctReadNicks(IEnumerable<string> readNicks)
    {
        var seen = new HashSet<string>();
        var result = new List<string>();
        foreach (var nick in readNicks)
        {
            var trimmed = nick.Trim();
            if (trimmed.Length == 0 || !seen.Add(Key(trimmed))) continue;
            result.Add(trimmed);
        }
        return result;
    }

    private static (int Distance, int Candidate, bool IsTie) FindBest(
        string read, List<NickCandidate> pool, bool[] claimed)
    {
        var allowed = Key(read).Length <= ShortNickLength ? 1 : 2;
        var foldedRead = Fold(read);

        var best = int.MaxValue;
        var bestCandidate = -1;
        var isTie = false;
        for (var c = 0; c < pool.Count; c++)
        {
            if (claimed[c]) continue;

            var distance = Levenshtein(foldedRead, Fold(pool[c].Nick));
            if (distance > allowed) continue;

            if (distance < best)
            {
                best = distance;
                bestCandidate = c;
                isTie = false;
            }
            else if (distance == best)
            {
                isTie = true;
            }
        }
        return (best, bestCandidate, isTie);
    }

    private static string Key(string s) => s.Trim().ToUpperInvariant();

    private static string Fold(string s) => string.Create(Key(s).Length, Key(s), static (span, key) =>
    {
        for (var i = 0; i < key.Length; i++)
        {
            span[i] = key[i] switch
            {
                '0' => 'O',
                '1' => 'I',
                'L' => 'I',
                '5' => 'S',
                '8' => 'B',
                var other => other,
            };
        }
    });

    private static int Levenshtein(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }
}
