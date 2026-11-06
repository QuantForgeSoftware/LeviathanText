using System.Security.Cryptography;
using System.Text;
using LeviathanText.AI;
using LeviathanText.Contracts;
using LeviathanText.Contracts.Serialization;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

public sealed class NativeAiProposalServiceTests
{
    private static readonly AgentRef Acceptor = new() { Id = "explicit-human", IsModel = false };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Repeated_source_without_editorial_change_uses_direct_source_composition(bool existingUnchangedL2)
    {
        var fixture = new Fixture("ααα");
        var issued = await fixture.Issue(new(0, 2), "α");
        if (existingUnchangedL2) fixture.EditL2("ααα");

        var accepted = fixture.Service.Accept(fixture.Current, [issued.Proposal.Id], Acceptor);

        Assert.Equal("αα", accepted.Acceptances[0].EditorialRevision.Text);
        Assert.Equal(new ScalarRange(0, 2), accepted.Acceptances[0].Receipt.Range);
        Assert.Equal(new ScalarRange(0, 2), fixture.Current.Objects[0].Surfaces[0].Annotations.Last().Range);
        Assert.Equal("ααα", fixture.Current.Objects[0].Surfaces[0].Revisions.Single(revision => revision.Id == issued.SourceRevision.Id).Text);
    }

    [Theory]
    [InlineData("ααβ", true, "αβ")]
    [InlineData("βαα", false, null)]
    public async Task Repeated_source_keeps_the_declared_replacement_hunk_instead_of_rediffing_it(
        string editorial, bool canAccept, string? expected)
    {
        var fixture = new Fixture("ααα");
        var issued = await fixture.Issue(new(0, 2), "α");
        fixture.EditL2(editorial);
        int saves = fixture.Store.Saves;

        var preview = fixture.Service.Preview(fixture.Current, [issued.Proposal.Id]);

        Assert.Equal(canAccept, preview.IsFullyApplicable);
        if (canAccept)
            Assert.Equal(expected, fixture.Service.Accept(fixture.Current, [issued.Proposal.Id], Acceptor).Acceptances[0].EditorialRevision.Text);
        else
        {
            Assert.Equal(BulkAcceptanceBlocker.EditorialConflict, preview.Items[0].Blocker);
            Assert.Throws<LeviathanException>(() => fixture.Service.Accept(fixture.Current, [issued.Proposal.Id], Acceptor));
            Assert.Equal(saves, fixture.Store.Saves);
        }
    }

    [Theory]
    [InlineData("αα", 1, "Ω")]
    [InlineData("αααα", 1, "Ω")]
    [InlineData("αα", 2, "α")]
    public async Task Ambiguous_remote_insertions_or_deletions_cannot_be_guessed_from_repeated_text(
        string editorial, int end, string replacement)
    {
        var fixture = new Fixture("ααα");
        var issued = await fixture.Issue(new(0, end), replacement);
        fixture.EditL2(editorial);
        int saves = fixture.Store.Saves;

        Assert.Equal(BulkAcceptanceBlocker.EditorialConflict,
            Assert.Single(fixture.Service.Preview(fixture.Current, [issued.Proposal.Id]).Items).Blocker);
        Assert.Throws<LeviathanException>(() => fixture.Service.Accept(fixture.Current, [issued.Proposal.Id], Acceptor));
        Assert.Equal(saves, fixture.Store.Saves);
    }

    [Theory]
    [InlineData("empty-reading")]
    [InlineData("nonempty-restoration")]
    [InlineData("merged-restoration")]
    public async Task Unproven_layer_mapping_is_refused_without_writes(string condition)
    {
        var fixture = new Fixture();
        var task = condition == "empty-reading" ? AiTaskKind.CandidateReading : AiTaskKind.Restoration;
        var range = condition == "nonempty-restoration" ? new ScalarRange(1, 2) : new ScalarRange(1, 1);
        var issued = await fixture.Issue(range, "Ω", task: task);
        if (condition == "merged-restoration") fixture.EditL2("α𐅀β\u0301γδX");
        int saves = fixture.Store.Saves;

        Assert.Equal(BulkAcceptanceBlocker.EditorialConflict,
            Assert.Single(fixture.Service.Preview(fixture.Current, [issued.Proposal.Id]).Items).Blocker);
        Assert.Equal(ErrorCode.RevisionConflict,
            Assert.Throws<LeviathanException>(() => fixture.Service.Accept(fixture.Current, [issued.Proposal.Id], Acceptor)).Error.Code);
        Assert.Equal(saves, fixture.Store.Saves);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Accepted_reading_or_restoration_has_exact_reversible_layer_annotation(bool restoration)
    {
        var fixture = new Fixture();
        var issued = await fixture.Issue(restoration ? new(1, 1) : new(1, 2), "Ω",
            task: restoration ? AiTaskKind.Restoration : AiTaskKind.CandidateReading);

        var result = fixture.Service.Accept(fixture.Current, [issued.Proposal.Id], Acceptor);

        var annotation = Assert.Single(fixture.Current.Objects[0].Surfaces[0].Annotations);
        Assert.Equal(Acceptor, annotation.Author);
        if (restoration)
        {
            Assert.Equal(AnnotationKind.SuppliedEditorial, annotation.Kind);
            Assert.Equal(result.Acceptances[0].EditorialRevision.Id, annotation.AnchoredRevisionId);
            Assert.Equal(new ScalarRange(1, 2), annotation.Range);
        }
        else
        {
            Assert.Equal(AnnotationKind.AlternativeReading, annotation.Kind);
            Assert.Equal(issued.SourceRevision.Id, annotation.AnchoredRevisionId);
            Assert.Equal(issued.Proposal.Request.Range, annotation.Range);
            Assert.Equal("Ω", annotation.AlternativeText);
            Assert.Equal("choice/orig-reg", annotation.Description);
        }
    }

    [Theory]
    [InlineData(1, 2, "Ω", "αΩβ\u0301γδε", AiTaskKind.CandidateReading)]
    [InlineData(2, 4, "Ω", "α𐅀Ωγδε", AiTaskKind.CandidateReading)]
    [InlineData(3, 4, "\u0300", "α𐅀β\u0300γδε", AiTaskKind.CandidateReading)]
    [InlineData(7, 7, "Ω", "α𐅀β\u0301γδεΩ", AiTaskKind.Restoration)]
    public async Task Authoritative_scalar_span_replaces_only_that_span_and_persists_complete_l2_provenance(
        int start, int end, string replacement, string expected, AiTaskKind task)
    {
        var fixture = new Fixture();
        var original = fixture.Current;
        var source = original.Objects[0].Surfaces[0].Revisions[0];
        var issued = await fixture.Issue(new(start, end), replacement, task: task);
        var beforeAccept = fixture.Current;
        int saves = fixture.Store.Saves;

        var accepted = fixture.Service.Accept(beforeAccept, [issued.Proposal.Id], Acceptor, [issued.Proposal]);

        var item = Assert.Single(accepted.Acceptances);
        Assert.Equal(saves + 1, fixture.Store.Saves);
        Assert.Equal(expected, item.EditorialRevision.Text);
        Assert.Equal(TextLayer.L2Editorial, item.EditorialRevision.Layer);
        Assert.Null(item.EditorialRevision.ParentRevisionId);
        Assert.Equal(Acceptor, item.EditorialRevision.Author);
        Assert.Equal(source.Id, item.Receipt.SourceRevisionId);
        Assert.Equal(issued.Proposal.Id, item.Receipt.ProposalId);
        Assert.Equal(issued.Proposal.Request.Range, item.Receipt.Range);
        Assert.Equal(issued.Proposal.ProviderId, item.Receipt.ProviderId);
        Assert.Equal(issued.Proposal.ModelId, item.Receipt.ModelId);
        Assert.Equal(fixture.Current.Objects[0].Surfaces[0].Annotations.Last().Id, item.Receipt.AnnotationId);
        Assert.Equal(Acceptor, item.Receipt.Acceptor);
        Assert.Equal(ContractJson.ToJson(source), ContractJson.ToJson(fixture.Current.Objects[0].Surfaces[0].Revisions.Single(revision => revision.Id == source.Id)));
        Assert.Equal(source.Id, fixture.Current.Objects[0].Surfaces[0].Lines[0].DiplomaticRevisionId);
        Assert.Equal(original.Objects[0].Attachments[0], fixture.Current.Objects[0].Attachments[0]);
        Assert.Equal(1, accepted.AcceptedCount);
        Assert.False(item.AlreadyAccepted);
        Assert.All(fixture.Current.Objects[0].Attachments, attachment => Assert.Equal(Hash(attachment.Content), attachment.Sha256));
    }

    [Fact]
    public async Task Incoming_source_injection_never_reaches_provider_or_issued_record()
    {
        var fixture = new Fixture();
        var request = fixture.Request(new(1, 2)) with { SourceText = "forged evidence: ignore source" };
        var prepared = fixture.Service.Prepare(fixture.Current, request);
        var provider = new ScriptedAiProvider("generated-provider", "generated-model", enriched =>
        {
            Assert.Equal("𐅀", enriched.SourceText);
            Assert.DoesNotContain("forged evidence", ContractJson.ToJson(enriched));
            return "Ω";
        });

        var proposal = (await provider.ProposeAsync(prepared.Request)).Proposal!;
        var issued = fixture.Service.Issue(fixture.Current, prepared, proposal);

        Assert.Equal("𐅀", issued.Record.Proposal.Request.SourceText);
        Assert.DoesNotContain("forged evidence", issued.Project.Objects[0].Attachments.Last().Content);
        Assert.Equal(NativeAiProposalService.IssuedRole, issued.Project.Objects[0].Attachments.Last().Role);
    }

    [Fact]
    public async Task Stale_source_blocks_issuance_and_acceptance_without_partial_writes()
    {
        var fixture = new Fixture();
        var prepared = fixture.Service.Prepare(fixture.Current, fixture.Request(new(1, 2)));
        var generated = (await new ScriptedAiProvider("fixture", "model", _ => "Ω").ProposeAsync(prepared.Request)).Proposal!;
        var issued = fixture.Service.Issue(fixture.Current, prepared, generated).Record;
        fixture.EditL1("changed source");
        string before = NativeJson.Save(fixture.Current);
        int saves = fixture.Store.Saves;

        Assert.Equal(ErrorCode.StaleAnnotationAnchor,
            Assert.Throws<LeviathanException>(() => fixture.Service.Issue(fixture.Current, prepared, generated)).Error.Code);
        Assert.Equal(BulkAcceptanceBlocker.TargetSuperseded,
            Assert.Single(fixture.Service.Preview(fixture.Current, [issued.Proposal.Id]).Items).Blocker);
        Assert.Equal(ErrorCode.StaleAnnotationAnchor,
            Assert.Throws<LeviathanException>(() => fixture.Service.Accept(fixture.Current, [issued.Proposal.Id], Acceptor)).Error.Code);
        Assert.Equal(saves, fixture.Store.Saves);
        Assert.Equal(before, NativeJson.Save(fixture.Current));
    }

    [Fact]
    public async Task Unrelated_l2_edits_survive_and_current_editorial_head_is_the_parent()
    {
        var fixture = new Fixture();
        var issued = await fixture.Issue(new(1, 2), "Ω");
        var prior = fixture.EditL2("α𐅀β\u0301γδX");

        var result = fixture.Service.Accept(fixture.Current, [issued.Proposal.Id], Acceptor);

        var revision = Assert.Single(result.Acceptances).EditorialRevision;
        Assert.Equal("αΩβ\u0301γδX", revision.Text);
        Assert.Equal(prior.Id, revision.ParentRevisionId);
        Assert.Equal(prior.Id, result.Acceptances[0].Receipt.ParentEditorialRevisionId);
    }

    [Fact]
    public async Task Conflicting_editorial_change_blocks_preview_and_accept_without_writes()
    {
        var fixture = new Fixture();
        var issued = await fixture.Issue(new(1, 2), "Ω");
        fixture.EditL2("αZβ\u0301γδε");
        var before = NativeJson.Save(fixture.Current);
        int saves = fixture.Store.Saves;

        var preview = fixture.Service.Preview(fixture.Current, [issued.Proposal.Id]);
        Assert.Equal(BulkAcceptanceBlocker.EditorialConflict, Assert.Single(preview.Items).Blocker);
        Assert.Equal(ErrorCode.RevisionConflict,
            Assert.Throws<LeviathanException>(() => fixture.Service.Accept(fixture.Current, [issued.Proposal.Id], Acceptor)).Error.Code);
        Assert.Equal(saves, fixture.Store.Saves);
        Assert.Equal(before, NativeJson.Save(fixture.Current));
    }

    [Theory]
    [InlineData(AiTaskKind.Translation)]
    [InlineData(AiTaskKind.StructuredExtraction)]
    [InlineData(AiTaskKind.LexicalAnnotation)]
    [InlineData(AiTaskKind.MorphologicalAnnotation)]
    public async Task Advisory_issued_records_survive_but_cannot_replace_text(AiTaskKind task)
    {
        var fixture = new Fixture();
        var issued = await fixture.Issue(new(0, 1), "advisory", task: task);
        int saves = fixture.Store.Saves;

        Assert.Equal(BulkAcceptanceBlocker.UnsupportedTask,
            Assert.Single(fixture.Service.Preview(fixture.Current, [issued.Proposal.Id]).Items).Blocker);
        Assert.Equal(ErrorCode.StageContractViolation,
            Assert.Throws<LeviathanException>(() => fixture.Service.Accept(fixture.Current, [issued.Proposal.Id], Acceptor)).Error.Code);
        Assert.Equal(saves, fixture.Store.Saves);
        Assert.Equal(task, fixture.Service.GetIssued(fixture.Current, issued.Proposal.Id).Proposal.Request.Task);
    }

    [Fact]
    public async Task Supplied_graph_tampering_and_unissued_ids_never_create_editorial_revisions()
    {
        var fixture = new Fixture();
        var issued = await fixture.Issue(new(1, 2), "Ω");
        var forged = issued.Proposal with { ProposedText = "forged" };
        int saves = fixture.Store.Saves;
        Assert.Equal(ErrorCode.StageContractViolation,
            Assert.Throws<LeviathanException>(() => fixture.Service.Accept(fixture.Current, [forged.Id], Acceptor, [forged])).Error.Code);
        Assert.Equal(ErrorCode.StageContractViolation,
            Assert.Throws<LeviathanException>(() => fixture.Service.Accept(fixture.Current, [forged.Id], Acceptor, [null!])).Error.Code);
        var unknown = new EntityId(EntityKind.Proposal, "unissued");
        Assert.Equal(BulkAcceptanceBlocker.UnknownIssuedProposal, Assert.Single(fixture.Service.Preview(fixture.Current, [unknown]).Items).Blocker);
        Assert.Equal(ErrorCode.InputMissing,
            Assert.Throws<LeviathanException>(() => fixture.Service.Accept(fixture.Current, [unknown], Acceptor)).Error.Code);
        Assert.Equal(saves, fixture.Store.Saves);
        Assert.DoesNotContain(fixture.Current.Objects[0].Surfaces[0].Revisions, revision => revision.Layer == TextLayer.L2Editorial);
    }

    [Theory]
    [InlineData("checksum")]
    [InlineData("source")]
    [InlineData("version")]
    [InlineData("duplicate")]
    public async Task Corrupt_or_duplicate_issued_attachments_are_refused(string mode)
    {
        var fixture = new Fixture();
        var issued = await fixture.Issue(new(1, 2), "Ω");
        var project = fixture.Current;
        var obj = project.Objects[0];
        var attachment = obj.Attachments.Last();
        if (mode == "checksum") attachment = attachment with { Content = attachment.Content + " " };
        if (mode is "source" or "version")
        {
            var changed = mode == "source" ? issued with { SourceRevision = issued.SourceRevision with { Text = "forged source" } }
                : issued with { SchemaVersion = 99 };
            var content = ContractJson.ToJson(changed);
            attachment = attachment with { Content = content, Sha256 = Hash(content) };
        }
        fixture.Store.SaveProject(project with { Objects = [obj with
        {
            Attachments = mode == "duplicate" ? [.. obj.Attachments, attachment] : [obj.Attachments[0], attachment],
        }] });
        int saves = fixture.Store.Saves;

        Assert.Equal(ErrorCode.StageContractViolation,
            Assert.Throws<LeviathanException>(() => fixture.Service.Accept(fixture.Current, [issued.Proposal.Id], Acceptor)).Error.Code);
        Assert.Equal(saves, fixture.Store.Saves);
    }

    [Fact]
    public async Task Duplicate_batch_ids_or_two_proposals_for_one_line_cannot_partially_apply()
    {
        var fixture = new Fixture();
        var first = await fixture.Issue(new(0, 1), "A");
        var second = await fixture.Issue(new(1, 2), "B");
        int saves = fixture.Store.Saves;
        Assert.Equal(ErrorCode.StageContractViolation, Assert.Throws<LeviathanException>(() =>
            fixture.Service.Accept(fixture.Current, [first.Proposal.Id, first.Proposal.Id], Acceptor)).Error.Code);
        var preview = fixture.Service.Preview(fixture.Current, [first.Proposal.Id, second.Proposal.Id]);
        Assert.Equal(BulkAcceptanceBlocker.ConflictsWithEarlierInBatch, preview.Items[1].Blocker);
        Assert.Throws<LeviathanException>(() => fixture.Service.Accept(fixture.Current, [first.Proposal.Id, second.Proposal.Id], Acceptor));
        Assert.Equal(saves, fixture.Store.Saves);
    }

    [Fact]
    public async Task Multi_line_batch_is_one_save_and_one_blocked_item_prevents_every_write()
    {
        var fixture = new Fixture();
        var first = await fixture.Issue(new(0, 1), "A");
        var second = await fixture.Issue(new(0, 1), "B", line: 1);
        var ids = new[] { first.Proposal.Id, second.Proposal.Id };
        fixture.EditL2("conflicting", line: 1);
        int saves = fixture.Store.Saves;
        var before = NativeJson.Save(fixture.Current);
        Assert.Throws<LeviathanException>(() => fixture.Service.Accept(fixture.Current, ids, Acceptor));
        Assert.Equal(saves, fixture.Store.Saves);
        Assert.Equal(before, NativeJson.Save(fixture.Current));

        var clean = new Fixture();
        var a = await clean.Issue(new(0, 1), "A");
        var b = await clean.Issue(new(0, 1), "B", line: 1);
        saves = clean.Store.Saves;
        var result = clean.Service.Accept(clean.Current, [a.Proposal.Id, b.Proposal.Id], Acceptor);
        Assert.Equal(saves + 1, clean.Store.Saves);
        Assert.Equal(2, result.Acceptances.Count);
        Assert.Equal(2, clean.Current.Objects[0].Attachments.Count(item => item.Role == NativeAiProposalService.AcceptanceRole));
    }

    [Fact]
    public async Task Optimistic_conflict_preserves_concurrent_edit_without_receipts_or_revisions()
    {
        var fixture = new Fixture();
        var issued = await fixture.Issue(new(1, 2), "Ω");
        var racing = new RacingStore(fixture.Store);
        var service = new NativeAiProposalService(racing);

        Assert.Equal(ErrorCode.RevisionConflict,
            Assert.Throws<LeviathanException>(() => service.Accept(fixture.Current, [issued.Proposal.Id], Acceptor)).Error.Code);

        Assert.Equal("concurrent winner", fixture.Current.Name);
        Assert.DoesNotContain(fixture.Current.Objects[0].Attachments, item => item.Role == NativeAiProposalService.AcceptanceRole);
        Assert.DoesNotContain(fixture.Current.Objects[0].Surfaces[0].Revisions, item => item.Layer == TextLayer.L2Editorial);
    }

    [Fact]
    public async Task Serialize_reload_and_replay_return_same_receipt_without_a_second_save_but_stale_l1_still_blocks()
    {
        var fixture = new Fixture();
        var issued = await fixture.Issue(new(1, 2), "Ω");
        var first = fixture.Service.Accept(fixture.Current, [issued.Proposal.Id], Acceptor);
        var serialized = NativeJson.Save(fixture.Current);
        fixture.Store.SaveProject(NativeJson.Load(serialized) with { Version = fixture.Current.Version });
        int saves = fixture.Store.Saves;
        var restarted = new NativeAiProposalService(fixture.Store);

        var replay = restarted.Accept(fixture.Current, [issued.Proposal.Id], new() { Id = "other-human", IsModel = false }, [issued.Proposal]);

        Assert.Equal(saves, fixture.Store.Saves);
        Assert.True(replay.Acceptances[0].AlreadyAccepted);
        Assert.Equal(first.Acceptances[0].Receipt, replay.Acceptances[0].Receipt);
        Assert.Equal(ContractJson.ToJson(first.Acceptances[0].EditorialRevision), ContractJson.ToJson(replay.Acceptances[0].EditorialRevision));
        Assert.Equal(ProposalStatus.Proposed, restarted.GetIssued(fixture.Current, issued.Proposal.Id).Proposal.Status);
        fixture.EditL1("superseded after acceptance");
        saves = fixture.Store.Saves;
        Assert.Equal(ErrorCode.StaleAnnotationAnchor,
            Assert.Throws<LeviathanException>(() => restarted.Accept(fixture.Current, [issued.Proposal.Id], Acceptor)).Error.Code);
        Assert.Equal(saves, fixture.Store.Saves);
    }

    [Fact]
    public async Task Receipt_integrity_and_editorial_revision_fingerprint_are_verified_on_replay()
    {
        var fixture = new Fixture();
        var issued = await fixture.Issue(new(1, 2), "Ω");
        var accepted = fixture.Service.Accept(fixture.Current, [issued.Proposal.Id], Acceptor);
        var project = fixture.Current;
        var obj = project.Objects[0];
        var surface = obj.Surfaces[0];
        fixture.Store.SaveProject(project with { Objects = [obj with { Surfaces = [surface with
        {
            Revisions = [.. surface.Revisions.Select(revision => revision.Id == accepted.Acceptances[0].EditorialRevision.Id
                ? revision with { Text = "tampered accepted text" } : revision)],
        }] }] });

        Assert.Equal(ErrorCode.StageContractViolation,
            Assert.Throws<LeviathanException>(() => fixture.Service.Accept(fixture.Current, [issued.Proposal.Id], Acceptor)).Error.Code);
    }

    [Fact]
    public async Task Acceptance_annotation_fingerprint_is_verified_on_replay()
    {
        var fixture = new Fixture();
        var issued = await fixture.Issue(new(1, 2), "Ω");
        fixture.Service.Accept(fixture.Current, [issued.Proposal.Id], Acceptor);
        var project = fixture.Current;
        var obj = project.Objects[0];
        var surface = obj.Surfaces[0];
        fixture.Store.SaveProject(project with { Objects = [obj with { Surfaces = [surface with
        {
            Annotations = [surface.Annotations[0] with { AlternativeText = "changed outside acceptance" }],
        }] }] });
        int saves = fixture.Store.Saves;

        Assert.Equal(ErrorCode.StageContractViolation,
            Assert.Throws<LeviathanException>(() => fixture.Service.Accept(fixture.Current, [issued.Proposal.Id], Acceptor)).Error.Code);
        Assert.Equal(saves, fixture.Store.Saves);
    }

    [Fact]
    public async Task Invalid_source_range_or_provider_graph_is_refused_before_issuance()
    {
        var fixture = new Fixture();
        int saves = fixture.Store.Saves;
        Assert.Equal(ErrorCode.StageContractViolation,
            Assert.Throws<LeviathanException>(() => fixture.Service.Prepare(fixture.Current, fixture.Request(new(0, 100)))).Error.Code);
        var prepared = fixture.Service.Prepare(fixture.Current, fixture.Request(new(1, 2)));
        var proposal = (await new ScriptedAiProvider("fixture", "model", _ => "Ω").ProposeAsync(prepared.Request)).Proposal!;

        Assert.Equal(ErrorCode.StageContractViolation, Assert.Throws<LeviathanException>(() => fixture.Service.Issue(
            fixture.Current, prepared, proposal with { Request = proposal.Request with { SourceText = "forged evidence" } })).Error.Code);
        Assert.Equal(saves, fixture.Store.Saves);
    }

    [Fact]
    public async Task Oversized_receipt_is_refused_without_partial_annotation_or_revision()
    {
        var fixture = new Fixture();
        var issued = await fixture.Issue(new(1, 2), "Ω");
        var before = NativeJson.Save(fixture.Current);
        int saves = fixture.Store.Saves;

        Assert.Equal(ErrorCode.StageContractViolation, Assert.Throws<LeviathanException>(() => fixture.Service.Accept(
            fixture.Current, [issued.Proposal.Id], new() { Id = new string('x', TeiXml.MaxAttachmentBytes), IsModel = false })).Error.Code);
        Assert.Equal(saves, fixture.Store.Saves);
        Assert.Equal(before, NativeJson.Save(fixture.Current));
    }

    [Fact]
    public async Task Read_only_preview_does_not_mutate_a_reloaded_project_or_save()
    {
        var fixture = new Fixture();
        var issued = await fixture.Issue(new(1, 2), "Ω");
        var project = fixture.Current;
        var before = NativeJson.Save(project);
        int saves = fixture.Store.Saves;

        Assert.True(fixture.Service.Preview(project, [issued.Proposal.Id], [issued.Proposal]).IsFullyApplicable);

        Assert.Equal(saves, fixture.Store.Saves);
        Assert.Equal(before, NativeJson.Save(project));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Imported_editorial_chronology_is_preserved_or_explicitly_refused(bool exhausted)
    {
        var fixture = new Fixture();
        var issued = await fixture.Issue(new(1, 2), "Ω");
        var prior = fixture.EditL2("α𐅀β\u0301γδX");
        var project = fixture.Current;
        var obj = project.Objects[0];
        var surface = obj.Surfaces[0];
        var future = prior with { CreatedAt = exhausted ? DateTimeOffset.MaxValue : DateTimeOffset.UtcNow.AddDays(1) };
        fixture.Store.SaveProject(project with { Objects = [obj with { Surfaces = [surface with
        {
            Revisions = [.. surface.Revisions.Select(revision => revision.Id == prior.Id ? future : revision)],
        }] }] });

        if (exhausted)
        {
            int saves = fixture.Store.Saves;
            Assert.Equal(BulkAcceptanceBlocker.EditorialConflict,
                Assert.Single(fixture.Service.Preview(fixture.Current, [issued.Proposal.Id]).Items).Blocker);
            Assert.Equal(ErrorCode.RevisionConflict,
                Assert.Throws<LeviathanException>(() => fixture.Service.Accept(fixture.Current, [issued.Proposal.Id], Acceptor)).Error.Code);
            Assert.Equal(saves, fixture.Store.Saves);
            return;
        }
        var result = fixture.Service.Accept(fixture.Current, [issued.Proposal.Id], Acceptor);

        Assert.Equal(future.Id, result.Acceptances[0].EditorialRevision.ParentRevisionId);
        Assert.True(result.Acceptances[0].EditorialRevision.CreatedAt > future.CreatedAt);
        Assert.True(result.Acceptances[0].Receipt.AcceptedAt < result.Acceptances[0].EditorialRevision.CreatedAt);
        Assert.Equal(result.Acceptances[0].EditorialRevision.Id, fixture.Current.Objects[0].Surfaces[0].Revisions
            .Where(revision => revision.Layer == TextLayer.L2Editorial).OrderBy(revision => revision.CreatedAt).Last().Id);
    }

    [Fact]
    public async Task Oversized_issued_record_is_refused_without_saving()
    {
        var fixture = new Fixture(new string('α', TeiXml.MaxAttachmentBytes));
        var prepared = fixture.Service.Prepare(fixture.Current, fixture.Request(new(0, 1)));
        var proposal = (await new ScriptedAiProvider("fixture", "model", _ => "Ω").ProposeAsync(prepared.Request)).Proposal!;
        int saves = fixture.Store.Saves;

        Assert.Equal(ErrorCode.StageContractViolation,
            Assert.Throws<LeviathanException>(() => fixture.Service.Issue(fixture.Current, prepared, proposal)).Error.Code);
        Assert.Equal(saves, fixture.Store.Saves);
    }

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private sealed class Fixture
    {
        public CountingStore Store { get; } = new();
        public NativeAiProposalService Service { get; }
        public NativeProject Current => Store.LoadProject(new(EntityKind.Collection, "native-ai"))!;
        public Fixture(string text = "α𐅀β\u0301γδε")
        {
            var lines = Enumerable.Range(0, 2).Select(index => new NativeLine
            {
                Id = new(EntityKind.Line, "line-" + index), FrameRef = new(EntityKind.Capture, "capture"), RegionId = new(EntityKind.Region, "region"),
                Polygon = [new(10, 10 + index * 30), new(90, 10 + index * 30), new(90, 30 + index * 30), new(10, 30 + index * 30)],
                Baseline = [new(10, 28 + index * 30), new(90, 28 + index * 30)],
                DiplomaticRevisionId = new(EntityKind.TranscriptionRevision, "source-" + index),
            }).ToArray();
            var source = lines.Select((line, index) => new TextRevision
            {
                Id = line.DiplomaticRevisionId!.Value, ScopeId = line.Id, Layer = TextLayer.L1Diplomatic,
                Author = new() { Id = "generated-source", IsModel = false }, CreatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                Text = index == 0 ? text : "ζηθικ",
            }).ToArray();
            Store.SaveProject(new NativeProject
            {
                Id = new(EntityKind.Collection, "native-ai"), Name = "generated native AI", Objects = [new NativeObject
                {
                    Id = new(EntityKind.PhysicalObject, "object"),
                    Attachments = [new NativeAttachment
                    {
                        Id = "source", Name = "source.xml", MediaType = "text/xml", Role = "original-tei-markup",
                        Content = "<source>invented evidence</source>", Sha256 = Hash("<source>invented evidence</source>"),
                    }],
                    Surfaces = [new NativeSurface
                    {
                        Id = new(EntityKind.Surface, "surface"), Kind = "page",
                        Captures = [new NativeCapture
                        {
                            Id = new(EntityKind.Capture, "capture"), ImageLocation = "generated-image-not-opened.png", WidthPx = 100, HeightPx = 100,
                            Frame = new CoordinateFrame
                            {
                                ImageRef = new(EntityKind.Capture, "capture"),
                                ImageChecksum = new Checksum { Algorithm = "unavailable", Digest = "generated-test-frame" },
                            },
                        }],
                        Regions = [new NativeRegion
                        {
                            Id = new(EntityKind.Region, "region"), FrameRef = new(EntityKind.Capture, "capture"),
                            Polygon = [new(0, 0), new(100, 0), new(100, 100), new(0, 100)],
                            LayoutSource = LayoutSource.Manual, ReadingOrderIndex = 0,
                        }],
                        Lines = lines, Revisions = source,
                    }],
                }],
            });
            Service = new(Store);
        }
        public AiProposalRequest Request(ScalarRange range, int line = 0, AiTaskKind task = AiTaskKind.CandidateReading) => new()
        {
            TargetRevisionId = Current.Objects[0].Surfaces[0].Lines[line].DiplomaticRevisionId!.Value,
            Range = range, Task = task, ProviderConfigRef = "generated", PromptVersion = "generated-v1",
        };
        public async Task<NativeAiIssuedProposal> Issue(ScalarRange range, string text, int line = 0, AiTaskKind task = AiTaskKind.CandidateReading)
        {
            var prepared = Service.Prepare(Current, Request(range, line, task));
            var proposal = (await new ScriptedAiProvider("generated-provider", "generated-model", _ => text).ProposeAsync(prepared.Request)).Proposal!;
            return Service.Issue(Current, prepared, proposal).Record;
        }
        public TextRevision EditL2(string text, int line = 0) => new TranscriptionService(Store).AppendLineRevision(
            Current, Current.Objects[0].Surfaces[0].Lines[line].Id, TextLayer.L2Editorial, text, Acceptor).Revision;
        public void EditL1(string text) => new TranscriptionService(Store).AppendLineRevision(
            Current, Current.Objects[0].Surfaces[0].Lines[0].Id, TextLayer.L1Diplomatic, text, Acceptor);
    }

    private sealed class CountingStore : IProjectStore
    {
        private readonly InMemoryStore _inner = new();
        public int Saves { get; private set; }
        public long SaveProject(NativeProject project) { var version = _inner.SaveProject(project); Saves++; return version; }
        public NativeProject? LoadProject(EntityId id) => _inner.LoadProject(id);
        public IReadOnlyList<(EntityId Id, string Name)> ListProjects() => _inner.ListProjects();
    }
    private sealed class RacingStore(CountingStore inner) : IProjectStore
    {
        public long SaveProject(NativeProject project)
        {
            var latest = inner.LoadProject(project.Id)!;
            inner.SaveProject(latest with { Name = "concurrent winner" });
            return inner.SaveProject(project);
        }
        public NativeProject? LoadProject(EntityId id) => inner.LoadProject(id);
        public IReadOnlyList<(EntityId Id, string Name)> ListProjects() => inner.ListProjects();
    }
}
