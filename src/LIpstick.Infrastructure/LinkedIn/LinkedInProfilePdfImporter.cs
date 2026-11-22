using System.Text;
using UglyToad.PdfPig;

namespace LIpstick.Infrastructure.LinkedIn;

public sealed record LinkedInProfilePdfImport(string Text, int PageCount);

public static class LinkedInProfilePdfImporter
{
    private const long MaximumPdfBytes = 20 * 1024 * 1024;
    private const int MaximumPages = 50;
    private const int MaximumExtractedCharacters = 1_000_000;

    public static LinkedInProfilePdfImport ExtractText(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException("Choose a LinkedIn Save to PDF file.", path);
        if (!string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The selected file must be a PDF.");
        if (new FileInfo(path).Length > MaximumPdfBytes)
            throw new InvalidDataException("The profile PDF is larger than 20 MB.");

        using var document = PdfDocument.Open(path);
        if (document.NumberOfPages is < 1 or > MaximumPages)
            throw new InvalidDataException($"The profile PDF must contain between 1 and {MaximumPages} pages.");

        var text = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            text.AppendLine(page.Text);
            if (text.Length > MaximumExtractedCharacters)
                throw new InvalidDataException("The extracted profile text is too large to import.");
        }

        string extracted = text.ToString().Trim();
        if (extracted.Length < 40)
            throw new InvalidDataException("No readable text was found in this PDF. Choose the PDF created by LinkedIn's Save to PDF option.");

        return new LinkedInProfilePdfImport(extracted, document.NumberOfPages);
    }
}
