using System.Text;
using Tsukimichi.Core.Diagnostics;

namespace Tsukimichi.Tests.Diagnostics;

/// <summary>"Trust you can check" (feature plan v7 N2): the fingerprint Settings shows is the one sha256sum prints.</summary>
public class FileFingerprintTests
{
    [Theory]
    [InlineData("", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [InlineData("abc", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    public void A_stream_hashes_to_the_published_vector(string text, string expected)
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(text));
        Assert.Equal(expected, FileFingerprint.Sha256(stream));
    }

    [Fact]
    public void A_file_hashes_as_lowercase_hex_while_another_handle_holds_it_open()
    {
        var path = Path.Combine(Path.GetTempPath(), "tsukimichi-fingerprint-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            File.WriteAllBytes(path, Encoding.ASCII.GetBytes("abc"));

            // The game keeps the loaded plugin file open; the read must share it.
            using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var hash = FileFingerprint.Sha256(path);
            Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
            Assert.Equal(64, hash.Length);
            Assert.Equal(hash.ToLowerInvariant(), hash);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void A_missing_file_throws()
    {
        var path = Path.Combine(Path.GetTempPath(), "tsukimichi-missing-" + Guid.NewGuid().ToString("N") + ".bin");
        Assert.ThrowsAny<IOException>(() => FileFingerprint.Sha256(path));
    }
}
