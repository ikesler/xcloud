using System.Diagnostics.CodeAnalysis;

namespace XCloud.Sharing.Impl;

public record ShareEvaluation(
    SharedFileInfo? File = null,
    SharedFileInfo? BlockedBy = null,
    NavigationInfo? Navigation = null)
{
    [MemberNotNullWhen(true, nameof(File))]
    public bool CanAccess() => File != null && BlockedBy == null;

    [MemberNotNullWhen(true, nameof(BlockedBy))]
    public bool IsBlocked() => BlockedBy != null;
}
