using System.Reflection;
using System.Security.Cryptography;
using Xunit;
namespace LeviathanText.AI.Tests;
public sealed class SkiaPngCorpusTests
{
    private delegate uint CrcCheck(ReadOnlySpan<byte> data);
    [Fact]
    public void Table_crc_matches_known_vectors_and_independent_bitwise_reference()
    {
        foreach(string method in new[]{"Crc32","Crc32Portable"})
        {
            var check=typeof(VisionImage).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static)!.CreateDelegate<CrcCheck>();
            Assert.Equal(0u,check([])); Assert.Equal(0xcbf43926u,check("123456789"u8)); Assert.Equal(0xae426082u,check("IEND"u8));
            var random=new Random(165);
            for(int size=0;size<=1024;size++)
            {
                var buffer=new byte[size+7];random.NextBytes(buffer);
                for(int offset=0;offset<8;offset++)
                {
                    var data=buffer.AsSpan(offset,size);uint crc=uint.MaxValue;
                    foreach(byte value in data){crc^=value;for(int bit=0;bit<8;bit++)crc=(crc>>1)^((crc&1)==0?0:0xedb88320u);}
                    Assert.Equal(~crc,check(data));
                }
            }
        }
    }
    public static IEnumerable<object[]> Cases => new[] {"rgb8", "rgba8", "gray8", "gray16", "rgb16", "palette8", "unknown-ancillary", "orphan-fctl", "orphan-fdat", "apng", "bad-crc", "corrupt-idat", "duplicate-ihdr", "missing-iend", "over-limit-header", "spoof-gif", "spoof-jpeg", "trailing-chunk", "trailing-junk", "truncated"}.Select(name => new object[] {name});
    [Theory, MemberData(nameof(Cases))]
    public void Format_and_resource_boundaries_preserve_exact_valid_bytes_and_reject_malformed_input(string name)
    {
        var bytes=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Corpus",name+".png"));
        var hash=Convert.ToHexStringLower(SHA256.HashData(bytes));
        bool valid=new[]{"rgb8", "rgba8", "gray8", "gray16", "rgb16", "palette8", "unknown-ancillary"}.Contains(name);
        if(!valid)
        {
            var error=Assert.Throws<InvalidDataException>(()=>VisionImage.FromPng(bytes,hash));
            if(name=="over-limit-header") Assert.Contains("four-million-pixel",error.Message);
            return;
        }
        if(name.StartsWith("gray")) Assert.Equal((byte)0,bytes[25]);
        if(name.EndsWith("16")) Assert.Equal((byte)16,bytes[24]);
        if(name=="palette8") Assert.Equal((byte)3,bytes[25]);
        if(name=="rgb8" || name=="rgb16") Assert.Equal((byte)2,bytes[25]);
        if(name=="rgba8") Assert.Equal((byte)6,bytes[25]);
        var image=VisionImage.FromPng(bytes,hash);
        Assert.Equal(7,image.Width); Assert.Equal(3,image.Height); Assert.Equal(hash,image.Sha256); Assert.Equal(bytes.Length,image.EncodedBytes);
        Assert.Equal("data:image/png;base64,"+Convert.ToBase64String(bytes),typeof(VisionImage).GetMethod("ToDataUri",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(image,null));
        bytes[0]^=1;
        Assert.Equal(hash,image.Sha256); // The caller cannot change the owned snapshot after validation.
    }
}
