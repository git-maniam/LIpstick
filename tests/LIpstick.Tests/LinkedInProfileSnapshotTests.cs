using LIpstick.Infrastructure.LinkedIn;
using LIpstick.Infrastructure.Storage;
using LIpstick.Tests.Fakes;
using LIpstick.Core.Models;
using UglyToad.PdfPig.Writer;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;

namespace LIpstick.Tests;

[TestClass]
public class LinkedInProfileSnapshotTests
{
    [TestMethod]
    public void AcceptsOnlyLinkedInMemberProfileUrls()
    {
        Assert.IsTrue(BrowserLinkedInProfileClient.IsProfileUrl("https://www.linkedin.com/in/rsmaniam/"));
        Assert.IsFalse(BrowserLinkedInProfileClient.IsProfileUrl("https://example.com/in/rsmaniam/"));
        Assert.IsFalse(BrowserLinkedInProfileClient.IsProfileUrl("https://www.linkedin.com/company/example/"));
        Assert.IsFalse(BrowserLinkedInProfileClient.IsProfileUrl("javascript:alert(1)"));
    }

    [TestMethod]
    public void ParsesUserProvidedSnapshotSectionsWithoutInventingProfileFields()
    {
        const string url = "https://www.linkedin.com/in/rsmaniam/";
        const string text = "Ravi Maniam\nProduct Leader\nAbout\nI build product teams.\nExperience\nVP Product\nAcme Corp\nBuilt a platform for customers.\nEducation\nExample University\nMBA\nSkills\nProduct Strategy\nLeadership\nFeatured\nProduct keynote";

        var profile = BrowserLinkedInProfileClient.ParseProfile(url, text);

        Assert.AreEqual("Ravi Maniam", profile.FullName);
        Assert.AreEqual("Product Leader", profile.Headline);
        Assert.AreEqual("I build product teams.", profile.About);
        Assert.AreEqual(url, profile.CustomUrl);
        Assert.AreEqual(1, profile.Experiences.Count);
        StringAssert.Contains(profile.Experiences[0].Description, "Built a platform for customers.");
        Assert.AreEqual("Example University", profile.Educations[0].Institution);
        CollectionAssert.AreEquivalent(new[] { "Product Strategy", "Leadership" }, profile.Skills.Select(skill => skill.Name).ToArray());
        Assert.AreEqual("Product keynote", profile.FeaturedItems[0].Title);
        Assert.AreEqual(text, profile.SourceSnapshotText);
        Assert.IsNull(profile.PhotoUrl);
        Assert.IsNull(profile.BannerUrl);
    }

    [TestMethod]
    public void ParsesVisibleTextAndOpenGraphMetadataFromPublicProfileHtml()
    {
        const string url = "https://www.linkedin.com/in/rsmaniam/";
        const string html = "<html><head><meta property=\"og:title\" content=\"Ravi Maniam | LinkedIn\"><meta property=\"og:description\" content=\"Product Leader\"></head><body><main><h1>Ravi Maniam</h1><p>Product Leader</p><h2>About</h2><p>I build useful products.</p><h2>Experience</h2><p>VP Product</p><p>Acme Corp</p></main><script>secret noise</script></body></html>";

        var profile = BrowserLinkedInProfileClient.ParsePublicPage(url, html);

        Assert.AreEqual("Ravi Maniam", profile.FullName);
        Assert.AreEqual("Product Leader", profile.Headline);
        Assert.AreEqual("I build useful products.", profile.About);
        StringAssert.Contains(profile.SourceSnapshotText!, "Acme Corp");
        Assert.IsFalse(profile.SourceSnapshotText!.Contains("secret noise", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task FetchesOnePublicUrlWithoutCookiesAndCachesParsedSnapshot()
    {
        const string url = "https://www.linkedin.com/in/rsmaniam/";
        const string html = "<html><head><meta property=\"og:title\" content=\"Ravi Maniam | LinkedIn\"><meta property=\"og:description\" content=\"Product Leader\"></head><body><main><h1>Ravi Maniam</h1><p>Product Leader</p><h2>About</h2><p>I build useful products.</p></main></body></html>";
        var testDirectory = Path.Combine(Path.GetTempPath(), $"LIpstickProfileScanTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        try
        {
            var storage = new LocalDataService(testDirectory);
            await storage.SaveConfigAsync(new AppConfig { LinkedInProfileUrl = url });
            var handler = new MockHttpMessageHandler { ResponseContent = html };
            var client = new BrowserLinkedInProfileClient(storage, new HttpClient(handler));

            var profile = await client.ScanPublicProfileAsync(url, initialPdfText: null, isInitialScan: false);

            Assert.AreEqual("Ravi Maniam", profile.FullName);
            Assert.AreEqual(1, handler.CapturedRequests.Count);
            Assert.IsFalse(handler.LastRequest!.Headers.Contains("Cookie"));
            Assert.AreEqual(profile.SourceSnapshotText, (await storage.LoadConfigAsync()).LinkedInProfileText);
        }
        finally
        {
            if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task InitialScanCombinesPdfAndPublicPageThenRescanUsesOnlyFreshPublicPage()
    {
        const string url = "https://www.linkedin.com/in/rsmaniam/";
        const string firstHtml = "<html><body><main><h1>Ravi Maniam</h1><p>Product Leader</p><h2>About</h2><p>Public about, initial</p></main></body></html>";
        const string updatedHtml = "<html><body><main><h1>Ravi Maniam</h1><p>Product Executive</p><h2>About</h2><p>Updated public about</p></main></body></html>";
        const string pdfText = "Ravi Maniam\nProduct Leader\nAbout\nPDF details with long form profile content and additional evidence.";
        var directory = Path.Combine(Path.GetTempPath(), $"LIpstickProfileScanTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var storage = new LocalDataService(directory);
            await storage.SaveConfigAsync(new AppConfig { LinkedInProfileUrl = url, LinkedInPdfText = pdfText });
            var handler = new MockHttpMessageHandler();
            handler.ResponseFactory = _ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(handler.CapturedRequests.Count == 1 ? firstHtml : updatedHtml)
            };
            var client = new BrowserLinkedInProfileClient(storage, new HttpClient(handler));

            var initial = await client.ScanPublicProfileAsync(url, pdfText, isInitialScan: true);
            var rescanned = await client.ScanPublicProfileAsync(url, initialPdfText: null, isInitialScan: false);

            StringAssert.Contains(initial.SourceSnapshotText!, "LinkedIn Save to PDF");
            StringAssert.Contains(initial.SourceSnapshotText!, "PDF details with long form");
            StringAssert.Contains(rescanned.SourceSnapshotText!, "Updated public about");
            Assert.IsFalse(rescanned.SourceSnapshotText!.Contains("PDF details with long form", StringComparison.Ordinal));
            Assert.AreEqual(2, handler.CapturedRequests.Count);
            Assert.AreEqual(rescanned.SourceSnapshotText, (await storage.LoadConfigAsync()).LinkedInProfileText);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ImportsLinkedInSaveToPdfText()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        string[] lines = ["Ravi Maniam", "Product Leader", "About", "I build useful products and lead teams with measurable outcomes."];
        for (int index = 0; index < lines.Length; index++)
            page.AddText(lines[index], 12, new PdfPoint(40, 700 - (index * 18)), font);
        var path = Path.Combine(Path.GetTempPath(), $"LIpstickProfile_{Guid.NewGuid():N}.pdf");
        try
        {
            File.WriteAllBytes(path, builder.Build());
            var imported = LinkedInProfilePdfImporter.ExtractText(path);
            Assert.AreEqual(1, imported.PageCount);
            StringAssert.Contains(imported.Text, "Ravi Maniam");
            StringAssert.Contains(imported.Text, "measurable outcomes");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
