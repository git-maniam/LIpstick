using LIpstick.Infrastructure.LinkedIn;
using LIpstick.Infrastructure.Storage;
using LIpstick.Tests.Fakes;
using LIpstick.Core.Models;

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

            var profile = await client.IngestProfileAsync();

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
}
