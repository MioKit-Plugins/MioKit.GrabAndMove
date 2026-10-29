namespace GrabAndMove.Nodes;

[EavType(GrabAndMoveConst.SearchGroupTypeId)]
public sealed class GrabAndMoveSearchGroup : MioObject, ISearchableFeature
{
    public GrabAndMoveSearchGroup(string id) : base(id)
    {
    }

    public override Guid MioType => GrabAndMoveConst.SearchGroupType;

    public override async Task EnsureSubTreeLoadedAsync()
    {
        await EnsureTreeLoadedAsync<StartGrabAndMoveNode>(GrabAndMoveConst.StartNodeId);
        await EnsureTreeLoadedAsync<StopGrabAndMoveNode>(GrabAndMoveConst.StopNodeId);
    }

    public ValueTask SearchAsync(
        SearchRequest request,
        IWriteOnlyResultList<SearchResult> results,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.IsEmptyText)
            return ValueTask.CompletedTask;

        foreach (var node in this.GetDescendants<MioObject>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (node is not (StartGrabAndMoveNode or StopGrabAndMoveNode))
                continue;
            if (!node.TryMatch(request.SearchText!, out var match, out var title))
                continue;

            results.Add(new SearchResult(request, node)
            {
                Title = title,
                Description = node.GetDescription(),
                MatchResult = match!.Value,
                Source = "抓取移动"
            });
        }

        return ValueTask.CompletedTask;
    }
}
