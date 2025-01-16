namespace XCloud.ReadEra.Models;

public record ReadEraWordData(
    string WordKey,
    string WordTitle,
    long WordModifiedTime,
    ReadEraColor WordColor,
    string? WordComment,
    string DocUri,
    ReadEraDictionary GroupId);
