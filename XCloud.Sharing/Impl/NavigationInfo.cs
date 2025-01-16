namespace XCloud.Sharing.Impl;

public record NavigationLink(string Url, string Title);

public record NavigationInfo(NavigationLink Parent, NavigationLink[] Children);
