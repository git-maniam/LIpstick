using LIpstick.Infrastructure.LinkedIn;

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
}
