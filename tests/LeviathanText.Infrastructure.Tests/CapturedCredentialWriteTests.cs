using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Infrastructure;

namespace LeviathanText.Infrastructure.Tests;

public sealed class CapturedCredentialWriteTests
{
    [Theory]
    [InlineData("current", true)]
    [InlineData("rotated-old", false)]
    [InlineData("rotated-current", true)]
    [InlineData("foreign", false)]
    [InlineData("legacy", true)]
    public void Authorized_project_save_checks_optional_captured_key_hash_in_the_write_transaction(string mode, bool allowed)
    {
        var directory = Directory.CreateTempSubdirectory("lt-captured-key-write-").FullName;
        try
        {
            var path = Path.Combine(directory, "store.db");
            using var store = new LocalStore(path);
            var oldHash = UserKeys.Hash("generated-old-key");
            var newHash = UserKeys.Hash("generated-new-key");
            var user = store.CreateUser("generated", UserRole.Annotator, oldHash);
            var project = new NativeProject { Id = new(EntityKind.Collection, "generated"), Name = "before", Objects = [] };
            store.SaveProject(project); project = store.LoadProject(project.Id)!;
            store.AddProjectMember(project.Id, user.Id);
            var before = NativeJson.Save(project);
            if (mode.StartsWith("rotated", StringComparison.Ordinal) || mode == "legacy")
            {
                using var peer = new LocalStore(path);
                peer.UpdateUserKey(user.Id, newHash);
            }
            var expected = mode == "rotated-current" ? newHash : mode == "foreign" ? UserKeys.Hash("generated-foreign-key") : oldHash;
            long Save() => mode == "legacy"
                ? store.SaveProjectAuthorized(project with { Name = "saved" }, user.Id, true)
                : store.SaveProjectAuthorized(project with { Name = "saved" }, user.Id, true, expected);
            if (allowed)
            {
                Assert.Equal(project.Version + 1, Save());
                Assert.Equal("saved", store.LoadProject(project.Id)!.Name);
            }
            else
            {
                Assert.Throws<ProjectWriteAccessDeniedException>(() => Save());
                Assert.Equal(before, NativeJson.Save(store.LoadProject(project.Id)!));
            }
        }
        finally { Directory.Delete(directory, true); }
    }
}
