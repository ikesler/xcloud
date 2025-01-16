using System.IdentityModel.Tokens.Jwt;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Microsoft.IdentityModel.Tokens;

namespace XCloud.Cli.Commands;

[Command("issue-jwt-token")]
public class IssueApiJwt : ICommand
{
    [CommandOption("key", 'k',
        IsRequired = true,
        Description = "Key for signing the token")]
    public string Key { get; init; } = null!;

    public async ValueTask ExecuteAsync(IConsole console)
    {
        console.Output.WriteLine("Using private key: {0}", Key);
        var signingKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(Key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken 
        (
            issuer: "XCloud",
            audience: "XCloud",
            expires: DateTime.Now.AddYears(13),
            signingCredentials: credentials
        );
        var tt = new JwtSecurityTokenHandler();
        console.Output.WriteLine(tt.WriteToken(jwt));
    }
}
