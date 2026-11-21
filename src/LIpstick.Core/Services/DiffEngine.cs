namespace LIpstick.Core.Services;

public enum DiffChunkType
{
    Unchanged,
    Inserted,
    Deleted
}

public record DiffChunk(
    DiffChunkType Type,
    string Text
);

public static class DiffEngine
{
    public static IReadOnlyList<DiffChunk> ComputeWordDiff(string oldText, string newText)
    {
        if (oldText == newText)
        {
            return new[] { new DiffChunk(DiffChunkType.Unchanged, oldText) };
        }

        var oldWords = SplitIntoTokens(oldText);
        var newWords = SplitIntoTokens(newText);

        var chunks = new List<DiffChunk>();

        // Longest Common Subsequence (LCS) matrix
        int[,] matrix = new int[oldWords.Count + 1, newWords.Count + 1];

        for (int i = 1; i <= oldWords.Count; i++)
        {
            for (int j = 1; j <= newWords.Count; j++)
            {
                if (oldWords[i - 1] == newWords[j - 1])
                    matrix[i, j] = matrix[i - 1, j - 1] + 1;
                else
                    matrix[i, j] = Math.Max(matrix[i - 1, j], matrix[i, j - 1]);
            }
        }

        // Backtrack to extract diff chunks
        int r = oldWords.Count;
        int c = newWords.Count;
        var tempChunks = new List<DiffChunk>();

        while (r > 0 || c > 0)
        {
            if (r > 0 && c > 0 && oldWords[r - 1] == newWords[c - 1])
            {
                tempChunks.Add(new DiffChunk(DiffChunkType.Unchanged, oldWords[r - 1]));
                r--;
                c--;
            }
            else if (c > 0 && (r == 0 || matrix[r, c - 1] >= matrix[r - 1, c]))
            {
                tempChunks.Add(new DiffChunk(DiffChunkType.Inserted, newWords[c - 1]));
                c--;
            }
            else if (r > 0 && (c == 0 || matrix[r, c - 1] < matrix[r - 1, c]))
            {
                tempChunks.Add(new DiffChunk(DiffChunkType.Deleted, oldWords[r - 1]));
                r--;
            }
        }

        tempChunks.Reverse();

        // Merge contiguous chunks of same type
        if (tempChunks.Count == 0)
            return chunks;

        var currentType = tempChunks[0].Type;
        var currentText = new System.Text.StringBuilder(tempChunks[0].Text);

        for (int i = 1; i < tempChunks.Count; i++)
        {
            if (tempChunks[i].Type == currentType)
            {
                currentText.Append(tempChunks[i].Text);
            }
            else
            {
                chunks.Add(new DiffChunk(currentType, currentText.ToString()));
                currentType = tempChunks[i].Type;
                currentText.Clear();
                currentText.Append(tempChunks[i].Text);
            }
        }
        chunks.Add(new DiffChunk(currentType, currentText.ToString()));

        return chunks;
    }

    private static List<string> SplitIntoTokens(string text)
    {
        var tokens = new List<string>();
        int i = 0;
        while (i < text.Length)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                int start = i;
                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
                tokens.Add(text[start..i]);
            }
            else if (char.IsPunctuation(text[i]))
            {
                tokens.Add(text[i].ToString());
                i++;
            }
            else
            {
                int start = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && !char.IsPunctuation(text[i])) i++;
                tokens.Add(text[start..i]);
            }
        }
        return tokens;
    }
}
